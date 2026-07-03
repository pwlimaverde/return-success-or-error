// Mitigação do TData longo: alias `using` (C# 12) torna a assinatura legível.
using SalesRows = System.Collections.Generic.IReadOnlyList<
    ReturnSuccessOrError.Samples.Features.SalesReport.SalesRow>;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>
/// UseCase (regra de negócio, PORTÁVEL): agrega as linhas num relatório. Depende de
/// <see cref="IRepository{TData, TParams, TError}"/> — funciona com qualquer datasource por baixo.
/// </summary>
public sealed class GenerateSalesReportUsecase(IRepository<SalesRows, SalesReportParameters, SalesError> repo)
    : UsecaseBaseCallData<SalesReport, SalesRows, SalesReportParameters, SalesError>(repo)
{
    protected override ReturnSuccessOrError<SalesReport, SalesError> Process(
        SalesRows rows, SalesReportParameters p, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return Fail(new EmptyPeriod($"Sem vendas no período (RowCount = {p.RowCount})")); // -> Failure (factory, sem cast)

        decimal revenue = 0;
        var items = 0;
        var byProduct = new Dictionary<string, decimal>();
        foreach (var r in rows)
        {
            // Cancelamento cooperativo em processamento longo: o token do chamador chega ao Process.
            cancellationToken.ThrowIfCancellationRequested();

            var total = r.Quantity * r.UnitPrice;
            revenue += total;
            items += r.Quantity;
            byProduct[r.Product] = byProduct.GetValueOrDefault(r.Product) + total;
        }

        var top = byProduct.MaxBy(kv => kv.Value).Key;

        return Ok(new SalesReport(items, revenue, revenue / rows.Count, top)); // -> Success
    }

    protected override SalesError OnUnexpected(Exception exception)
        => new ErrorGeneric($"Bug na agregação: {exception.Message}");
}
