using System.Text.RegularExpressions;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Infrastructure.Jev;

/// <summary>
/// Keyword-driven signal extraction backing <see cref="MockJevClient"/>. Recognises both Persian and
/// English vocabulary so mock mode behaves sensibly for either language.
/// </summary>
internal sealed partial class TicketSignals
{
    private const double Prior = 0.35;

    /// <summary>The highest <see cref="TicketPriority"/> level index.</summary>
    private const int MaxPriorityLevel = 3;

    /// <summary>Standard deviation of the priority kernel, in levels. See <see cref="BuildPriorityWeights"/>.</summary>
    private const double PriorityKernelWidth = 0.45;

    /// <summary>
    /// How strongly the priority kernel's centre is pulled to the nearest whole level, from 0 (snap
    /// exactly onto the level) to 1 (no pull). See <see cref="BuildPriorityWeights"/>.
    /// </summary>
    private const double PriorityCentrePull = 0.6;

    private TicketSignals(
        IReadOnlyDictionary<string, double> categoryWeights,
        IReadOnlyDictionary<string, double> teamWeights,
        IReadOnlyList<double> priorityWeights,
        double sensitiveDataProbability,
        double humanReviewProbability)
    {
        CategoryWeights = categoryWeights;
        TeamWeights = teamWeights;
        PriorityWeights = priorityWeights;
        SensitiveDataProbability = sensitiveDataProbability;
        HumanReviewProbability = humanReviewProbability;
    }

    public IReadOnlyDictionary<string, double> CategoryWeights { get; }

    public IReadOnlyDictionary<string, double> TeamWeights { get; }

    public IReadOnlyList<double> PriorityWeights { get; }

    public double SensitiveDataProbability { get; }

    public double HumanReviewProbability { get; }

    public static TicketSignals Extract(string text)
    {
        var security = Count(text, SecurityTerms);
        var access = Count(text, AccessTerms);
        var technical = Count(text, TechnicalTerms);
        var inquiry = Count(text, InquiryTerms);
        var infrastructure = Count(text, InfrastructureTerms);
        var business = Count(text, BusinessTerms);
        var urgency = Count(text, UrgencyTerms);
        var outage = Count(text, OutageTerms);

        var categoryWeights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [nameof(TicketCategory.TechnicalIssue)] = Prior + (technical * 1.4) + (infrastructure * 0.8),
            [nameof(TicketCategory.ServiceInquiry)] = Prior + (inquiry * 1.4),
            [nameof(TicketCategory.AccessRequest)] = Prior + (access * 1.6),
            [nameof(TicketCategory.SecurityConcern)] = Prior + (security * 2.4),
            [nameof(TicketCategory.GeneralQuestion)] = Prior + (Count(text, GeneralTerms) * 1.1),
        };

