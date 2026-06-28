using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError.Samples.Composition;

namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>Parâmetros da verificação de conectividade.</summary>
public sealed record CheckConnectionParameters(AppError Error) : ParametersReturnResult(Error);

/// <summary>
/// Fonte de dados fake de conectividade. <paramref name="online"/> define o resultado;
/// <paramref name="shouldThrow"/> simula uma exceção de infraestrutura (capturada como DataSourceCatch).
/// </summary>
public sealed class FakeConnectivityDataSource(bool online, bool shouldThrow = false)
    : IDataSource<bool>
{
    public Task<bool> CallAsync(
        ParametersReturnResult parameters, CancellationToken cancellationToken = default)
    {
        if (shouldThrow)
            throw new InvalidOperationException("Falha de rede ao verificar conectividade");
        return Task.FromResult(online);
    }
}

/// <summary>Caso de uso: mapeia o estado de conectividade (bool) em mensagem ou erro de negócio.</summary>
public sealed class CheckConnectionUsecase(IDataSource<bool> ds)
    : UsecaseBaseCallData<string, bool>(ds)
{
    protected override ReturnSuccessOrError<string> Process(bool online, ParametersReturnResult p)
    {
        if (!online)
            return p.Error.WithMessage("You are offline");  // AppError -> Failure (conversão implícita)
        return "You are connected";                          // string -> Success (conversão implícita)
    }
}

/// <summary>Service da feature (Service Layer) — ponto de entrada público.</summary>
public sealed class CheckConnectionService(CheckConnectionUsecase usecase) : IFeatureService
{
    public Task<ReturnSuccessOrError<string>> CheckAsync(CancellationToken cancellationToken = default)
        => usecase.CallAsync(
            new CheckConnectionParameters(new ErrorGeneric("Falha ao verificar conectividade")),
            cancellationToken);
}

/// <summary>Composition Root da feature (online por padrão).</summary>
public sealed class CheckConnectionModule : IFeatureModule
{
    public IServiceCollection RegisterServices(IServiceCollection services)
        => services
            .AddSingleton<IDataSource<bool>>(_ => new FakeConnectivityDataSource(online: true))
            .AddSingleton<CheckConnectionUsecase>()
            .AddSingleton<CheckConnectionService>();
}
