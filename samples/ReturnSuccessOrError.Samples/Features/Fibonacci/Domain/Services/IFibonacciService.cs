namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>Contrato do service da feature.</summary>
public interface IFibonacciService
{
    Task<ReturnSuccessOrError<long, FibonacciError>> CalculateAsync(
        int n, CancellationToken cancellationToken = default);
}
