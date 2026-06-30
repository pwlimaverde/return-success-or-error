using Microsoft.Extensions.DependencyInjection;
using SalesRows = System.Collections.Generic.IReadOnlyList<
    ReturnSuccessOrError.Samples.Features.SalesReport.SalesRow>;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Registro de DI da feature — usecase em background, com medição de tempo.</summary>
public static class SalesReportServiceCollectionExtensions
{
    public static IServiceCollection AddSalesReportFeature(this IServiceCollection services)
        => services
            .AddSingleton<IDataSource<SalesRows, SalesReportParameters>, InMemorySalesDataSource>()
            .AddSingleton<IRepository<SalesRows, SalesReportParameters, SalesError>, SalesRepository>()
            .AddSingleton(sp => new GenerateSalesReportUsecase(
                sp.GetRequiredService<IRepository<SalesRows, SalesReportParameters, SalesError>>())
            {
                RunInBackground = true,
                MonitorExecutionTime = true,
            })
            .AddSingleton<ISalesReportService, SalesReportService>();
}
