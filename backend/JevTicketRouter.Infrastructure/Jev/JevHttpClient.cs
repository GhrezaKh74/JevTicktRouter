using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Infrastructure.Jev;

/// <summary>
/// Calls <c>POST /v1/systemone</c> on the TypeSafe API over a pooled <see cref="HttpClient"/> from
/// <c>IHttpClientFactory</c>.
/// <para>
/// TypeSafe publishes Python and JavaScript SDKs but no .NET SDK, so this project calls the
/// documented HTTP API directly. Retries for <c>429</c> and <c>529</c> are handled by the resilience
/// handler configured in <see cref="DependencyInjection"/>; this class is responsible for
/// authentication, serialisation, and turning failures into a <see cref="JevClientException"/>
/// that never carries the API key.
/// </para>
/// </summary>
public sealed class JevHttpClient : IJevClient
{
    /// <summary>The name used to register this client with <c>IHttpClientFactory</c>.</summary>
    public const string HttpClientName = "typesafe-jev";

    private readonly HttpClient _httpClient;
    private readonly JevOptions _options;
    private readonly ILogger<JevHttpClient> _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">A configured client from the factory.</param>
    /// <param name="options">Connection settings.</param>
    /// <param name="logger">Structured logger. Never receives the API key or ticket text.</param>
    public JevHttpClient(HttpClient httpClient, IOptions<JevOptions> options, ILogger<JevHttpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsLive => true;

    /// <inheritdoc />
    public async Task<JevSystemOneResponse> EvaluateAsync(
        JevSystemOneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.EvaluationPath)
        {
            Content = JsonContent.Create(request, options: JevJsonSerialization.Options),
        };

        // Set per-request rather than on the shared client so a rotated key takes effect immediately.
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
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
            _logger.LogWarning("Jev evaluation timed out after {TimeoutSeconds}s.", _options.TimeoutSeconds);
            throw new JevClientException(
                $"The Jev evaluation timed out after {_options.TimeoutSeconds} seconds.",
                HttpStatusCode.RequestTimeout,
                exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Jev evaluation failed to reach the TypeSafe API.");
            throw new JevClientException(
                "Could not reach the TypeSafe API.",
                statusCode: null,
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await BuildFailureAsync(response, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var payload = await response.Content
                    .ReadFromJsonAsync<JevSystemOneResponse>(JevJsonSerialization.Options, cancellationToken)
                    .ConfigureAwait(false);

                if (payload is null)
                {
                    throw new JevClientException("The TypeSafe API returned an empty response body.");
                }

                _logger.LogDebug(
                    "Jev evaluation succeeded. Model={Model} InputTokens={InputTokens} Answers={AnswerCount}",
                    payload.Model,
                    payload.Usage?.InputTokens ?? 0,
                    payload.Answers.Count);

                return payload;
            }
            catch (JsonException exception)
            {
                throw new JevClientException(
                    "The TypeSafe API returned a response that could not be parsed.",
                    response.StatusCode,
                    exception);
            }
        }
    }

    private async Task<JevClientException> BuildFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;

        // Read a bounded amount of the error body for diagnostics. TypeSafe error bodies describe the
        // offending field; they never echo credentials, but the body is still kept out of the message
        // returned to the client for anything other than a validation failure.
        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // Diagnostics only; the status code below is what matters.
        }

        var message = (int)status switch
        {
            401 => "The TypeSafe API rejected the configured credentials.",
            403 => "The configured TypeSafe credentials are not permitted to use this model.",
            422 => "The TypeSafe API rejected the triage request as invalid.",
            429 => "The TypeSafe API rate limit was exceeded. Please retry shortly.",
            529 => "The TypeSafe API is temporarily overloaded. Please retry shortly.",
            >= 500 => "The TypeSafe API reported an internal error.",
            _ => $"The TypeSafe API returned an unexpected status ({(int)status}).",
        };

        _logger.LogWarning(
            "Jev evaluation failed. Status={StatusCode} Body={Body}",
            (int)status,
            Truncate(body, 500));

        return new JevClientException(message, status);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "...");
}
