namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Parâmetros da geração do relatório — só dados.</summary>
public sealed record SalesReportParameters(int RowCount) : Parameters;
