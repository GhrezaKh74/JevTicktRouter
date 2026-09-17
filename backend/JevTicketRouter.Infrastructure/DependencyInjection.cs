using JevTicketRouter.Application.Benchmarking;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Infrastructure.Benchmarking;
using JevTicketRouter.Infrastructure.Decisions;
using JevTicketRouter.Infrastructure.Decisions.Local;
using JevTicketRouter.Infrastructure.Jev;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace JevTicketRouter.Infrastructure;

/// <summary>Registration helpers for the infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers exactly one <see cref="IDecisionEngine"/>, chosen from <c>AI_PROVIDER</c>.
    /// <para>
    /// Provider choice lives here and nowhere else. Nothing above this method — not the triage
    /// service, not the API contract, not the React client — knows which engine ran.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        BindOptions(services, configuration);

        var providerOptions = ReadProviderOptions(configuration);
        var jevOptions = ReadJevOptions(configuration);
        var localOptions = ReadLocalOptions(configuration);

        var triageModel = configuration.GetSection(TriageOptions.SectionName)["Model"];

        var selection = DecisionEngineResolver.Resolve(
            providerOptions,
            jevOptions,
            localOptions,
            string.IsNullOrWhiteSpace(triageModel) ? "jev-latest" : triageModel);

        services.AddSingleton(selection);

        // The deterministic mock is always available: it backs Mock mode and every fallback path.
        services.AddSingleton<MockJevClient>();
        services.AddSingleton<MockDecisionEngine>();

        // Engines are registered as concrete types, and the interface is bound to whichever one the
        // resolver picked. Registering several engines under IDecisionEngine would leave the engine
        // serving production dependent on registration order.
        var jevAvailable = jevOptions.HasApiKey && !jevOptions.ForceMockMode;
        var localAvailable = IsLocalAvailable(localOptions);

        if (jevAvailable)
        {
            AddJevEngine(services);
        }

        if (localAvailable)
        {
            AddLocalEngine(services);
        }

        services.AddSingleton<IDecisionEngine>(provider => selection.Provider switch
        {
            AiProvider.Jev => provider.GetRequiredService<TypeSafeJevDecisionEngine>(),
            AiProvider.Local => provider.GetRequiredService<LocalOpenAiCompatibleDecisionEngine>(),
            _ => provider.GetRequiredService<MockDecisionEngine>(),
        });

        // The benchmark compares the real providers when both are configured. When neither is, it
        // still measures the mock so the endpoint remains useful in development.
        services.AddSingleton(provider =>
        {
            var engines = new List<IDecisionEngine>();

            if (jevAvailable)
            {
                engines.Add(provider.GetRequiredService<TypeSafeJevDecisionEngine>());
            }

            if (localAvailable)
            {
                engines.Add(provider.GetRequiredService<LocalOpenAiCompatibleDecisionEngine>());
            }

            if (engines.Count == 0)
            {
                engines.Add(provider.GetRequiredService<MockDecisionEngine>());
            }

            return new BenchmarkEngines(engines);
        });

        services.AddSingleton<IBenchmarkRunner, BenchmarkRunner>();

        return services;
    }

    /// <summary>
    /// Logs, once at startup, which engine is running and why. Never logs a key or an endpoint
    /// credential.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    public static void LogDecisionEngine(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var selection = services.GetRequiredService<DecisionEngineSelection>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("JevTicketRouter.Startup");

        if (selection.Provider == AiProvider.Mock)
        {
            logger.LogWarning("AI provider: MOCK MODE. {Reason}", selection.Reason);
        }
        else
        {
            logger.LogInformation("AI provider: {Provider}. {Reason}", selection.Provider, selection.Reason);
        }

        if (selection.Provider == AiProvider.Local)
        {
            logger.LogInformation(
                "Local mode makes no internet calls: no telemetry, no analytics, no cloud fallback, "
                    + "and no automatic model downloads.");
        }
    }

    private static void BindOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName))
            .Configure(options =>
            {
                var fromEnvironment = Environment.GetEnvironmentVariable(
                    AiProviderOptions.ProviderEnvironmentVariable);

                if (!string.IsNullOrWhiteSpace(fromEnvironment))
                {
                    options.Provider = fromEnvironment;
                }
            });

        services
            .AddOptions<JevOptions>()
            .Bind(configuration.GetSection(JevOptions.SectionName))
            .Configure(options =>
            {
                // User secrets and appsettings bind to Jev:ApiKey; the bare environment variable named
                // in the TypeSafe docs is honoured as well, and only used if nothing else supplied one.
                if (!options.HasApiKey)
                {
                    options.ApiKey = Environment.GetEnvironmentVariable(JevOptions.ApiKeyEnvironmentVariable);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<LocalAiOptions>()
            .Bind(configuration.GetSection(LocalAiOptions.SectionName))
            .Configure(ApplyLocalEnvironment)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddJevEngine(IServiceCollection services)
    {
        services
            .AddHttpClient<IJevClient, JevHttpClient>(JevHttpClient.HttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<JevOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JevTicketRouter/1.0");
            })
            .AddResilienceHandler("typesafe-jev", (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<JevOptions>>().Value;

                // The TypeSafe docs call for exponential backoff on 429 and 529 specifically; 5xx and
                // transport faults are retried on the same policy.
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(400),
                    ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome)),
                });

                builder.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));
            });

        services.AddSingleton<TypeSafeJevDecisionEngine>();
    }

    private static void AddLocalEngine(IServiceCollection services)
    {
        services
            .AddHttpClient<LocalOpenAiCompatibleDecisionEngine>(
                LocalOpenAiCompatibleDecisionEngine.HttpClientName,
                (provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<LocalAiOptions>>().Value;

                    // A trailing slash matters: without it the last path segment of BaseUrl ("/v1")
                    // would be dropped when the relative request path is resolved against it.
                    var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";

                    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("JevTicketRouter/1.0");
                });
    }

    /// <summary>
    /// True when a local engine could actually be constructed: a model is named and the endpoint
    /// passes the security check.
    /// </summary>
    private static bool IsLocalAvailable(LocalAiOptions options) =>
        !string.IsNullOrWhiteSpace(options.Model)
        && LocalEndpointGuard.Inspect(options.BaseUrl, options.AllowPublicEndpoint).IsAllowed;

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutException)
        {
            return true;
        }

        if (outcome.Result is not { } response)
        {
            return false;
        }

        var status = (int)response.StatusCode;

        return status is 408 or 429 or 529 or >= 500 and < 600;
    }

    private static AiProviderOptions ReadProviderOptions(IConfiguration configuration)
    {
        var options = new AiProviderOptions();
        configuration.GetSection(AiProviderOptions.SectionName).Bind(options);

        var fromEnvironment = Environment.GetEnvironmentVariable(
            AiProviderOptions.ProviderEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            options.Provider = fromEnvironment;
        }

        return options;
    }

    private static JevOptions ReadJevOptions(IConfiguration configuration)
    {
        var options = new JevOptions();
        configuration.GetSection(JevOptions.SectionName).Bind(options);

        if (!options.HasApiKey)
        {
            options.ApiKey = Environment.GetEnvironmentVariable(JevOptions.ApiKeyEnvironmentVariable);
        }

        return options;
    }

    private static LocalAiOptions ReadLocalOptions(IConfiguration configuration)
    {
        var options = new LocalAiOptions();
        configuration.GetSection(LocalAiOptions.SectionName).Bind(options);
        ApplyLocalEnvironment(options);

        return options;
    }

    /// <summary>Lets the documented bare environment variables override bound configuration.</summary>
    private static void ApplyLocalEnvironment(LocalAiOptions options)
    {
        var baseUrl = Environment.GetEnvironmentVariable(LocalAiOptions.BaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.BaseUrl = baseUrl;
        }

        var model = Environment.GetEnvironmentVariable(LocalAiOptions.ModelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(model))
        {
            options.Model = model;
        }

        var apiKey = Environment.GetEnvironmentVariable(LocalAiOptions.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            options.ApiKey = apiKey;
        }
    }
}
