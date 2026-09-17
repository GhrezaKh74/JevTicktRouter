using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Infrastructure.Decisions.Local;
using JevTicketRouter.Infrastructure.Jev;

namespace JevTicketRouter.Infrastructure.Decisions;

/// <summary>
/// Decides which decision engine to register, from configuration alone.
/// <para>
/// Pure and side-effect free so the whole selection matrix can be unit-tested without a host. The
/// guiding rule is that the application always starts: a provider that cannot run degrades to Mock
/// with a stated reason, rather than crashing a service on a missing key. The one exception is a
/// local endpoint that fails the security check, which is a misconfiguration an operator must see
/// and fix rather than have silently worked around.
/// </para>
/// </summary>
public static class DecisionEngineResolver
{
    /// <summary>The model name reported when the deterministic mock answers.</summary>
    public const string MockModel = "deterministic-mock";

    /// <summary>Chooses the engine.</summary>
    /// <param name="providerOptions">The requested provider.</param>
    /// <param name="jevOptions">TypeSafe settings, for the API key.</param>
    /// <param name="localOptions">Local endpoint settings.</param>
    /// <exception cref="InvalidOperationException">
    /// Local was requested and its endpoint is not permitted. Falling back would send restricted data
    /// somewhere the operator did not intend, so this fails loudly instead.
    /// </exception>
    /// <param name="jevModel">The Jev model name, reported when Jev or Mock answers.</param>
    public static DecisionEngineSelection Resolve(
        AiProviderOptions providerOptions,
        JevOptions jevOptions,
        LocalAiOptions localOptions,
        string jevModel = "jev-latest")
    {
        ArgumentNullException.ThrowIfNull(providerOptions);
        ArgumentNullException.ThrowIfNull(jevOptions);
        ArgumentNullException.ThrowIfNull(localOptions);

        var requested = providerOptions.Resolve(out var wasRecognised);

        if (!wasRecognised)
        {
            return new DecisionEngineSelection(
                AiProvider.Mock,
                AiProvider.Mock,
                MockModel,
                $"'{providerOptions.Provider}' is not a recognised {AiProviderOptions.ProviderEnvironmentVariable} "
                    + $"value. Expected Jev, Local, or Mock. Falling back to Mock mode.");
        }

        return requested switch
        {
            AiProvider.Mock => new DecisionEngineSelection(
                AiProvider.Mock,
                requested,
                MockModel,
                "Mock mode was requested explicitly. No model is called and no network request is made."),

            AiProvider.Local => ResolveLocal(requested, localOptions),

            _ => ResolveJev(requested, jevOptions, jevModel),
        };
    }

    private static DecisionEngineSelection ResolveLocal(AiProvider requested, LocalAiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return new DecisionEngineSelection(
                AiProvider.Mock,
                requested,
                MockModel,
                $"Local mode was requested but {LocalAiOptions.ModelEnvironmentVariable} is not set. "
                    + "Falling back to Mock mode.");
        }

        var verdict = LocalEndpointGuard.Inspect(options.BaseUrl, options.AllowPublicEndpoint);

        if (!verdict.IsAllowed)
        {
            // Deliberately fatal. Quietly switching to another provider here could route restricted
            // tickets somewhere the operator never approved.
            throw new InvalidOperationException(verdict.Reason);
        }

        return new DecisionEngineSelection(
            AiProvider.Local,
            requested,
            options.Model!,
            $"Local mode. Calling {options.BaseUrl} with model '{options.Model}'. {verdict.Reason}");
    }

    private static DecisionEngineSelection ResolveJev(
        AiProvider requested,
        JevOptions options,
        string jevModel)
    {
        if (options.ForceMockMode)
        {
            return new DecisionEngineSelection(
                AiProvider.Mock,
                requested,
                MockModel,
                "Jev was requested but Jev:ForceMockMode is enabled. Running in Mock mode.");
        }

        if (!options.HasApiKey)
        {
            return new DecisionEngineSelection(
                AiProvider.Mock,
                requested,
                MockModel,
                $"Jev was requested but no {JevOptions.ApiKeyEnvironmentVariable} is configured. "
                    + "Falling back to Mock mode with deterministic sample answers.");
        }

        return new DecisionEngineSelection(
            AiProvider.Jev,
            requested,
            jevModel,
            $"Live Jev. Calling {options.BaseUrl}{options.EvaluationPath}.");
    }
}
