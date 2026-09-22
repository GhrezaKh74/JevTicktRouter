using FluentAssertions;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Infrastructure.Decisions;
using JevTicketRouter.Infrastructure.Decisions.Local;
using JevTicketRouter.Infrastructure.Decisions.SelfHosted;
using JevTicketRouter.Infrastructure.Jev;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers which engine runs for a given configuration.
/// <para>
/// The guiding rule is that the service always starts: a provider that cannot run degrades to Mock
/// with a stated reason. The single exception is a local endpoint that fails the security check,
/// which must fail loudly rather than quietly route restricted tickets elsewhere.
/// </para>
/// </summary>
public sealed class DecisionEngineResolverTests
{
    [Fact]
    public void Resolve_JevWithAKey_SelectsJev()
    {
        var selection = Resolve("Jev", jev: WithKey());

        selection.Provider.Should().Be(AiProvider.Jev);
        selection.FellBack.Should().BeFalse();
    }

    [Fact]
    public void Resolve_JevWithoutAKey_FallsBackToMock()
    {
        var selection = Resolve("Jev", jev: new JevOptions());

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Requested.Should().Be(AiProvider.Jev);
        selection.FellBack.Should().BeTrue();
        selection.Reason.Should().Contain(JevOptions.ApiKeyEnvironmentVariable);
    }

    [Fact]
    public void Resolve_JevWithForcedMockMode_FallsBackToMock()
    {
        var jev = WithKey();
        jev.ForceMockMode = true;

        var selection = Resolve("Jev", jev: jev);

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Reason.Should().Contain("ForceMockMode");
    }

    [Fact]
    public void Resolve_LocalWithAModelAndALoopbackEndpoint_SelectsLocal()
    {
        var selection = Resolve("Local", local: WithLocalModel());

        selection.Provider.Should().Be(AiProvider.Local);
        selection.FellBack.Should().BeFalse();
        selection.Reason.Should().Contain("localhost");
    }

    [Fact]
    public void Resolve_LocalWithoutAModel_FallsBackToMock()
    {
        var local = WithLocalModel();
        local.Model = null;

        var selection = Resolve("Local", local: local);

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Reason.Should().Contain(LocalAiOptions.ModelEnvironmentVariable);
    }

    [Fact]
    public void Resolve_LocalPointingAtAPublicEndpoint_Throws()
    {
        // Falling back here would send restricted tickets somewhere the operator never approved,
        // so this is deliberately fatal rather than a degradation.
        var local = WithLocalModel();
        local.BaseUrl = "https://api.openai.com/v1";

        var act = () => Resolve("Local", local: local);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a loopback or private address*");
    }

    [Fact]
    public void Resolve_LocalPointingAtAPublicEndpointWithTheOverride_SelectsLocal()
    {
        var local = WithLocalModel();
        local.BaseUrl = "https://models.corp.example.com/v1";
        local.AllowPublicEndpoint = true;

        Resolve("Local", local: local).Provider.Should().Be(AiProvider.Local);
    }

    [Fact]
    public void Resolve_SelfHostedWithALoopbackEndpoint_SelectsSelfHosted()
    {
        var selection = Resolve("SelfHosted", selfHosted: SelfHosted());

        selection.Provider.Should().Be(AiProvider.SelfHosted);
        selection.FellBack.Should().BeFalse();
        selection.Model.Should().Be("circuit-8b");
        selection.Reason.Should().Contain("8901");
    }

    [Fact]
    public void Resolve_SelfHostedPointingAtAPublicEndpoint_Throws()
    {
        // Running the weights yourself is pointless if the address is someone else's server, so
        // this fails loudly rather than degrading to another provider.
        var options = SelfHosted();
        options.BaseUrl = "https://someone-elses-host.com";

        var act = () => Resolve("SelfHosted", selfHosted: options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a loopback or private address*");
    }

    [Fact]
    public void Resolve_SelfHostedPointingAtAPublicEndpointWithTheOverride_Selects()
    {
        var options = SelfHosted();
        options.BaseUrl = "https://systemone.corp.example.com";
        options.AllowPublicEndpoint = true;

        Resolve("SelfHosted", selfHosted: options).Provider.Should().Be(AiProvider.SelfHosted);
    }

    [Fact]
    public void Resolve_SelfHostedWithoutABaseUrl_FallsBackToMock()
    {
        var options = SelfHosted();
        options.BaseUrl = string.Empty;

        var selection = Resolve("SelfHosted", selfHosted: options);

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Reason.Should().Contain(SelfHostedOptions.BaseUrlEnvironmentVariable);
    }

    [Fact]
    public void Resolve_SelfHostedDoesNotNeedATypeSafeKey()
    {
        // The whole point is that no hosted credential is involved.
        Resolve("SelfHosted", jev: new JevOptions(), selfHosted: SelfHosted())
            .Provider.Should().Be(AiProvider.SelfHosted);
    }

    [Fact]
    public void Resolve_SelfHostedDefaultsToCircuitsOwnPort()
    {
        // circuit's s1proto server listens on 8901; the default should not need overriding.
        new SelfHostedOptions().BaseUrl.Should().Contain("8901");
        new SelfHostedOptions().EvaluationPath.Should().Be("/v1/systemone");
    }

    [Fact]
    public void Resolve_MockRequestedExplicitly_SelectsMock()
    {
        var selection = Resolve("Mock", jev: WithKey(), local: WithLocalModel());

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Requested.Should().Be(AiProvider.Mock);
        selection.FellBack.Should().BeFalse();
        selection.Reason.Should().Contain("no network request");
    }

    [Theory]
    [InlineData("jev")]
    [InlineData("JEV")]
    [InlineData("  Local  ")]
    [InlineData("mOcK")]
    public void Resolve_IgnoresCaseAndSurroundingWhitespace(string provider)
    {
        var act = () => Resolve(provider, jev: WithKey(), local: WithLocalModel());

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithAnUnrecognisedProvider_FallsBackToMockRatherThanCrashing(string provider)
    {
        var selection = Resolve(provider, jev: WithKey());

        selection.Provider.Should().Be(AiProvider.Mock);
        selection.Reason.Should().Contain(AiProviderOptions.ProviderEnvironmentVariable);
    }

    [Fact]
    public void Resolve_NeverPutsTheApiKeyInTheReason()
    {
        const string key = "secret-key-value-not-real";
        var jev = new JevOptions { ApiKey = key };

        Resolve("Jev", jev: jev).Reason.Should().NotContain(key);
    }

    [Fact]
    public void Resolve_LocalSelectionDoesNotLeakTheLocalApiKey()
    {
        const string key = "local-key-not-real";
        var local = WithLocalModel();
        local.ApiKey = key;

        Resolve("Local", local: local).Reason.Should().NotContain(key);
    }

    private static DecisionEngineSelection Resolve(
        string provider,
        JevOptions? jev = null,
        LocalAiOptions? local = null,
        SelfHostedOptions? selfHosted = null) =>
        DecisionEngineResolver.Resolve(
            new AiProviderOptions { Provider = provider },
            jev ?? new JevOptions(),
            local ?? new LocalAiOptions(),
            selfHostedOptions: selfHosted ?? new SelfHostedOptions());

    private static SelfHostedOptions SelfHosted() => new()
    {
        BaseUrl = "http://localhost:8901",
        Model = "circuit-8b",
    };

    private static JevOptions WithKey() => new() { ApiKey = "test-key-not-a-real-credential" };

    private static LocalAiOptions WithLocalModel() => new()
    {
        BaseUrl = "http://localhost:11434/v1",
        Model = "qwen2.5:7b-instruct",
    };
}
