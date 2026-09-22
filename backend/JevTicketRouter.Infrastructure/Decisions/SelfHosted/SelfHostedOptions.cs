using System.ComponentModel.DataAnnotations;

namespace JevTicketRouter.Infrastructure.Decisions.SelfHosted;

/// <summary>
/// Settings for a self-hosted System One model, bound from the <c>SelfHosted</c> section.
/// <para>
/// The reference implementation is <see href="https://github.com/Barneyjm/circuit">circuit</see>:
/// open-weights models that answer typed questions with calibrated probabilities in one forward
/// pass, and that serve TypeSafe's <c>POST /v1/systemone</c> contract verbatim. Because the wire
/// format is identical, this provider reuses the same client, question set, and answer mapper as
/// the hosted API — only the address changes.
/// </para>
/// <para>
/// Note that these models need a real server, not Ollama: the calibrated probabilities come from a
/// pointer readout head on top of the LoRA, which a plain GGUF conversion drops. Run the project's
/// own <c>python -m s1proto</c> server.
/// </para>
/// </summary>
public sealed class SelfHostedOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "SelfHosted";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string BaseUrlEnvironmentVariable = "SELF_HOSTED_BASE_URL";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string ModelEnvironmentVariable = "SELF_HOSTED_MODEL";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string ApiKeyEnvironmentVariable = "SELF_HOSTED_API_KEY";

    /// <summary>
    /// Base address of the server. circuit's <c>s1proto</c> listens on 8901 by default.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "http://localhost:8901";

    /// <summary>The evaluation path. The same contract as the hosted API, so the same path.</summary>
    [Required]
    public string EvaluationPath { get; set; } = "/v1/systemone";

    /// <summary>
    /// The model name sent in the request's <c>model</c> field, e.g. <c>circuit-8b</c> or
    /// <c>circuit-1.7b</c>. Which weights actually answer is decided by the server's own
    /// <c>S1_MODEL</c> setting; this is what gets recorded on the decision.
    /// </summary>
    public string Model { get; set; } = "circuit-8b";

    /// <summary>
    /// Bearer token. The server requires the header to be present but does not care about the value
    /// unless it was deployed with one, hence the placeholder default.
    /// </summary>
    public string? ApiKey { get; set; } = "x";

    /// <summary>
    /// Overall timeout for one evaluation. Generous because a cold model load is slow, even though a
    /// warm single call answers in well under a second.
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>How many times a transient failure is retried before giving up.</summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Administrator override allowing a <see cref="BaseUrl"/> that is not loopback or private.
    /// <para>
    /// Off by default, for the same reason as local mode: a self-hosted model exists so restricted
    /// ticket data stays inside the network, and a typo in a hostname should not quietly undo that.
    /// </para>
    /// </summary>
    public bool AllowPublicEndpoint { get; set; }

    /// <summary>True when enough is configured to attempt a call.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
