using Microsoft.Extensions.DependencyInjection;

namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>
/// Registro de DI da feature (método de extensão idiomático): DataSource → Repository → UseCase → Service.
/// </summary>
public static class CheckConnectionServiceCollectionExtensions
{
    public static IServiceCollection AddCheckConnectionFeature(this IServiceCollection services)
        => services
            .AddSingleton<IDataSource<bool, NoParams>>(
                _ => new FakeConnectivityDataSource(online: true))
            .AddSingleton<IRepository<bool, NoParams, CheckConnectionError>,
                CheckConnectionRepository>()
            .AddSingleton<CheckConnectionUsecase>()
            .AddSingleton<ICheckConnectionService, CheckConnectionService>();
}
