using System.ComponentModel.DataAnnotations;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>
/// Settings for the self-hosted OpenAI-compatible endpoint, bound from the <c>LocalAi</c> section.
/// <para>
/// Works with anything that speaks the OpenAI chat-completions shape: Ollama, vLLM, llama.cpp's
/// server, LM Studio, or an internal model gateway.
/// </para>
/// </summary>
public sealed class LocalAiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "LocalAi";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string BaseUrlEnvironmentVariable = "LOCAL_AI_BASE_URL";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string ModelEnvironmentVariable = "LOCAL_AI_MODEL";

    /// <summary>Environment variable read when no configuration value is present.</summary>
    public const string ApiKeyEnvironmentVariable = "LOCAL_AI_API_KEY";

    /// <summary>
    /// Base URL of the OpenAI-compatible API, including the version segment.
    /// Ollama: <c>http://localhost:11434/v1</c>. vLLM: <c>http://localhost:8000/v1</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>The model name to request, e.g. <c>qwen2.5:7b-instruct</c> or <c>llama3.1:8b</c>.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// Bearer token, if the endpoint requires one. Ollama does not; vLLM and most gateways do.
    /// Supplied through User Secrets or the environment, never a committed file.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Overall timeout for a single evaluation. Local models are slower than a hosted API.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Whether to send <c>response_format: json_schema</c> with <c>strict: true</c>. Supported by
    /// vLLM, recent Ollama, and most gateways. Turn off for a server that rejects the field; the
    /// engine then falls back to <c>json_object</c> and still validates the body itself.
    /// </summary>
    public bool UseStructuredOutputs { get; set; } = true;

    /// <summary>
    /// Sampling temperature. Defaults to 0 because this is a classification task and reproducibility
    /// matters more than variety.
    /// </summary>
    [Range(0d, 2d)]
    public double Temperature { get; set; }

    /// <summary>
    /// Administrator override allowing a <see cref="BaseUrl"/> that is not loopback or private.
    /// <para>
    /// Off by default. Local mode exists to keep restricted data inside the network, so a non-local
    /// endpoint has to be an explicit, deliberate decision rather than the result of a typo.
    /// </para>
    /// </summary>
    public bool AllowPublicEndpoint { get; set; }

    /// <summary>True when enough is configured to attempt a call.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}
