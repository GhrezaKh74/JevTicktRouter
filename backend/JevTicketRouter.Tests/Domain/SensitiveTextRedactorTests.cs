using FluentAssertions;
using JevTicketRouter.Domain.Redaction;

namespace JevTicketRouter.Tests.Domain;

/// <summary>Covers the masking that keeps ticket text out of logs and out of the API response.</summary>
public sealed class SensitiveTextRedactorTests
{
    [Fact]
    public void Redact_WhenFlaggedSensitive_ReplacesTheWholeValue()
    {
        var result = SensitiveTextRedactor.Redact("My card number is 4111 1111 1111 1111", true);

        result.Should().Be(SensitiveTextRedactor.RedactedPlaceholder);
        result.Should().NotContain("4111");
    }

    [Fact]
    public void Redact_WhenNotFlagged_KeepsOrdinaryText()
    {
        const string text = "The reporting portal shows an error when I save a record.";

        SensitiveTextRedactor.Redact(text, false).Should().Be(text);
    }

    [Fact]
    public void Redact_WhenNotFlagged_StillMasksSecretLookingContent()
    {
        var result = SensitiveTextRedactor.Redact("Please reset it, my account number is 6037991234567890", false);

        result.Should().NotContain("6037991234567890");
        result.Should().Contain(SensitiveTextRedactor.MaskedToken);
    }

    [Fact]
    public void Redact_WithNullOrWhitespace_ReturnsEmpty()
    {
        SensitiveTextRedactor.Redact(null, false).Should().BeEmpty();
        SensitiveTextRedactor.Redact("   ", false).Should().BeEmpty();
    }

    [Fact]
    public void Redact_WithNullButFlagged_StillRedacts()
    {
        SensitiveTextRedactor.Redact(null, true).Should().Be(SensitiveTextRedactor.RedactedPlaceholder);
    }

    [Theory]
    [InlineData("Card 4111111111111111 was charged twice")]
    [InlineData("National id 0012345678 please verify")]
    [InlineData("Call me on 0912 345 6789")]
    public void MaskPatterns_MasksLongDigitRuns(string text)
    {
        SensitiveTextRedactor.MaskPatterns(text).Should().Contain(SensitiveTextRedactor.MaskedToken);
    }

    [Fact]
    public void MaskPatterns_MasksEmailAddresses()
    {
        var result = SensitiveTextRedactor.MaskPatterns("Contact me at sara.ahmadi@example.com for details");

        result.Should().NotContain("sara.ahmadi@example.com");
        result.Should().Contain(SensitiveTextRedactor.MaskedToken);
    }

    [Fact]
    public void MaskPatterns_MasksBearerTokensButKeepsTheScheme()
    {
        var result = SensitiveTextRedactor.MaskPatterns("Authorization: Bearer abc.def-123_XYZ");

        result.Should().NotContain("abc.def-123_XYZ");
        result.Should().Contain($"Bearer {SensitiveTextRedactor.MaskedToken}");
    }

    [Theory]
    [InlineData("password: hunter2")]
    [InlineData("api_key = sk-test-value")]
    [InlineData("OTP: 998877")]
    [InlineData("token=abcdefgh")]
    public void MaskPatterns_MasksSecretAssignments(string text)
    {
        SensitiveTextRedactor.MaskPatterns(text).Should().Contain(SensitiveTextRedactor.MaskedToken);
    }

    [Fact]
    public void MaskPatterns_LeavesShortNumbersAlone()
    {
        const string text = "It failed 3 times in branch 42 today.";

        SensitiveTextRedactor.MaskPatterns(text).Should().Be(text);
    }

    [Fact]
    public void MaskPatterns_LeavesPersianProseAlone()
    {
        const string text = "سامانه شعبه هنگام ثبت تراکنش خطا می‌دهد.";

        SensitiveTextRedactor.MaskPatterns(text).Should().Be(text);
    }

    [Fact]
    public void Truncate_LongerThanLimit_AppendsEllipsis()
    {
        SensitiveTextRedactor.Truncate("abcdefghij", 4).Should().Be("abcd...");
    }

    [Fact]
    public void Truncate_ShorterThanLimit_IsUnchanged()
    {
        SensitiveTextRedactor.Truncate("abc", 10).Should().Be("abc");
    }

    [Fact]
    public void Truncate_WithNonPositiveLimit_Throws()
    {
        var act = () => SensitiveTextRedactor.Truncate("abc", 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
