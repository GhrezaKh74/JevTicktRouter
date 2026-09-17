using System.Text.RegularExpressions;

namespace JevTicketRouter.Domain.Redaction;

/// <summary>
/// Masks text that must not reach structured logs or the client's developer-details panel.
/// <para>
/// Two layers are applied. <see cref="Redact"/> removes a whole field once the ticket has been
/// flagged as sensitive. <see cref="MaskPatterns"/> is a defence-in-depth pass that masks
/// obviously-secret shapes (long digit runs, emails, bearer tokens) even on tickets that were not
/// flagged, so an unflagged ticket still cannot leak a credential into the log stream.
/// </para>
/// </summary>
public static partial class SensitiveTextRedactor
{
    /// <summary>The placeholder substituted for a fully redacted value.</summary>
    public const string RedactedPlaceholder = "[REDACTED: sensitive data detected]";

    /// <summary>The placeholder substituted for an individual masked token.</summary>
    public const string MaskedToken = "[MASKED]";

    /// <summary>
    /// Returns <see cref="RedactedPlaceholder"/> when the ticket is sensitive, otherwise the text
    /// with <see cref="MaskPatterns"/> applied.
    /// </summary>
    /// <param name="text">The text to protect.</param>
    /// <param name="containsSensitiveData">Whether the ticket was flagged as sensitive.</param>
    public static string Redact(string? text, bool containsSensitiveData)
    {
        if (containsSensitiveData)
        {
            return RedactedPlaceholder;
        }

        return string.IsNullOrWhiteSpace(text) ? string.Empty : MaskPatterns(text);
    }

    /// <summary>
    /// Masks secret-looking substrings regardless of the sensitivity flag. Deliberately conservative:
    /// it targets shapes that are never useful in a log line.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    public static string MaskPatterns(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var masked = LongDigitRunRegex().Replace(text, MaskedToken);
        masked = EmailRegex().Replace(masked, MaskedToken);
        masked = BearerTokenRegex().Replace(masked, $"Bearer {MaskedToken}");
        masked = SecretAssignmentRegex().Replace(masked, $"$1: {MaskedToken}");

        return masked;
    }

    /// <summary>
    /// Truncates text to a bounded length so a single ticket cannot flood the log sink.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum number of characters to keep.</param>
    public static string Truncate(string? text, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
    }

    // Nine or more consecutive digits: account, card, and national-id shapes.
    [GeneratedRegex(@"\d[\d\s-]{7,}\d", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex LongDigitRunRegex();

    [GeneratedRegex(
        @"[\w.+-]+@[\w-]+\.[\w.-]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(
        @"Bearer\s+[A-Za-z0-9._\-]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex BearerTokenRegex();

    // key: value / password = value, for the obvious secret-bearing keys.
    [GeneratedRegex(
        @"\b(password|passwd|api[_-]?key|token|secret|otp|pin)\b\s*[:=]\s*\S+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex SecretAssignmentRegex();
}
