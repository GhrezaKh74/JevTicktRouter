using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Decisions.Local;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers the local OpenAI-compatible engine against a stub handler: the request it builds, the
/// structured-output constraint, and how it treats every kind of bad answer.
/// </summary>
public sealed class LocalDecisionEngineTests
{
    private const string Model = "qwen2.5:7b-instruct";
    private const string LocalApiKey = "local-key-not-a-real-credential";

    [Fact]
    public async Task EvaluateAsync_PostsToTheChatCompletionsPathUnderTheConfiguredBaseUrl()
    {
        var handler = new StubHandler(Success());
        var engine = CreateEngine(handler);

        await engine.EvaluateAsync(Input(), CancellationToken.None);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsoluteUri
            .Should().Be("http://localhost:11434/v1/chat/completions");
    }

    [Fact]
    public async Task EvaluateAsync_SendsTheModelAndAStrictJsonSchema()
    {
        var handler = new StubHandler(Success());

        await CreateEngine(handler).EvaluateAsync(Input(), CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be(Model);
        root.GetProperty("stream").GetBoolean().Should().BeFalse();
        root.GetProperty("temperature").GetDouble().Should().Be(0);

        var format = root.GetProperty("response_format");
        format.GetProperty("type").GetString().Should().Be("json_schema");

        var schema = format.GetProperty("json_schema");
        schema.GetProperty("strict").GetBoolean().Should().BeTrue();
        schema.GetProperty("name").GetString().Should().Be(LocalDecisionSchema.SchemaName);

        // The schema must constrain the labels, not merely ask for an object.
        var categoryEnum = schema
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("category")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToList();

        categoryEnum.Should().BeEquivalentTo(Enum.GetNames<TicketCategory>());
    }

    [Fact]
    public async Task EvaluateAsync_FallsBackToJsonObjectModeWhenStructuredOutputsAreDisabled()
    {
        var handler = new StubHandler(Success());
        var options = Options();
        options.UseStructuredOutputs = false;

        await CreateEngine(handler, options).EvaluateAsync(Input(), CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Body!);
        document.RootElement.GetProperty("response_format").GetProperty("type").GetString()
            .Should().Be("json_object");
    }

    [Fact]
    public async Task EvaluateAsync_SendsTheTicketAndTheRubricAsSeparateMessages()
    {
        var handler = new StubHandler(Success());

        await CreateEngine(handler).EvaluateAsync(Input(), CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Body!);
        var messages = document.RootElement.GetProperty("messages").EnumerateArray().ToList();

        messages.Should().HaveCount(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Contain("reporting portal");
    }

    [Fact]
    public async Task EvaluateAsync_SendsNoAuthorizationHeaderWhenNoKeyIsConfigured()
    {
        // Ollama needs no credential; sending an empty bearer would make it reject the call.
        var handler = new StubHandler(Success());
        var options = Options();
        options.ApiKey = null;

        await CreateEngine(handler, options).EvaluateAsync(Input(), CancellationToken.None);

        handler.Request!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_SendsABearerTokenWhenOneIsConfigured()
    {
        var handler = new StubHandler(Success());

        await CreateEngine(handler).EvaluateAsync(Input(), CancellationToken.None);

        handler.Request!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be(LocalApiKey);
        handler.Body.Should().NotContain(LocalApiKey, "the key belongs in the header, not the body");
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsADecisionTaggedAsLocal()
    {
        var result = await CreateEngine(new StubHandler(Success()))
            .EvaluateAsync(Input(), CancellationToken.None);

        result.Provider.Should().Be(AiProvider.Local);
        result.Category.Should().Be(TicketCategory.AccessRequest);
        result.TargetTeam.Should().Be(TargetTeam.IdentityAccess);
    }

    [Fact]
    public void Constructor_RefusesANonLocalEndpoint()
    {
        var options = Options();
        options.BaseUrl = "https://api.openai.com/v1";

        var act = () => CreateEngine(new StubHandler(Success()), options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a loopback or private address*");
    }

    [Fact]
    public void Constructor_RefusesAMissingModel()
    {
        var options = Options();
        options.Model = null;

        var act = () => CreateEngine(new StubHandler(Success()), options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{LocalAiOptions.ModelEnvironmentVariable}*");
    }

    [Fact]
    public async Task EvaluateAsync_WithAMalformedReply_ThrowsAndNeverInventsAResult()
    {
        var engine = CreateEngine(new StubHandler(Completion("I cannot answer that.")));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.MalformedResponse);
        exception.Provider.Should().Be(AiProvider.Local);
        exception.IsTransient.Should().BeFalse("retrying an unusable answer only burns time");
    }

    [Fact]
    public async Task EvaluateAsync_WithAnInventedLabel_Throws()
    {
        var engine = CreateEngine(new StubHandler(Completion(
            Decision().Replace("AccessRequest", "BillingProblem", StringComparison.Ordinal))));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        (await act.Should().ThrowAsync<DecisionEngineException>())
            .Which.Kind.Should().Be(DecisionFailureKind.MalformedResponse);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheReplyWasTruncated_SaysSo()
    {
        var engine = CreateEngine(new StubHandler(Completion("{\"category\":", finishReason: "length")));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        (await act.Should().ThrowAsync<DecisionEngineException>())
            .Which.Message.Should().Contain("cut off by the token limit");
    }

    [Fact]
    public async Task EvaluateAsync_WithNoChoices_Throws()
    {
        var engine = CreateEngine(new StubHandler(Json("{\"model\":\"m\",\"choices\":[]}")));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        (await act.Should().ThrowAsync<DecisionEngineException>())
            .Which.Message.Should().Contain("no choices");
    }

    [Fact]
    public async Task EvaluateAsync_WithANonJsonBody_Throws()
    {
        var engine = CreateEngine(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>gateway</html>", Encoding.UTF8, "application/json"),
        }));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        (await act.Should().ThrowAsync<DecisionEngineException>())
            .Which.Kind.Should().Be(DecisionFailureKind.MalformedResponse);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "structured outputs", false)]
    [InlineData(HttpStatusCode.Unauthorized, "credentials", false)]
    [InlineData(HttpStatusCode.NotFound, "no model named", false)]
    [InlineData(HttpStatusCode.InternalServerError, "internal error", true)]
    public async Task EvaluateAsync_MapsEndpointErrors(
        HttpStatusCode status,
        string fragment,
        bool expectedTransient)
    {
        var engine = CreateEngine(new StubHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent("{\"error\":\"details\"}", Encoding.UTF8, "application/json"),
        }));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Provider);
        exception.Message.Should().ContainEquivalentOf(fragment);
        exception.IsTransient.Should().Be(expectedTransient);
        exception.Message.Should().NotContain(LocalApiKey);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheModelIsMissing_NamesTheModelsTheEndpointDoesHave()
    {
        // The usual way this fails is a model name that was never pulled. Listing what the endpoint
        // can actually serve turns a dead end into an obvious fix.
        var handler = new RoutingHandler
        {
            ChatResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"model not found\"}", Encoding.UTF8, "application/json"),
            },
            ModelsResponse = Json(
                "{\"object\":\"list\",\"data\":["
                + "{\"id\":\"gemma3:12b\"},{\"id\":\"qwen3.6:latest\"}]}"),
        };

