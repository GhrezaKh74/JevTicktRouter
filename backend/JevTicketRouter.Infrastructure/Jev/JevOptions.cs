using System.ComponentModel.DataAnnotations;

namespace JevTicketRouter.Infrastructure.Jev;

/// <summary>
/// Connection settings for the TypeSafe API, bound from the <c>Jev</c> configuration section.
/// <para>
/// <see cref="ApiKey"/> is deliberately never written to configuration files in this repository. It
/// comes from .NET User Secrets or the <c>TYPESAFE_API_KEY</c> environment variable, and is never
/// logged or returned to a client.
/// </para>
/// </summary>
public sealed class JevOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Jev";

    /// <summary>The environment variable read when no user secret is configured.</summary>
    public const string ApiKeyEnvironmentVariable = "TYPESAFE_API_KEY";

    /// <summary>
    /// The TypeSafe API key. When empty the application starts in mock mode instead of failing.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>The API base address. Overridable only so tests can point at a stub server.</summary>
    [Required]
    public string BaseUrl { get; set; } = "https://api.typesafe.ai";

    /// <summary>The evaluation endpoint path, relative to <see cref="BaseUrl"/>.</summary>
    [Required]
    public string EvaluationPath { get; set; } = "/v1/systemone";

    /// <summary>Overall timeout for a single evaluation, in seconds.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// How many times a transient failure (429, 529, 5xx, transport error) is retried before giving up.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Forces mock mode even when an API key is present. Useful for demos and offline development.
    /// </summary>
    public bool ForceMockMode { get; set; }

    /// <summary>True when a usable API key has been supplied.</summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>True when the application should serve deterministic sample answers.</summary>
    public bool UseMockMode => ForceMockMode || !HasApiKey;
}
