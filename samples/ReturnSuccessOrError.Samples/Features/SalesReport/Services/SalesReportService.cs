namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Service da feature.</summary>
public sealed class SalesReportService(GenerateSalesReportUsecase usecase) : ISalesReportService
{
    public Task<ReturnSuccessOrError<SalesReport, SalesError>> GenerateAsync(
        int rowCount, CancellationToken cancellationToken = default)
        => usecase.CallAsync(new SalesReportParameters(rowCount), cancellationToken);
}
