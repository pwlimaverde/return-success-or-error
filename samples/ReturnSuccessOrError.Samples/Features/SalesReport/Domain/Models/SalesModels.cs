namespace ReturnSuccessOrError.Samples.Features.SalesReport;

/// <summary>Uma linha bruta de venda (dado vindo da fonte).</summary>
public sealed record SalesRow(string Product, int Quantity, decimal UnitPrice);

/// <summary>Relatório agregado de vendas (valor de sucesso do caso de uso).</summary>
public sealed record SalesReport(
    int TotalItems, decimal TotalRevenue, decimal AverageTicket, string TopProduct);
