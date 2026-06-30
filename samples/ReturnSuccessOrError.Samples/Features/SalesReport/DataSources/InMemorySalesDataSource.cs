using SalesRows = System.Collections.Generic.IReadOnlyList<
    ReturnSuccessOrError.Samples.Features.SalesReport.SalesRow>;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>DataSource burro (em memória): gera <c>RowCount</c> linhas determinísticas.</summary>
public sealed class InMemorySalesDataSource : IDataSource<SalesRows, SalesReportParameters>
{
    private static readonly string[] Products =
        ["Mouse", "Teclado", "Monitor", "Headset", "Webcam"];

    public Task<SalesRows> CallAsync(
        SalesReportParameters p, CancellationToken cancellationToken = default)
    {
        var rows = new List<SalesRow>(p.RowCount);
        for (var i = 0; i < p.RowCount; i++)
        {
            rows.Add(new SalesRow(
                Product: Products[i % Products.Length],
                Quantity: (i % 5) + 1,
                UnitPrice: 50m + (i % 100)));
        }

        return Task.FromResult<SalesRows>(rows);
    }
}
