using System.Text.Json;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>
/// Builds the prompt for the local model.
/// <para>
/// The rubric mirrors the criteria sent to Jev, so the two providers are asked the same question and
/// a benchmark comparison is meaningful. The ticket is passed as JSON in the user message, keeping
/// the instructions and the content clearly separated.
/// </para>
/// </summary>
public static class LocalDecisionPrompt
{
    /// <summary>Builds the system message: the task, the rubric, and the required output shape.</summary>
    public static string BuildSystemMessage()
    {
        var categories = string.Join("\n", new[]
        {
            $"- {nameof(TicketCategory.TechnicalIssue)}: something is broken, failing, erroring, slow, or behaving incorrectly.",
            $"- {nameof(TicketCategory.ServiceInquiry)}: a question about the status or handling of an existing service, order, or request.",
            $"- {nameof(TicketCategory.AccessRequest)}: a request to grant, restore, change, or revoke access, accounts, roles, permissions, or passwords.",
            $"- {nameof(TicketCategory.SecurityConcern)}: a suspected security problem: phishing, fraud, malware, a leaked credential, unauthorised access, or exposed confidential data.",
            $"- {nameof(TicketCategory.GeneralQuestion)}: a general or informational question fitting no other category.",
        });

        var teams = string.Join("\n", new[]
        {
            $"- {nameof(TargetTeam.ApplicationSupport)}: faults and questions about business applications, internal portals, and reports.",
            $"- {nameof(TargetTeam.Infrastructure)}: servers, networking, connectivity, databases, printers, hardware, platform availability.",
            $"- {nameof(TargetTeam.IdentityAccess)}: user accounts, passwords, roles, permissions, single sign-on, multi-factor authentication.",
            $"- {nameof(TargetTeam.Security)}: security incidents, phishing and fraud reports, malware, data-protection breaches.",
            $"- {nameof(TargetTeam.BusinessOperations)}: non-technical business process, policy, and operational questions.",
        });

        var priorities = string.Join("\n", JevTriageQuestions.PriorityLevels
            .Select((level, index) => $"- {(TicketPriority)index}: {level}"));

        return $"""
        You triage support tickets for an internal IT and operations service desk.
        Tickets arrive in Persian or English, or a mix of both. Treat either language equally.

        Classify the ticket on five independent judgements.

        category — one of:
        {categories}

        target_team — one of:
        {teams}

        priority — one of:
        {priorities}

        contains_sensitive_data_probability — the probability from 0 to 1 that the ticket text itself
        quotes a sensitive value: a national id or card or account number, a password, a one-time
        code, an API key, a token, or a private customer record. Describing a problem without quoting
        such a value is not sensitive.

        needs_human_review_probability — the probability from 0 to 1 that a human triage agent should
        review this before it is routed automatically, because it is ambiguous, unusually risky,
        spans several teams, or would be costly to misroute.

        Each confidence field is your own certainty about that field, from 0 to 1. Report genuine
        uncertainty honestly: a low confidence is used to route the ticket to a human, which is a
        useful outcome, not a failure.

        Reply with a single JSON object and nothing else. No prose, no explanation, no code fence.
        """;
    }

    /// <summary>Builds the user message: the ticket, as JSON.</summary>
    public static string BuildUserMessage(TicketInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ticket = new Dictionary<string, string>
        {
            ["title"] = input.Title,
            ["description"] = input.Description,
            ["requester_role"] = DescribeRole(input.RequesterRole),
        };

        return JsonSerializer.Serialize(new Dictionary<string, object> { ["ticket"] = ticket });
    }

    private static string DescribeRole(RequesterRole role) => role switch
    {
        RequesterRole.BranchEmployee => "BranchEmployee: a staff member working in a branch office.",
        RequesterRole.Customer => "Customer: an external customer of the company.",
        RequesterRole.InternalSupport => "InternalSupport: a member of the internal support organisation.",
        _ => role.ToString(),
    };
}
