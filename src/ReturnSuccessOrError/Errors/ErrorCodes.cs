namespace ReturnSuccessOrError;

/// <summary>
/// Códigos de rastreio anexados às mensagens de erro pela infraestrutura da
/// biblioteca ao converter exceções em <see cref="AppError"/>. Públicos para
/// permitir asserções e filtros sem depender de strings literais.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Exceção lançada pela fonte de dados durante o fetch (fase 1).</summary>
    public const string DataSourceCatch = "DataSourceCatch";

    /// <summary>Exceção lançada por <c>Process</c> ao rodar em background (fase 3).</summary>
    public const string BackgroundCatch = "BackgroundCatch";
}
