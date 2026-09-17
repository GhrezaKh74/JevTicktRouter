using JevTicketRouter.Application.Decisions;

namespace JevTicketRouter.Application.Benchmarking;

/// <summary>
/// The engines the benchmark should measure.
/// <para>
/// A separate type from <see cref="IDecisionEngine"/> on purpose. Triage resolves exactly one engine,
/// and registering several under the same interface would make which one serves production depend on
/// registration order. This keeps the comparison set explicit and leaves the production path
/// unambiguous.
/// </para>
/// </summary>
/// <param name="Engines">The engines to compare, in a stable order.</param>
public sealed record BenchmarkEngines(IReadOnlyList<IDecisionEngine> Engines);
