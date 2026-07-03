namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>
/// Caso de uso de lógica pura (sem fonte de dados → <see cref="UsecaseBase{TValue, TParams, TError}"/>).
/// Cálculo recursivo CPU-bound, ideal para demonstrar <c>RunInBackground = true</c>.
/// </summary>
public sealed class FibonacciUsecase : UsecaseBase<long, FibonacciParameters, FibonacciError>
{
    protected override ReturnSuccessOrError<long, FibonacciError> Process(
        FibonacciParameters p, CancellationToken cancellationToken)
    {
        if (p.N < 0)
            return Fail(new NegativeInput($"N deve ser >= 0 (N = {p.N})")); // -> Failure (factory, sem cast)
        return Ok(Fib(p.N));                                                 // long -> Success
    }

    protected override FibonacciError OnUnexpected(Exception exception)
        => new ErrorGeneric($"Bug no cálculo: {exception.Message}");

    private static long Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);
}
