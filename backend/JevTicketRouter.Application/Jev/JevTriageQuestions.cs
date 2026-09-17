using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Application.Jev;

/// <summary>
/// Builds the single batched System One request used to triage a ticket.
/// <para>
/// All five decisions are asked in one call: the TypeSafe API evaluates every question in parallel
/// and in isolation against the same state, which is both cheaper and faster than one call per
/// question. Each question is kept atomic — one judgement, one primitive — so that the probabilities
/// and confidence values stay meaningful.
/// </para>
/// </summary>
public static class JevTriageQuestions
{
    /// <summary>Question id for the ticket category (choice).</summary>
    public const string CategoryQuestionId = "category";

    /// <summary>Question id for the owning team (choice).</summary>
    public const string TargetTeamQuestionId = "target_team";

    /// <summary>Question id for the priority level (score).</summary>
    public const string PriorityQuestionId = "priority";

    /// <summary>Question id for the sensitive-data check (noul).</summary>
    public const string SensitiveDataQuestionId = "contains_sensitive_data";

    /// <summary>Question id for the human-review check (noul).</summary>
    public const string HumanReviewQuestionId = "needs_human_review";

    /// <summary>
    /// Priority levels in ascending order. The index of each entry is the Jev score level, which is
    /// why this array must stay aligned with <see cref="TicketPriority"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> PriorityLevels =
    [
        "No material impact. A cosmetic problem, a general question, or a request that can wait for normal scheduling.",
        "One person is inconvenienced but can still do their job, or a workaround exists.",
        "Work is blocked for a person or a group, or money, a deadline, or a compliance obligation is at stake.",
        "Severe and immediate: a widespread outage, an active security incident, or major financial or legal exposure.",
    ];

    /// <summary>
    /// Builds the request body for one ticket.
    /// </summary>
    /// <param name="ticket">The submitted ticket.</param>
    /// <param name="model">The model name to send, e.g. <c>jev-latest</c>.</param>
    public static JevSystemOneRequest BuildRequest(SupportTicket ticket, string model)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new JevSystemOneRequest
        {
            State = BuildState(ticket),
            Model = model,
            Questions = BuildQuestions(),
        };
    }

    /// <summary>
    /// Builds the state object. An object rather than a bare string, so each part of the ticket keeps
    /// a descriptive name and the model can tell the title apart from the body and the requester.
    /// </summary>
    /// <param name="ticket">The submitted ticket.</param>
    public static object BuildState(SupportTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new Dictionary<string, object>
        {
            ["ticket"] = new Dictionary<string, string>
            {
                ["title"] = ticket.Title,
                ["description"] = ticket.Description,
                ["requester_role"] = DescribeRole(ticket.RequesterRole),
            },
            ["context"] = "An internal IT and operations service desk at a mid-sized company. "
                + "Tickets arrive in Persian or English, or a mix of both. Treat either language equally.",
        };
    }

    private static Dictionary<string, JevQuestion> BuildQuestions() => new()
    {
        [CategoryQuestionId] = new JevChoiceQuestion
        {
            Instructions = "Classify what kind of request this ticket is.",
            Criteria = new Dictionary<string, string?>
            {
                [nameof(TicketCategory.TechnicalIssue)] =
                    "Something is broken, failing, erroring, slow, or behaving incorrectly.",
                [nameof(TicketCategory.ServiceInquiry)] =
                    "A question about the status, progress, or handling of an existing service, order, or request.",
                [nameof(TicketCategory.AccessRequest)] =
                    "A request to be granted, restored, changed, or revoked access: accounts, roles, permissions, or password resets.",
                [nameof(TicketCategory.SecurityConcern)] =
                    "A suspected security problem: phishing, fraud, malware, a leaked credential, unauthorised access, or exposed confidential data.",
                [nameof(TicketCategory.GeneralQuestion)] =
                    "A general or informational question that does not fit any other category.",
            },
        },

        [TargetTeamQuestionId] = new JevChoiceQuestion
        {
            Instructions = "Decide which internal team should own and resolve this ticket.",
            Criteria = new Dictionary<string, string?>
            {
                [nameof(TargetTeam.ApplicationSupport)] =
                    "Faults and questions about business applications: the core banking app, the CRM, internal portals, reports.",
                [nameof(TargetTeam.Infrastructure)] =
                    "Servers, networking, connectivity, databases, printers, hardware, and platform-wide availability.",
                [nameof(TargetTeam.IdentityAccess)] =
                    "User accounts, passwords, roles, permissions, single sign-on, and multi-factor authentication.",
                [nameof(TargetTeam.Security)] =
                    "Security incidents, phishing and fraud reports, malware, and data-protection breaches.",
                [nameof(TargetTeam.BusinessOperations)] =
                    "Non-technical business process, policy, and operational questions that no technical team owns.",
            },
        },

        [PriorityQuestionId] = new JevScoreQuestion
        {
            Instructions = "Rate how urgently this ticket must be handled, based on impact and time sensitivity.",
            Criteria = PriorityLevels,
        },

        [SensitiveDataQuestionId] = new JevNoulQuestion
        {
            Instructions =
                "The ticket text itself contains sensitive data that must not be stored in plain text.",
            Criteria = new Dictionary<string, string>
            {
                ["true"] =
                    "The text includes personal or confidential values such as national id numbers, account or card numbers, "
                    + "passwords, one-time codes, API keys, tokens, or private customer records.",
                ["false"] =
                    "The text describes the problem without quoting any personal, financial, or secret value.",
            },
        },

        [HumanReviewQuestionId] = new JevNoulQuestion
        {
            Instructions =
                "A human triage agent should review this ticket before it is routed automatically.",
            Criteria = new Dictionary<string, string>
            {
                ["true"] =
                    "The request is ambiguous, unusually risky, spans several teams, or the consequences of "
                    + "misrouting it are serious.",
                ["false"] =
                    "The request is clear, routine, and safe for an automated system to assign on its own.",
            },
        },
    };

    private static string DescribeRole(RequesterRole role) => role switch
    {
        RequesterRole.BranchEmployee => "BranchEmployee: a staff member working in a branch office.",
        RequesterRole.Customer => "Customer: an external customer of the company.",
        RequesterRole.InternalSupport => "InternalSupport: a member of the internal support organisation.",
        _ => role.ToString(),
    };
}
