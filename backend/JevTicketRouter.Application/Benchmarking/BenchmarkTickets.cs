using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Domain.Tickets;

namespace JevTicketRouter.Application.Benchmarking;

/// <summary>A fictional ticket used to compare providers.</summary>
/// <param name="Id">A stable identifier, reported instead of the ticket text.</param>
/// <param name="Input">The ticket itself.</param>
public sealed record BenchmarkTicket(string Id, TicketInput Input);

/// <summary>
/// The fixed corpus the benchmark runs.
/// <para>
/// Entirely fictional and held in code rather than read from submitted tickets, so a benchmark can
/// never touch real customer data. Reports identify a case by <see cref="BenchmarkTicket.Id"/>
/// only — no ticket content is ever stored or returned.
/// </para>
/// </summary>
public static class BenchmarkTickets
{
    /// <summary>The demo corpus: Persian and English, routine and escalating.</summary>
    public static IReadOnlyList<BenchmarkTicket> All { get; } =
    [
        new("persian-technical", new TicketInput(
            "خطا هنگام ثبت تراکنش در سامانه شعبه",
            "از امروز صبح هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شوم و صفحه لود نمی‌شود. "
                + "مجبورم هر تراکنش را دو بار وارد کنم و کار باجه کند شده است. لطفاً بررسی کنید.",
            RequesterRole.BranchEmployee)),

        new("english-access", new TicketInput(
            "Access to the reporting portal for a new analyst",
            "Our new business analyst started on Monday and needs read-only access to the quarterly "
                + "reporting portal. Their manager has already approved the request. Please provision "
                + "the account and the standard analyst role.",
            RequesterRole.InternalSupport)),

        new("security-phishing", new TicketInput(
            "Suspicious email asking staff to confirm their password",
            "Several colleagues received a suspicious email that looks like phishing. It links to a "
                + "fake login page and asks them to confirm their password. One colleague replied "
                + "before realising, and the message quoted their internal reference 4400123400567800. "
                + "Please investigate urgently.",
            RequesterRole.InternalSupport)),

        new("persian-access", new TicketInput(
            "درخواست دسترسی به سامانه گزارش‌گیری",
            "همکار جدید بخش اعتبارات از هفته گذشته شروع به کار کرده و به دسترسی فقط‌خواندنی سامانه "
                + "گزارش‌گیری نیاز دارد. مدیر مربوطه درخواست را تأیید کرده است.",
            RequesterRole.InternalSupport)),

        new("english-infrastructure", new TicketInput(
            "Branch printer will not come back online",
            "The shared printer on the second floor has not come back online since the power cut last "
                + "night. Restarting it and reconnecting the network cable made no difference.",
            RequesterRole.BranchEmployee)),

        new("ambiguous", new TicketInput(
            "Something is not right",
            "I am not sure what is happening but something seems off with the system today. Could "
                + "someone take a look when possible?",
            RequesterRole.Customer)),
    ];
}
