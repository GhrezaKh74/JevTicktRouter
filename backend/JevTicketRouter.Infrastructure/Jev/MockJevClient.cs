using System.Globalization;
using System.Text;
using System.Text.Json;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using Microsoft.Extensions.Logging;

namespace JevTicketRouter.Infrastructure.Jev;

/// <summary>
/// A deterministic stand-in for the TypeSafe API, used when no <c>TYPESAFE_API_KEY</c> is configured
/// so the project can be cloned and demonstrated without credentials.
/// <para>
/// It produces answers in exactly the shape the real API documents — a probability distribution per
/// choice and score question, a derived confidence, and a bare probability for each noul — so nothing
/// downstream can tell the difference. Scoring is keyword-driven over both Persian and English text
/// and is a demo aid, not a model: it is not an attempt to reproduce Jev's judgement.
/// </para>
/// </summary>
public sealed class MockJevClient : IJevClient
{
    /// <summary>The model id reported by mock answers, so mock data is never mistaken for live data.</summary>
    public const string MockModelId = "jev-mock-1.13.0";

    /// <summary>
    /// Power applied to raw keyword weights before normalising. See <see cref="Normalize"/>.
    /// </summary>
    private const double SharpeningExponent = 3.0;

    private readonly ILogger<MockJevClient> _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="logger">Structured logger.</param>
    public MockJevClient(ILogger<MockJevClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsLive => false;

    /// <inheritdoc />
    public Task<JevSystemOneResponse> EvaluateAsync(
        JevSystemOneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Jev mock mode: returning deterministic sample answers for {QuestionCount} questions. "
                + "No call was made to the TypeSafe API.",
            request.Questions.Count);

        var text = ExtractText(request.State);
        var signals = TicketSignals.Extract(text);

        var answers = new Dictionary<string, JevAnswer>
        {
            [JevTriageQuestions.CategoryQuestionId] = Choice(signals.CategoryWeights),
            [JevTriageQuestions.TargetTeamQuestionId] = Choice(signals.TeamWeights),
            [JevTriageQuestions.PriorityQuestionId] = Score(signals.PriorityWeights),
            [JevTriageQuestions.SensitiveDataQuestionId] = Noul(signals.SensitiveDataProbability),
            [JevTriageQuestions.HumanReviewQuestionId] = Noul(signals.HumanReviewProbability),
        };

        // Only answer what was actually asked, mirroring the API's per-question contract.
        var scoped = answers
            .Where(pair => request.Questions.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        var response = new JevSystemOneResponse
        {
            Model = MockModelId,
            Answers = scoped,
            Usage = new JevUsage
            {
                InputTokens = Math.Max(1, text.Length / 4),
                OutputTokens = scoped.Count * 8,
            },
        };

        return Task.FromResult(response);
    }

    private static JevAnswer Choice(IReadOnlyDictionary<string, double> weights)
    {
        var probabilities = Normalize(weights);

        return new JevAnswer
        {
            Type = "choice",
            Choice = probabilities.MaxBy(pair => pair.Value).Key,
            Probabilities = probabilities,
            Confidence = DeriveConfidence(probabilities.Values),
        };
    }

    private static JevAnswer Score(IReadOnlyList<double> levelWeights)
    {
        var probabilities = Normalize(
            levelWeights
                .Select((weight, index) => (Key: index.ToString(CultureInfo.InvariantCulture), Weight: weight))
                .ToDictionary(pair => pair.Key, pair => pair.Weight, StringComparer.Ordinal));

        // The documented score is the probability-weighted position across the levels.
        var score = probabilities.Sum(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture) * pair.Value);

        // The contract types legend values as raw JSON, so the descriptions are wrapped as such.
        var legend = JevTriageQuestions.PriorityLevels
            .Select((description, index) => (Key: index.ToString(CultureInfo.InvariantCulture), description))
            .ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.description),
                StringComparer.Ordinal);

        return new JevAnswer
        {
            Type = "score",
            Score = Math.Round(score, 3),
            Legend = legend,
            Probabilities = probabilities,
            Confidence = DeriveConfidence(probabilities.Values),
        };
    }

    private static JevAnswer Noul(double probability) => new()
    {
        Type = "noul",
        Noul = Math.Round(Math.Clamp(probability, 0d, 1d), 3),
    };

    /// <summary>
    /// Turns raw keyword weights into a probability distribution that sums to 1.
    /// <para>
    /// A power transform is applied first. Raw keyword counts produce distributions far flatter than
    /// a trained model's, which would leave every mock ticket below the confidence threshold and make
    /// the human-review rule fire on all of them. Sharpening keeps mock mode decisive enough to
    /// demonstrate confidence-gated routing, while genuinely ambiguous tickets still stay flat.
    /// </para>
    /// </summary>
    private static Dictionary<string, double> Normalize(IReadOnlyDictionary<string, double> weights)
    {
        if (weights.Values.Sum() <= 0d)
        {
            var uniform = 1d / weights.Count;
            return weights.ToDictionary(pair => pair.Key, _ => Math.Round(uniform, 3), StringComparer.Ordinal);
        }

        var sharpened = weights.ToDictionary(
            pair => pair.Key,
            pair => Math.Pow(pair.Value, SharpeningExponent),
            StringComparer.Ordinal);

        var total = sharpened.Values.Sum();

        return sharpened.ToDictionary(
            pair => pair.Key,
            pair => Math.Round(pair.Value / total, 4),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Collapses a distribution into a single certainty value, the way the API documents confidence:
    /// a distribution concentrated on one outcome is confident, a flat one is not. Implemented as
    /// 1 minus the normalised Shannon entropy.
    /// </summary>
    private static double DeriveConfidence(IEnumerable<double> probabilities)
    {
        var values = probabilities.Where(value => value > 0d).ToList();

        if (values.Count <= 1)
        {
            return 1d;
        }

        var entropy = -values.Sum(value => value * Math.Log(value));
        var maxEntropy = Math.Log(values.Count);

        return Math.Round(Math.Clamp(1d - (entropy / maxEntropy), 0d, 1d), 3);
    }

    /// <summary>Flattens the state object into a single lower-cased string for keyword matching.</summary>
    private static string ExtractText(object state)
    {
        var builder = new StringBuilder();

        using var document = JsonSerializer.SerializeToDocument(state, JevJsonSerialization.Options);
        Collect(document.RootElement, builder);

        return builder.ToString().ToLowerInvariant();
    }

    private static void Collect(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                builder.Append(element.GetString()).Append(' ');
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, builder);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, builder);
                }

                break;

            default:
                break;
        }
    }
}
