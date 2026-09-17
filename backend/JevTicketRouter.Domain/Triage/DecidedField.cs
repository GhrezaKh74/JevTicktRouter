namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// A single final field of a triage decision, together with the provenance needed to explain it:
/// what Jev proposed, how confident Jev was, and whether a deterministic rule changed it.
/// </summary>
/// <typeparam name="T">The type of the decided value.</typeparam>
/// <param name="Value">The final, authoritative value.</param>
/// <param name="ModelValue">What the Jev model proposed.</param>
/// <param name="Confidence">
/// Jev's confidence in <paramref name="ModelValue"/>, from 0 to 1. Null when the underlying question
/// type does not report one: per the TypeSafe API, <c>noul</c> answers carry no confidence.
/// </param>
/// <param name="Origin">Whether the final value came from Jev or from a business rule.</param>
public sealed record DecidedField<T>(T Value, T ModelValue, double? Confidence, DecisionOrigin Origin)
{
    /// <summary>Creates a field that Jev decided and no rule touched.</summary>
    public static DecidedField<T> FromModel(T value, double? confidence) =>
        new(value, value, confidence, DecisionOrigin.JevModel);

    /// <summary>Returns a copy of this field overridden by a deterministic rule.</summary>
    public DecidedField<T> OverriddenBy(T value) =>
        this with { Value = value, Origin = DecisionOrigin.BusinessRule };

    /// <summary>True when a business rule produced a value different from Jev's proposal.</summary>
    public bool WasOverridden =>
        Origin == DecisionOrigin.BusinessRule && !EqualityComparer<T>.Default.Equals(Value, ModelValue);
}
