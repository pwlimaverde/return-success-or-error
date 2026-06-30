using System.Globalization;
using SalesRows = System.Collections.Generic.IReadOnlyList<
    ReturnSuccessOrError.Samples.Features.SalesReport.SalesRow>;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>DataSource burro alternativo (CSV): mesma forma de dado, fonte diferente.</summary>
public sealed class CsvSalesDataSource(string csv) : IDataSource<SalesRows, SalesReportParameters>
{
    public Task<SalesRows> CallAsync(
        SalesReportParameters p, CancellationToken cancellationToken = default)
    {
        var rows = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(p.RowCount)
            .Select(line =>
            {
                var cols = line.Split(';');
                return new SalesRow(
                    cols[0],
                    int.Parse(cols[1], CultureInfo.InvariantCulture),
                    decimal.Parse(cols[2], CultureInfo.InvariantCulture));
            })
            .ToList();

        return Task.FromResult<SalesRows>(rows);
    }
}
