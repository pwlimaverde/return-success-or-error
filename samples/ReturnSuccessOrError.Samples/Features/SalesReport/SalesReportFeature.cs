using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError.Samples.Composition;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Uma linha bruta de venda (dado vindo da fonte).</summary>
public sealed record SalesRow(string Product, int Quantity, decimal UnitPrice);

/// <summary>Relatório agregado de vendas (valor de sucesso do caso de uso).</summary>
public sealed record SalesReport(
    int TotalItems, decimal TotalRevenue, decimal AverageTicket, string TopProduct);

/// <summary>Parâmetros da geração do relatório.</summary>
public sealed record SalesReportParameters(int RowCount, AppError Error) : ParametersReturnResult(Error);

/// <summary>Fonte de dados fake: gera <c>RowCount</c> linhas determinísticas de venda.</summary>
public sealed class FakeSalesDataSource : IDataSource<IReadOnlyList<SalesRow>>
{
    private static readonly string[] Products =
        ["Mouse", "Teclado", "Monitor", "Headset", "Webcam"];

    public Task<IReadOnlyList<SalesRow>> CallAsync(
        ParametersReturnResult parameters, CancellationToken cancellationToken = default)
    {
        var p = (SalesReportParameters)parameters;
        var rows = new List<SalesRow>(p.RowCount);
        for (var i = 0; i < p.RowCount; i++)
        {
            rows.Add(new SalesRow(
                Product: Products[i % Products.Length],
                Quantity: (i % 5) + 1,
                UnitPrice: 50m + (i % 100)));
        }

        return Task.FromResult<IReadOnlyList<SalesRow>>(rows);
    }
}

/// <summary>Caso de uso: agrega as linhas brutas num relatório (process CPU-bound).</summary>
public sealed class GenerateSalesReportUsecase(IDataSource<IReadOnlyList<SalesRow>> ds)
    : UsecaseBaseCallData<SalesReport, IReadOnlyList<SalesRow>>(ds)
{
    protected override ReturnSuccessOrError<SalesReport> Process(
        IReadOnlyList<SalesRow> rows, ParametersReturnResult p)
    {
        if (rows.Count == 0)
            return p.Error.WithMessage("Sem vendas no período");  // AppError -> Failure

        decimal revenue = 0;
        var items = 0;
        var byProduct = new Dictionary<string, decimal>();
        foreach (var r in rows)
        {
            var total = r.Quantity * r.UnitPrice;
            revenue += total;
            items += r.Quantity;
            byProduct[r.Product] = byProduct.GetValueOrDefault(r.Product) + total;
        }

        var top = byProduct.MaxBy(kv => kv.Value).Key;

        return new SalesReport(items, revenue, revenue / rows.Count, top);  // -> Success
    }
}

/// <summary>Service da feature.</summary>
public sealed class SalesReportService(GenerateSalesReportUsecase usecase) : IFeatureService
{
    public Task<ReturnSuccessOrError<SalesReport>> GenerateAsync(
        int rowCount, CancellationToken cancellationToken = default)
        => usecase.CallAsync(
            new SalesReportParameters(rowCount, new ErrorGeneric("Falha ao gerar relatório")),
            cancellationToken);
}

/// <summary>Composition Root da feature — usecase em background, com medição de tempo.</summary>
public sealed class SalesReportModule : IFeatureModule
{
    public IServiceCollection RegisterServices(IServiceCollection services)
        => services
            .AddSingleton<IDataSource<IReadOnlyList<SalesRow>>, FakeSalesDataSource>()
            .AddSingleton(sp => new GenerateSalesReportUsecase(
                sp.GetRequiredService<IDataSource<IReadOnlyList<SalesRow>>>())
            {
                RunInBackground = true,
                MonitorExecutionTime = true,
            })
            .AddSingleton<SalesReportService>();
}
