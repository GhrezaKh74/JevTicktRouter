namespace JevTicketRouter.Application.Benchmarking;

/// <summary>Runs the fictional corpus against every configured provider and compares the outcomes.</summary>
public interface IBenchmarkRunner
{
    /// <summary>True when at least one provider is available to measure.</summary>
    bool IsAvailable { get; }

    /// <summary>Runs the benchmark.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<BenchmarkReport> RunAsync(CancellationToken cancellationToken);
}
