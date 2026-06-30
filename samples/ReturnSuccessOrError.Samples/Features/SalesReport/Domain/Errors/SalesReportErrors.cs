namespace ReturnSuccessOrError.Samples.Features.SalesReport;

// ── Conjunto fechado de erros da feature ──────────────────────────────────────
/// <summary>Erro de negócio: período sem vendas.</summary>
public sealed record EmptyPeriod(string Message) : AppError(Message);

/// <summary>Falha de I/O traduzida pelo Repository (CSV malformado).</summary>
public sealed record InvalidCsv(string Message) : AppError(Message);

/// <summary>Os erros possíveis desta feature.</summary>
public readonly union SalesError(EmptyPeriod, InvalidCsv, ErrorGeneric);

/// <summary>Descrição exaustiva do erro.</summary>
public static class SalesErrorText
{
    public static string Describe(this SalesError error) => error switch
    {
        EmptyPeriod e  => $"[negócio] {e.Message}",
        InvalidCsv c   => $"[infra] {c.Message}",
        ErrorGeneric g => $"[inesperado] {g.Message}",
    };
}
