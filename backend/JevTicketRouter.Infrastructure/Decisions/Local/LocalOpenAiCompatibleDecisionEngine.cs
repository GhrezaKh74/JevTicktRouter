using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>
/// Calls a self-hosted OpenAI-compatible endpoint — Ollama, vLLM, llama.cpp, or an internal model
/// gateway — and validates the reply into a <see cref="DecisionResult"/>.
/// <para>
/// This is the engine intended for production on restricted data: nothing leaves the organisation's
/// network. The endpoint is checked against <see cref="LocalEndpointGuard"/> at construction, so a
/// base URL pointing somewhere public fails at startup rather than after the first ticket has
/// already been sent.
/// </para>
/// <para>
/// A malformed reply is always an error. The engine does not retry it, does not fall back to another
/// provider, and never fills in a missing field: a ticket routed on an invented value is worse than
/// a ticket that failed visibly.
/// </para>
/// </summary>
public sealed class LocalOpenAiCompatibleDecisionEngine : IDecisionEngine
{
    /// <summary>The name used to register this client with <c>IHttpClientFactory</c>.</summary>
    public const string HttpClientName = "local-ai";

    private const string CompletionsPath = "chat/completions";

    private readonly HttpClient _httpClient;
    private readonly LocalAiOptions _options;
    private readonly ILogger<LocalOpenAiCompatibleDecisionEngine> _logger;

    /// <summary>Creates the engine.</summary>
    /// <param name="httpClient">A configured client from the factory.</param>
    /// <param name="options">Endpoint settings.</param>
    /// <param name="logger">Structured logger. Never receives the API key or ticket text.</param>
    /// <exception cref="InvalidOperationException">The endpoint is missing or not permitted.</exception>
    public LocalOpenAiCompatibleDecisionEngine(
        HttpClient httpClient,
        IOptions<LocalAiOptions> options,
        ILogger<LocalOpenAiCompatibleDecisionEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var verdict = LocalEndpointGuard.Inspect(_options.BaseUrl, _options.AllowPublicEndpoint);

        if (!verdict.IsAllowed)
        {
            throw new InvalidOperationException(verdict.Reason);
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException(
                $"{LocalAiOptions.ModelEnvironmentVariable} is not configured. Local mode needs a model name.");
        }
    }

    /// <inheritdoc />
    public AiProvider Provider => AiProvider.Local;

    /// <inheritdoc />
    public bool IsLive => true;

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(TicketInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var model = _options.Model ?? string.Empty;

        var payload = new LocalChatRequest
        {
            Model = model,
            Temperature = _options.Temperature,
            Stream = false,
            Messages =
            [
                new LocalChatMessage("system", LocalDecisionPrompt.BuildSystemMessage()),
                new LocalChatMessage("user", LocalDecisionPrompt.BuildUserMessage(input)),
            ],
            ResponseFormat = BuildResponseFormat(),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, CompletionsPath)
        {
            Content = JsonContent.Create(payload, options: LocalAiJson.Options),
        };

        // Many local servers need no credential at all; send one only when configured.
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await BuildProviderFailureAsync(response, cancellationToken).ConfigureAwait(false);
            }

            LocalChatResponse? body;
            try
            {
                body = await response.Content
                    .ReadFromJsonAsync<LocalChatResponse>(LocalAiJson.Options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw Malformed("The local endpoint returned a body that is not valid JSON.", exception);
            }

            var choice = body?.Choices?.FirstOrDefault();

            if (choice is null)
            {
                throw Malformed("The local endpoint returned no choices.");
            }

            if (string.Equals(choice.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                throw Malformed(
                    "The local model's reply was cut off by the token limit before it completed the JSON.");
            }

            var validation = LocalDecisionValidator.Validate(
                choice.Message?.Content,
                string.IsNullOrWhiteSpace(body?.Model) ? model : body.Model);

            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Local model {Model} returned an unusable decision: {Reason}",
                    model,
                    validation.Error);

                throw Malformed(validation.Error);
            }

