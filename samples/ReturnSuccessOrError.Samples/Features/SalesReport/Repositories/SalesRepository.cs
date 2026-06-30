using SalesRows = System.Collections.Generic.IReadOnlyList<
    ReturnSuccessOrError.Samples.Features.SalesReport.SalesRow>;

namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Repository (fronteira): traduz exceção de parsing/fonte num dos erros do union.</summary>
public sealed class SalesRepository(IDataSource<SalesRows, SalesReportParameters> ds)
    : RepositoryBase<SalesRows, SalesReportParameters, SalesError>(ds)
{
    protected override SalesError MapError(Exception exception, SalesReportParameters parameters) =>
        exception switch
        {
            FormatException => new InvalidCsv($"CSV inválido: {exception.Message}"),
            _               => new ErrorGeneric($"Falha ao carregar vendas: {exception.Message}"),
        };
}
