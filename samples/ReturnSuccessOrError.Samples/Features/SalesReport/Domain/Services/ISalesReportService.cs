namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Contrato do service da feature.</summary>
public interface ISalesReportService
{
    Task<ReturnSuccessOrError<SalesReport, SalesError>> GenerateAsync(
        int rowCount, CancellationToken cancellationToken = default);
}
