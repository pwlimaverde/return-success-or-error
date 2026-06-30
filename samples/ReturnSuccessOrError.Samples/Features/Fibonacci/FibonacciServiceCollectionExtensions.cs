using Microsoft.Extensions.DependencyInjection;

namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>Registro de DI da feature — usecase configurado para rodar em background.</summary>
public static class FibonacciServiceCollectionExtensions
{
    public static IServiceCollection AddFibonacciFeature(this IServiceCollection services)
        => services
            .AddSingleton(_ => new FibonacciUsecase
            {
                RunInBackground = true,
                MonitorExecutionTime = true,
            })
            .AddSingleton<IFibonacciService, FibonacciService>();
}