            _logger.LogDebug("Local model {Model} returned a valid decision.", model);

            return validation.Result;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            // A cancellation that is not ours is the HttpClient timeout elapsing.
            _logger.LogWarning(
                "The local endpoint did not answer within {TimeoutSeconds}s.",
                _options.TimeoutSeconds);

            throw new DecisionEngineException(
                $"The local AI endpoint did not answer within {_options.TimeoutSeconds} seconds.",
                AiProvider.Local,
                DecisionFailureKind.Transport,
                HttpStatusCode.RequestTimeout,
                exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Could not reach the local AI endpoint.");

            throw new DecisionEngineException(
                $"Could not reach the local AI endpoint at {_options.BaseUrl}. "
                    + "If the app runs in a container, 'localhost' means the container itself: use "
                    + "http://host.docker.internal:11434/v1 and start the model server with "
                    + "OLLAMA_HOST=0.0.0.0 so it accepts connections from outside the host.",
                AiProvider.Local,
                DecisionFailureKind.Transport,
                innerException: exception);
        }
    }

    /// <summary>
    /// Asks the server to constrain decoding to the schema. Falls back to plain JSON mode when
    /// structured outputs are switched off, since the reply is validated either way.
    /// </summary>
    private JsonObject? BuildResponseFormat()
    {
        if (!_options.UseStructuredOutputs)
        {
            return new JsonObject { ["type"] = "json_object" };
        }

        return new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject
            {
                ["name"] = LocalDecisionSchema.SchemaName,
                ["strict"] = true,
                ["schema"] = LocalDecisionSchema.Build(),
            },
        };
    }

    private async Task<DecisionEngineException> BuildProviderFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        var body = string.Empty;

        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // Diagnostics only.
        }

        var detail = (int)status switch
        {
            400 => "The local endpoint rejected the request. It may not support structured outputs; "
                + "try setting LocalAi:UseStructuredOutputs to false.",
            401 or 403 => "The local endpoint rejected the configured credentials.",
            404 => $"The local endpoint has no model named '{_options.Model}', or the base URL is wrong. "
                + $"Tried {_options.BaseUrl}/{CompletionsPath}. The base URL must include the OpenAI "
                + "compatibility segment, e.g. http://localhost:11434/v1 rather than "
                + $"http://localhost:11434.{await DescribeAvailableModelsAsync(cancellationToken).ConfigureAwait(false)}",
            >= 500 => "The local endpoint reported an internal error.",
            _ => $"The local endpoint returned an unexpected status ({(int)status}).",
        };

        _logger.LogWarning(
            "Local AI call failed. Status={StatusCode} Body={Body}",
            (int)status,
            Truncate(body, 500));

        return new DecisionEngineException(
            detail,
            AiProvider.Local,
            DecisionFailureKind.Provider,
            status);
    }

    /// <summary>
    /// Best-effort: asks the endpoint which models it can serve, so a "no such model" error names
    /// the real options. Purely diagnostic — any failure here is swallowed, because the caller is
    /// already reporting a different error and this must never replace it.
    /// </summary>
    private async Task<string> DescribeAvailableModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var list = await _httpClient
                .GetFromJsonAsync<LocalModelList>("models", LocalAiJson.Options, timeout.Token)
                .ConfigureAwait(false);

            var names = list?.Data?
                .Select(entry => entry.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Take(20)
                .ToList();

            return names is { Count: > 0 }
                ? $" Models this endpoint can serve: {string.Join(", ", names)}."
                : " The endpoint reported no models at all; pull one with: ollama pull <model>";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or NotSupportedException
                or OperationCanceledException)
        {
            return " Check which models are available with: ollama list";
        }
    }

    private static DecisionEngineException Malformed(string reason, Exception? inner = null) =>
        new(
            $"The local model did not return a usable decision. {reason}",
            AiProvider.Local,
            DecisionFailureKind.MalformedResponse,
            innerException: inner);

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "...");
}

/// <summary>JSON settings for the local endpoint.</summary>
internal static class LocalAiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
