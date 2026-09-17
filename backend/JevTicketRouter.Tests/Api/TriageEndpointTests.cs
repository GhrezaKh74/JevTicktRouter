using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JevTicketRouter.Domain.Redaction;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JevTicketRouter.Tests.Api;

/// <summary>
/// End-to-end tests over the real HTTP pipeline. The factory sets no API key, so the API starts in
/// mock mode exactly as a freshly cloned checkout would.
/// </summary>
public sealed class TriageEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;

    public TriageEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", null);

        _client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Jev:ForceMockMode", "true"))
            .CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Health_ReportsMockProviderWhenNothingIsConfigured()
    {
        var response = await _client.GetAsync("/api/health", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        body.GetProperty("provider").GetString().Should().Be("Mock");
        body.GetProperty("isLive").GetBoolean().Should().BeFalse();
        body.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_NeverLeaksACredentialOrAnEndpoint()
    {
        var raw = await (await _client.GetAsync("/api/health", CancellationToken.None))
            .Content.ReadAsStringAsync(CancellationToken.None);

        raw.Should().NotContain("ApiKey", "the health payload is public to the dashboard");
        raw.Should().NotContain("http://");
    }

    [Fact]
    public async Task Benchmark_RunsTheFictionalCorpusAndReportsMetricsOnly()
    {
        var response = await _client.PostAsync("/api/benchmark", content: null, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var body = JsonDocument.Parse(raw).RootElement;

        body.GetProperty("ticketCount").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("providers").EnumerateArray().Should().NotBeEmpty();

        // The corpus lives server-side and its content must never come back out.
        raw.Should().NotContain("reporting portal");
        raw.Should().NotContain("phishing");
    }

    [Fact]
    public async Task Triage_WithAValidTicket_ReturnsAFullDecision()
    {
        var response = await PostAsync(new
        {
            title = "Access to the reporting portal for a new analyst",
            description = "Our new analyst needs read-only access to the quarterly reporting portal. "
                + "Please provision the account and the standard analyst role.",
            requesterRole = "InternalSupport",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        body.GetProperty("category").GetProperty("value").GetString().Should().Be("AccessRequest");
        body.GetProperty("targetTeam").GetProperty("value").GetString().Should().Be("IdentityAccess");
        body.GetProperty("priority").GetProperty("value").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("routingSummary").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("jev").GetProperty("provider").GetString().Should().Be("Mock");
        body.GetProperty("jev").GetProperty("isLive").GetBoolean().Should().BeFalse();
        body.GetProperty("ticketId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Triage_ReturnsEnumsAsNamesNotNumbers()
    {
        var response = await PostAsync(new
        {
            title = "Printer in the branch is offline",
            description = "The shared printer on the second floor will not come back online after the reboot.",
            requesterRole = "BranchEmployee",
        });

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);

        raw.Should().NotContain("\"value\":0");
        raw.Should().MatchRegex("\"origin\":\"(JevModel|BusinessRule)\"");
    }

    [Fact]
    public async Task Triage_WithASensitiveTicket_RedactsTheTextFromTheResponse()
    {
        const string accountNumber = "6037991234567890";

        var response = await PostAsync(new
        {
            title = "Duplicate charge on my account",
            description = $"My account number is {accountNumber} and my card was charged twice this morning.",
            requesterRole = "Customer",
        });

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var body = JsonDocument.Parse(raw).RootElement;

        body.GetProperty("containsSensitiveData").GetProperty("value").GetBoolean().Should().BeTrue();
        body.GetProperty("jev").GetProperty("stateSummary").GetProperty("redacted").GetBoolean().Should().BeTrue();
        body.GetProperty("jev").GetProperty("stateSummary").GetProperty("description").GetString()
            .Should().Be(SensitiveTextRedactor.RedactedPlaceholder);

        raw.Should().NotContain(accountNumber, "the sensitive value must never reach the client");
    }

    [Fact]
    public async Task Triage_WithASecurityTicket_EscalatesAndExplainsWhy()
    {
        var response = await PostAsync(new
        {
            title = "Suspicious email asking staff to confirm their password",
            description = "Several colleagues received a suspicious phishing email linking to a fake login page. "
                + "Please investigate urgently.",
            requesterRole = "InternalSupport",
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        body.GetProperty("needsHumanReview").GetProperty("value").GetBoolean().Should().BeTrue();
        body.GetProperty("needsHumanReview").GetProperty("origin").GetString().Should().Be("BusinessRule");

        var ruleIds = body.GetProperty("appliedRules").EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetString())
            .ToList();

        ruleIds.Should().Contain("SECURITY_OR_CRITICAL_ESCALATION");
    }

    [Fact]
    public async Task Triage_WithAPersianTicket_IsAccepted()
    {
        var response = await PostAsync(new
        {
            title = "خطا هنگام ثبت تراکنش در سامانه شعبه",
            description = "از امروز صبح هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شوم و صفحه لود نمی‌شود.",
            requesterRole = "BranchEmployee",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        body.GetProperty("category").GetProperty("value").GetString().Should().Be("TechnicalIssue");
    }

    [Fact]
    public async Task Triage_WithAnInvalidTicket_ReturnsValidationProblemDetails()
    {
        var response = await PostAsync(new { title = "x", description = "short", requesterRole = "Customer" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var errors = body.GetProperty("errors");

        errors.TryGetProperty("Title", out _).Should().BeTrue();
        errors.TryGetProperty("Description", out _).Should().BeTrue();
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Triage_WithAnUnknownRole_IsRejected()
    {
        var response = await PostAsync(new
        {
            title = "A perfectly valid title",
            description = "A perfectly valid description that is long enough to pass validation.",
            requesterRole = "Chief Executive",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Triage_WithMalformedJson_ReturnsBadRequest()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(
            "/api/tickets/triage",
            content,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpenApiDocument_IsServedAndDescribesTheTriageEndpoint()
    {
        var response = await _client.GetAsync("/openapi/v1.json", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        document.GetProperty("info").GetProperty("title").GetString().Should().Be("JevTicketRouter API");
        document.GetProperty("paths").TryGetProperty("/api/tickets/triage", out _).Should().BeTrue();
    }

    private Task<HttpResponseMessage> PostAsync(object payload) =>
        _client.PostAsJsonAsync("/api/tickets/triage", payload, CancellationToken.None);
}
