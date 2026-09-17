using FluentAssertions;
using JevTicketRouter.Infrastructure.Decisions.Local;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers the rule that keeps local mode local. This is the security boundary of the whole
/// local-first design: if it lets a public address through, restricted ticket data leaves the
/// network without anyone being told.
/// </summary>
public sealed class LocalEndpointGuardTests
{
    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("http://[::1]:8000/v1")]
    [InlineData("https://localhost:8443/v1")]
    [InlineData("http://host.docker.internal:11434/v1")]
    public void Inspect_AllowsLoopback(string url)
    {
        LocalEndpointGuard.Inspect(url, allowPublicEndpoint: false).IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://10.0.0.5:8000/v1")]            // RFC 1918 /8
    [InlineData("http://172.16.4.2:8000/v1")]          // RFC 1918 /12, low end
    [InlineData("http://172.31.255.254:8000/v1")]      // RFC 1918 /12, high end
    [InlineData("http://192.168.1.50:11434/v1")]       // RFC 1918 /16
    [InlineData("http://169.254.10.10:8000/v1")]       // link-local
    [InlineData("http://100.64.0.1:8000/v1")]          // carrier-grade NAT
    [InlineData("http://[fd00::1]:8000/v1")]           // IPv6 unique local
    public void Inspect_AllowsPrivateAddresses(string url)
    {
        LocalEndpointGuard.Inspect(url, allowPublicEndpoint: false).IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://ollama:11434/v1")]             // single-label container name
    [InlineData("http://model-gateway:8000/v1")]
    [InlineData("http://ai.internal:8000/v1")]
    [InlineData("http://gateway.corp.local:8000/v1")]
    [InlineData("http://llm.home.arpa:8000/v1")]
    public void Inspect_AllowsInternalNames(string url)
    {
        LocalEndpointGuard.Inspect(url, allowPublicEndpoint: false).IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://api.typesafe.ai/v1")]
    [InlineData("http://8.8.8.8:8000/v1")]
    [InlineData("http://172.32.0.1:8000/v1")]          // just outside RFC 1918 /12
    [InlineData("http://172.15.0.1:8000/v1")]          // just below RFC 1918 /12
    [InlineData("https://someone-elses-gateway.com/v1")]
    public void Inspect_RefusesPublicEndpointsByDefault(string url)
    {
        var verdict = LocalEndpointGuard.Inspect(url, allowPublicEndpoint: false);

        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not a loopback or private address");
    }

    [Fact]
    public void Inspect_AllowsAPublicEndpointOnlyWithTheAdministratorOverride()
    {
        const string url = "https://models.example.com/v1";

        LocalEndpointGuard.Inspect(url, allowPublicEndpoint: false).IsAllowed.Should().BeFalse();

        var overridden = LocalEndpointGuard.Inspect(url, allowPublicEndpoint: true);
        overridden.IsAllowed.Should().BeTrue();
        overridden.Reason.Should().Contain("administrator override");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Inspect_RefusesAMissingUrl(string? url)
    {
        var verdict = LocalEndpointGuard.Inspect(url, allowPublicEndpoint: true);

        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not configured");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("localhost:11434")]
    public void Inspect_RefusesAMalformedUrl(string url)
    {
        LocalEndpointGuard.Inspect(url, allowPublicEndpoint: true).IsAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("ftp://localhost/v1")]
    [InlineData("file:///etc/passwd")]
    public void Inspect_RefusesANonHttpScheme(string url)
    {
        var verdict = LocalEndpointGuard.Inspect(url, allowPublicEndpoint: true);

        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("http or https");
    }

    [Fact]
    public void Inspect_RefusesAPublicHostEvenWhenItLooksLikeALocalPath()
    {
        // A common way to get this wrong: the path says "local", the host does not.
        var verdict = LocalEndpointGuard.Inspect("https://example.com/localhost/v1", allowPublicEndpoint: false);

        verdict.IsAllowed.Should().BeFalse();
    }
}