        var engine = CreateEngine(handler);

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Provider);
        exception.Message.Should().Contain("gemma3:12b").And.Contain("qwen3.6:latest");
        handler.ModelsRequests.Should().Be(1, "the model list is only consulted to explain a failure");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheModelListCannotBeRead_StillReportsTheOriginalFailure()
    {
        // The diagnostic is best-effort: if it fails too, it must not replace the real error.
        var handler = new RoutingHandler
        {
            ChatResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            },
            ModelsResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom", Encoding.UTF8, "text/plain"),
            },
        };

        var act = async () => await CreateEngine(handler).EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Provider);
        exception.Message.Should().Contain("no model named");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheEndpointIsUnreachable_ReportsATransientTransportFailure()
    {
        var engine = CreateEngine(new StubHandler(new HttpRequestException("connection refused")));

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Transport);
        exception.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheCallerCancels_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var engine = CreateEngine(new StubHandler(Success()));

        var act = async () => await engine.EvaluateAsync(Input(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateAsync_MakesExactlyOneRequestAndOnlyToTheConfiguredHost()
    {
        // The core promise of local mode: nothing reaches the internet, and there is no retry,
        // telemetry call, or cloud fallback hiding behind a single evaluation.
        var handler = new StubHandler(Success());

        await CreateEngine(handler).EvaluateAsync(Input(), CancellationToken.None);

        handler.RequestCount.Should().Be(1);
        handler.Hosts.Should().ContainSingle().Which.Should().Be("localhost");
    }

    [Fact]
    public async Task EvaluateAsync_MakesNoFurtherCallAfterAMalformedReply()
    {
        var handler = new StubHandler(Completion("nonsense"));
        var engine = CreateEngine(handler);

        var act = async () => await engine.EvaluateAsync(Input(), CancellationToken.None);

        await act.Should().ThrowAsync<DecisionEngineException>();
        handler.RequestCount.Should().Be(1, "there is no cloud fallback to try next");
    }

    private static TicketInput Input() => new(
        "Access to the reporting portal",
        "A new analyst needs read-only access to the quarterly reporting portal.",
        RequesterRole.InternalSupport);

    private static LocalAiOptions Options() => new()
    {
        BaseUrl = "http://localhost:11434/v1",
        Model = Model,
        ApiKey = LocalApiKey,
    };

    private static LocalOpenAiCompatibleDecisionEngine CreateEngine(
        HttpMessageHandler handler,
        LocalAiOptions? options = null)
    {
        options ??= Options();

        var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };

        return new LocalOpenAiCompatibleDecisionEngine(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<LocalOpenAiCompatibleDecisionEngine>.Instance);
    }

    private static string Decision() =>
        """
        {
          "category": "AccessRequest",
          "category_confidence": 0.91,
          "target_team": "IdentityAccess",
          "target_team_confidence": 0.88,
          "priority": "Medium",
          "priority_confidence": 0.84,
          "contains_sensitive_data_probability": 0.02,
          "needs_human_review_probability": 0.11
        }
        """;

    private static HttpResponseMessage Success() => Completion(Decision());

    private static HttpResponseMessage Completion(string content, string finishReason = "stop")
    {
        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            choices = new[]
            {
                new { message = new { role = "assistant", content }, finish_reason = finishReason },
            },
        });

        return Json(body);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    /// <summary>A stub that answers the chat and the model-list paths differently.</summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        public HttpResponseMessage? ChatResponse { get; init; }

        public HttpResponseMessage? ModelsResponse { get; init; }

        public int ModelsRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/models", StringComparison.Ordinal))
            {
                ModelsRequests++;
                return Task.FromResult(ModelsResponse!);
            }

            return Task.FromResult(ChatResponse!);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public StubHandler(HttpResponseMessage response) => _response = response;

        public StubHandler(Exception exception) => _exception = exception;

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public int RequestCount { get; private set; }

        public List<string> Hosts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestCount++;
            Request = request;

            if (request.RequestUri is { } uri)
            {
                Hosts.Add(uri.Host);
            }

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
