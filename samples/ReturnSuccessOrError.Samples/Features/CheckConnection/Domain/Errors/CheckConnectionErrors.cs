namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

// ── Conjunto FECHADO de erros da feature (definido na construção dela) ─────────
/// <summary>Erro de negócio: sem conectividade (decidido no Process).</summary>
public sealed record Offline(string Message) : AppError(Message);

/// <summary>Falha de I/O traduzida pelo Repository (timeout da fonte).</summary>
public sealed record ConnectionTimeout(string Message) : AppError(Message);

/// <summary>Os 3 erros possíveis desta feature — consumo exaustivo, sem <c>_</c>.</summary>
public readonly union CheckConnectionError(Offline, ConnectionTimeout, ErrorGeneric);

/// <summary>Descrição exaustiva do erro (escrita uma vez, pela feature).</summary>
public static class CheckConnectionErrorText
{
    public static string Describe(this CheckConnectionError error) => error switch
    {
        Offline o           => $"[negócio] {o.Message}",
        ConnectionTimeout t => $"[infra] {t.Message}",
        ErrorGeneric g      => $"[inesperado] {g.Message}",
    };
}
