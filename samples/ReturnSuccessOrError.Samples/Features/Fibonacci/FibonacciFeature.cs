using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError.Samples.Composition;

namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>Parâmetros do cálculo de Fibonacci.</summary>
public sealed record FibonacciParameters(int N, AppError Error) : ParametersReturnResult(Error);

/// <summary>
/// Caso de uso de lógica pura (sem fonte de dados). Cálculo recursivo CPU-bound, ideal para
/// demonstrar <c>RunInBackground = true</c> despachando o processamento ao thread pool.
/// </summary>
public sealed class FibonacciUsecase : UsecaseBase<long>
{
    protected override ReturnSuccessOrError<long> Process(ParametersReturnResult p)
    {
        var fp = (FibonacciParameters)p;
        if (fp.N < 0)
            return ReturnSuccessOrError<long>.Err(p.Error.WithMessage("N deve ser >= 0"));
        return ReturnSuccessOrError<long>.Ok(Fib(fp.N));
    }

    private static long Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);
}

/// <summary>Service da feature.</summary>
public sealed class FibonacciService(FibonacciUsecase usecase) : IFeatureService
{
    public Task<ReturnSuccessOrError<long>> CalculateAsync(
        int n, CancellationToken cancellationToken = default)
        => usecase.CallAsync(
            new FibonacciParameters(n, new ErrorGeneric("Falha ao calcular Fibonacci")),
            cancellationToken);
}

/// <summary>Composition Root da feature — usecase configurado para rodar em background.</summary>
public sealed class FibonacciModule : IFeatureModule
{
    public IServiceCollection RegisterServices(IServiceCollection services)
        => services
            .AddSingleton(_ => new FibonacciUsecase
            {
                RunInBackground = true,
                MonitorExecutionTime = true,
            })
            .AddSingleton<FibonacciService>();
}
