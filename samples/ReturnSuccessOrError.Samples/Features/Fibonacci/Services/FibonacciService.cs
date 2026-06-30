namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>Service da feature.</summary>
public sealed class FibonacciService(FibonacciUsecase usecase) : IFibonacciService
{
    public Task<ReturnSuccessOrError<long, FibonacciError>> CalculateAsync(
        int n, CancellationToken cancellationToken = default)
        => usecase.CallAsync(new FibonacciParameters(n), cancellationToken);
}
