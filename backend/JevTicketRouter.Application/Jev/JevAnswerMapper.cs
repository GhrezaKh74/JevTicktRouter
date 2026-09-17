using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Application.Jev;

/// <summary>
/// Maps the raw TypeSafe answers onto the domain's <see cref="JevAssessment"/>. Treats the response
/// as untrusted input: a missing question, an unknown option, or an out-of-range score is reported as
/// a <see cref="JevClientException"/> rather than silently producing a wrong routing decision.
/// </summary>
public static class JevAnswerMapper
{
    /// <summary>Maps a System One response into a domain assessment.</summary>
    /// <param name="response">The response returned by <see cref="IJevClient"/>.</param>
    /// <exception cref="JevClientException">The response was missing or malformed for any question.</exception>
    public static JevAssessment Map(JevSystemOneResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var category = ReadChoice<TicketCategory>(response, JevTriageQuestions.CategoryQuestionId);
        var team = ReadChoice<TargetTeam>(response, JevTriageQuestions.TargetTeamQuestionId);
        var priority = ReadPriority(response);

        return new JevAssessment(
            category.Value,
            category.Confidence,
            team.Value,
            team.Confidence,
            priority.Value,
            priority.Confidence,
            priority.RawScore,
            ReadNoul(response, JevTriageQuestions.SensitiveDataQuestionId),
            ReadNoul(response, JevTriageQuestions.HumanReviewQuestionId),
            string.IsNullOrWhiteSpace(response.Model) ? "unknown" : response.Model);
    }

    private static (TEnum Value, double Confidence) ReadChoice<TEnum>(
        JevSystemOneResponse response,
        string questionId)
        where TEnum : struct, Enum
    {
        var answer = RequireAnswer(response, questionId);

        if (string.IsNullOrWhiteSpace(answer.Choice))
        {
            throw new JevClientException($"Jev answer '{questionId}' did not include a choice.");
        }

        if (!Enum.TryParse<TEnum>(answer.Choice, ignoreCase: true, out var parsed))
        {
            throw new JevClientException(
                $"Jev answer '{questionId}' returned unknown option '{answer.Choice}'.");
        }

        return (parsed, Clamp01(answer.Confidence ?? 0d));
    }

    private static (TicketPriority Value, double Confidence, double RawScore) ReadPriority(
        JevSystemOneResponse response)
    {
        var answer = RequireAnswer(response, JevTriageQuestions.PriorityQuestionId);

        if (answer.Score is not { } score)
        {
            throw new JevClientException(
                $"Jev answer '{JevTriageQuestions.PriorityQuestionId}' did not include a score.");
        }

        // Prefer the most probable discrete level: the weighted score can land between two levels,
        // and routing needs one of the four defined priorities. Fall back to rounding the score when
        // the distribution is absent.
        var level = MostProbableLevel(answer.Probabilities)
            ?? (int)Math.Round(score, MidpointRounding.AwayFromZero);

        var maxLevel = JevTriageQuestions.PriorityLevels.Count - 1;
        level = Math.Clamp(level, 0, maxLevel);

        return ((TicketPriority)level, Clamp01(answer.Confidence ?? 0d), score);
    }

    private static int? MostProbableLevel(IReadOnlyDictionary<string, double>? probabilities)
    {
        if (probabilities is null || probabilities.Count == 0)
        {
            return null;
        }

        int? best = null;
        var bestProbability = double.NegativeInfinity;

        foreach (var (key, probability) in probabilities)
        {
            if (!int.TryParse(key, out var level) || probability <= bestProbability)
            {
                continue;
            }

            best = level;
            bestProbability = probability;
        }

        return best;
    }

    private static double ReadNoul(JevSystemOneResponse response, string questionId)
    {
        var answer = RequireAnswer(response, questionId);

        if (answer.Noul is not { } noul)
        {
            throw new JevClientException($"Jev answer '{questionId}' did not include a noul value.");
        }

        return Clamp01(noul);
    }

    private static JevAnswer RequireAnswer(JevSystemOneResponse response, string questionId)
    {
        if (!response.Answers.TryGetValue(questionId, out var answer) || answer is null)
        {
            throw new JevClientException($"Jev response is missing an answer for '{questionId}'.");
        }

        return answer;
    }

    private static double Clamp01(double value) =>
        double.IsNaN(value) ? 0d : Math.Clamp(value, 0d, 1d);
}
