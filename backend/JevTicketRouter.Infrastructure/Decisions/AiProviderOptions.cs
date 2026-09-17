using JevTicketRouter.Domain.Decisions;

namespace JevTicketRouter.Infrastructure.Decisions;

/// <summary>
/// Chooses which decision engine runs, bound from the <c>Ai</c> configuration section or the
/// <c>AI_PROVIDER</c> environment variable.
/// </summary>
public sealed class AiProviderOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Ai";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string ProviderEnvironmentVariable = "AI_PROVIDER";

    /// <summary>The requested provider: <c>Jev</c>, <c>Local</c>, or <c>Mock</c>.</summary>
    public string Provider { get; set; } = nameof(AiProvider.Jev);

    /// <summary>
    /// Parses <see cref="Provider"/>, falling back to Jev for an unrecognised value. The resolver
    /// reports the fallback rather than failing, so a typo does not take the service down.
    /// </summary>
    public AiProvider Resolve(out bool wasRecognised)
    {
        wasRecognised = Enum.TryParse<AiProvider>(Provider?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);

        return wasRecognised ? parsed : AiProvider.Jev;
    }
}
