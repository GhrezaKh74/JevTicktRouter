using JevTicketRouter.Application.Jev.Abstractions;
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
    /// Registers the Jev client. Binds <see cref="JevOptions"/>, falls back to the
    /// <c>TYPESAFE_API_KEY</c> environment variable when no user secret is set, and selects the live
    /// HTTP client or the deterministic mock based on whether a key is available.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        var jevOptions = ResolveOptions(configuration);

        if (jevOptions.UseMockMode)
        {
            services.AddSingleton<IJevClient, MockJevClient>();
            return services;
        }

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

        return services;
    }

    /// <summary>
    /// Logs, once at startup, which mode the Jev integration is running in. Never logs the key itself.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    public static void LogJevMode(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetRequiredService<IOptions<JevOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("JevTicketRouter.Startup");

        if (options.UseMockMode)
        {
            logger.LogWarning(
                "Jev is running in MOCK MODE: no {EnvironmentVariable} was configured, so triage returns "
                    + "deterministic sample answers. Set the key via dotnet user-secrets or the environment "
                    + "variable to call the real TypeSafe API.",
                JevOptions.ApiKeyEnvironmentVariable);
        }
        else
        {
            logger.LogInformation(
                "Jev is running in LIVE MODE against {BaseUrl}{Path}.",
                options.BaseUrl,
                options.EvaluationPath);
        }
    }

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

    private static JevOptions ResolveOptions(IConfiguration configuration)
    {
        var options = new JevOptions();
        configuration.GetSection(JevOptions.SectionName).Bind(options);

        if (!options.HasApiKey)
        {
            options.ApiKey = Environment.GetEnvironmentVariable(JevOptions.ApiKeyEnvironmentVariable);
        }

        return options;
    }
}