        var teamWeights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [nameof(TargetTeam.ApplicationSupport)] = Prior + (technical * 1.3),
            [nameof(TargetTeam.Infrastructure)] = Prior + (infrastructure * 1.9) + (outage * 0.9),
            [nameof(TargetTeam.IdentityAccess)] = Prior + (access * 1.9),
            [nameof(TargetTeam.Security)] = Prior + (security * 2.6),
            [nameof(TargetTeam.BusinessOperations)] = Prior + (business * 1.5) + (inquiry * 0.6),
        };

        // Priority rises with explicit urgency, blast radius, and security exposure. A modest baseline
        // comes from the request type itself, so a plain fault report lands above a routine, pre-approved
        // request even when the reporter never says the word "urgent".
        var severity = Math.Clamp(
            (urgency * 1.1)
                + (outage * 1.3)
                + (security * 1.2)
                + (technical * 0.35)
                + (access * 0.05),
            0d,
            MaxPriorityLevel);

        var priorityWeights = BuildPriorityWeights(severity);

        return new TicketSignals(
            categoryWeights,
            teamWeights,
            priorityWeights,
            EstimateSensitiveData(text),
            EstimateHumanReview(text, security, urgency));
    }

    private static double EstimateSensitiveData(string text)
    {
        var probability = 0.04;

        if (Count(text, SensitiveTerms) > 0)
        {
            probability += 0.55;
        }

        // A long run of digits is a card, account, or national-id shape. On its own that is enough to
        // treat the ticket as sensitive, so this must clear the 0.5 threshold without help.
        if (LongDigitRunRegex().IsMatch(text))
        {
            probability += 0.55;
        }

        if (EmailRegex().IsMatch(text))
        {
            probability += 0.12;
        }

        return Math.Clamp(probability, 0d, 0.99);
    }

    private static double EstimateHumanReview(string text, int security, int urgency)
    {
        var probability = 0.08 + (security * 0.34) + (urgency * 0.08);

        if (Count(text, AmbiguityTerms) > 0)
        {
            probability += 0.25;
        }

        return Math.Clamp(probability, 0d, 0.99);
    }

    private static int Count(string text, IReadOnlyList<string> terms) =>
        terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Spreads the estimated severity across the four priority levels as a bell curve centred on it.
    /// A severity that lands squarely on one level produces a peaked distribution and therefore high
    /// confidence; one that falls between two levels stays spread out, which is exactly the signal the
    /// low-confidence escalation rule is meant to catch.
    /// </summary>
    private static double[] BuildPriorityWeights(double severity)
    {
        // Pull the centre partially towards the nearest level. Without this, a severity landing exactly
        // between two levels yields a perfectly flat distribution and zero confidence, so tiny wording
        // changes would swing the mock between certain and maximally unsure. The residual offset still
        // lowers confidence for borderline tickets, which is what the escalation rule keys off.
        var nearest = Math.Round(severity, MidpointRounding.AwayFromZero);
        var centre = nearest + ((severity - nearest) * PriorityCentrePull);

        var weights = new double[MaxPriorityLevel + 1];

        for (var level = 0; level <= MaxPriorityLevel; level++)
        {
            var distance = level - centre;
            weights[level] = Math.Exp(-(distance * distance) / (2 * PriorityKernelWidth * PriorityKernelWidth));
        }

        return weights;
    }

    private static readonly string[] TechnicalTerms =
    [
        "error", "crash", "bug", "fail", "broken", "not working", "freeze", "slow", "timeout",
        "exception", "unable to load", "blank screen",
        "خطا", "ارور", "کار نمی‌کند", "کار نمیکند", "قطع", "کند", "باگ", "هنگ", "بالا نمی‌آید",
        "لود نمی‌شود", "مشکل فنی",
    ];

    private static readonly string[] AccessTerms =
    [
        "access", "permission", "role", "account", "login", "log in", "sign in", "locked out",
        "password reset", "onboard", "new joiner", "provision", "sso", "mfa",
        "دسترسی", "مجوز", "نقش کاربری", "حساب کاربری", "ورود", "رمز عبور", "قفل شده", "کاربر جدید",
    ];

    private static readonly string[] SecurityTerms =
    [
        "phishing", "suspicious", "fraud", "malware", "ransomware", "breach", "leaked", "unauthorized",
        "unauthorised", "compromise", "hacked", "attack", "scam", "spoof",
        "فیشینگ", "مشکوک", "کلاهبرداری", "بدافزار", "نفوذ", "هک", "حمله", "جعلی", "سوءاستفاده",
    ];

    private static readonly string[] InfrastructureTerms =
    [
        "network", "server", "vpn", "printer", "database", "connectivity", "outage", "wifi", "disk",
        "شبکه", "سرور", "پرینتر", "پایگاه داده", "اتصال", "اینترنت",
    ];

    private static readonly string[] InquiryTerms =
    [
        "status", "follow up", "follow-up", "when will", "update on", "ticket number", "progress",
        "وضعیت", "پیگیری", "چه زمانی", "درخواست قبلی",
    ];

    private static readonly string[] BusinessTerms =
    [
        "policy", "process", "invoice", "billing", "contract", "procedure", "approval workflow",
        "سیاست", "فرآیند", "قرارداد", "صورتحساب", "رویه",
    ];

    private static readonly string[] GeneralTerms =
    [
        "question", "how do i", "wondering", "guidance", "clarify",
        "سوال", "پرسش", "راهنمایی", "چگونه",
    ];

    private static readonly string[] UrgencyTerms =
    [
        "urgent", "asap", "immediately", "critical", "blocked", "cannot work", "deadline",
        "فوری", "فورا", "بحرانی", "متوقف", "نمی‌توانیم کار کنیم",
    ];

    private static readonly string[] OutageTerms =
    [
        "all users", "everyone", "entire branch", "company-wide", "outage", "down for",
        "همه کاربران", "کل شعبه", "تمام شعب", "قطعی سراسری",
    ];

    private static readonly string[] SensitiveTerms =
    [
        "national id", "passport number", "card number", "cvv", "password is", "otp", "one-time code",
        "api key", "token is", "credentials are", "account number",
        "کد ملی", "شماره کارت", "رمز دوم", "رمز عبور من", "شماره حساب", "کد یکبار مصرف",
    ];

    private static readonly string[] AmbiguityTerms =
    [
        "not sure", "unclear", "several teams", "multiple issues", "escalate",
        "مطمئن نیستم", "نامشخص", "چند بخش",
    ];

    [GeneratedRegex(@"\d[\d\s-]{7,}\d", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex LongDigitRunRegex();

    [GeneratedRegex(
        @"[\w.+-]+@[\w-]+\.[\w.-]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();
}
