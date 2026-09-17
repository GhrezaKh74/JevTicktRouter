using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Infrastructure.Jev;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Tests.Infrastructure;

/// <summary>
/// Covers the live HTTP client against a stub handler: the documented request shape, the
/// Authorization header, and the mapping of every documented error status.
/// </summary>
public sealed class JevHttpClientTests
{
    private const string TestApiKey = "test-key-not-a-real-credential";

    [Fact]
    public async Task EvaluateAsync_PostsToTheDocumentedEndpointWithBearerAuth()
    {
        var handler = new StubHandler(SuccessResponse());
        var client = CreateClient(handler);

        await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsoluteUri.Should().Be("https://api.typesafe.ai/v1/systemone");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be(TestApiKey);
    }

    [Fact]
    public async Task EvaluateAsync_SerialisesTheRequestInTheDocumentedShape()
    {
        var handler = new StubHandler(SuccessResponse());
        var client = CreateClient(handler);

        await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be("jev-latest");
        root.GetProperty("state").ValueKind.Should().Be(JsonValueKind.Object);

        var questions = root.GetProperty("questions");
        questions.GetProperty(JevTriageQuestions.CategoryQuestionId).GetProperty("type").GetString()
            .Should().Be("choice");
        questions.GetProperty(JevTriageQuestions.PriorityQuestionId).GetProperty("type").GetString()
            .Should().Be("score");
        questions.GetProperty(JevTriageQuestions.SensitiveDataQuestionId).GetProperty("type").GetString()
            .Should().Be("noul");

        // Score criteria must be an ordered array; choice criteria an option map.
        questions.GetProperty(JevTriageQuestions.PriorityQuestionId).GetProperty("criteria").ValueKind
            .Should().Be(JsonValueKind.Array);
        questions.GetProperty(JevTriageQuestions.CategoryQuestionId).GetProperty("criteria").ValueKind
            .Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task EvaluateAsync_NeverPutsTheApiKeyInTheBody()
    {
        var handler = new StubHandler(SuccessResponse());
        var client = CreateClient(handler);

        await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        handler.Body.Should().NotContain(TestApiKey);
    }

    [Fact]
    public async Task EvaluateAsync_DeserialisesADocumentedResponse()
    {
        var client = CreateClient(new StubHandler(SuccessResponse()));

        var response = await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        response.Model.Should().Be("jev-1.13.0");
        response.Answers.Should().ContainKey(JevTriageQuestions.CategoryQuestionId);
        response.Answers[JevTriageQuestions.CategoryQuestionId].Confidence.Should().Be(0.91);
        response.Answers[JevTriageQuestions.SensitiveDataQuestionId].Noul.Should().Be(0.03);
        response.Usage!.InputTokens.Should().Be(312);
    }

    [Fact]
    public void IsLive_IsTrue()
    {
        CreateClient(new StubHandler(SuccessResponse())).IsLive.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "credentials", false)]
    [InlineData(HttpStatusCode.UnprocessableContent, "invalid", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate limit", true)]
    [InlineData((HttpStatusCode)529, "overloaded", true)]
    [InlineData(HttpStatusCode.InternalServerError, "internal error", true)]
    public async Task EvaluateAsync_MapsDocumentedErrorStatuses(
        HttpStatusCode status,
        string expectedFragment,
        bool expectedTransient)
    {
        var handler = new StubHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent("{\"error\":\"details\"}", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var act = async () => await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<JevClientException>()).Which;
        exception.StatusCode.Should().Be(status);
        exception.Message.Should().ContainEquivalentOf(expectedFragment);
        exception.IsTransient.Should().Be(expectedTransient);
        exception.Message.Should().NotContain(TestApiKey);
    }

    [Fact]
    public async Task EvaluateAsync_WithUnparseableBody_Throws()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var act = async () => await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<JevClientException>().WithMessage("*could not be parsed*");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheTransportFails_ThrowsATransientError()
    {
        var client = CreateClient(new StubHandler(new HttpRequestException("connection refused")));

        var act = async () => await client.EvaluateAsync(BuildRequest(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<JevClientException>()).Which;
        exception.StatusCode.Should().BeNull();
        exception.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheCallerCancels_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var client = CreateClient(new StubHandler(SuccessResponse()));

        var act = async () => await client.EvaluateAsync(BuildRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static JevTicketRouter.Application.Jev.Contracts.JevSystemOneRequest BuildRequest() =>
        JevTriageQuestions.BuildRequest(
            new SupportTicket("A title", "A description long enough to be realistic.", RequesterRole.Customer),
            "jev-latest");

    private static JevHttpClient CreateClient(StubHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.typesafe.ai") };

        var options = Options.Create(new JevOptions { ApiKey = TestApiKey });

        return new JevHttpClient(httpClient, options, NullLogger<JevHttpClient>.Instance);
    }

    private static HttpResponseMessage SuccessResponse()
    {
        var payload = $$"""
        {
          "model": "jev-1.13.0",
          "answers": {
            "{{JevTriageQuestions.CategoryQuestionId}}": {
              "type": "choice",
              "choice": "AccessRequest",
              "probabilities": { "AccessRequest": 0.91, "TechnicalIssue": 0.09 },
              "confidence": 0.91
            },
            "{{JevTriageQuestions.TargetTeamQuestionId}}": {
              "type": "choice",
              "choice": "IdentityAccess",
              "probabilities": { "IdentityAccess": 0.88, "Security": 0.12 },
              "confidence": 0.88
            },
            "{{JevTriageQuestions.PriorityQuestionId}}": {
              "type": "score",
              "score": 1.03,
              "legend": { "0": "Low", "1": "Medium", "2": "High", "3": "Critical" },
              "probabilities": { "0": 0.1, "1": 0.8, "2": 0.1, "3": 0.0 },
              "confidence": 0.84
            },
            "{{JevTriageQuestions.SensitiveDataQuestionId}}": { "type": "noul", "noul": 0.03 },
            "{{JevTriageQuestions.HumanReviewQuestionId}}": { "type": "noul", "noul": 0.07 }
          },
          "usage": { "input_tokens": 312, "output_tokens": 48 }
        }
        """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public StubHandler(HttpResponseMessage response) => _response = response;

        public StubHandler(Exception exception) => _exception = exception;

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Request = request;

            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            return _response!;
        }
    }
}
