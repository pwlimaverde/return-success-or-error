namespace ReturnSuccessOrError;

/// <summary>
/// Enriquecimento interno de erros na fronteira de captura de exceções. Centraliza o
/// formato da mensagem de rastreio para que <see cref="ErrorCodes.DataSourceCatch"/> e
/// <see cref="ErrorCodes.BackgroundCatch"/> sejam anexados de forma consistente.
/// </summary>
internal static class ErrorTrace
{
    /// <summary>
    /// Devolve uma cópia do erro com a mensagem enriquecida por um código de rastreio e os
    /// detalhes da exceção capturada — preservando o tipo concreto (via <see cref="AppError.WithMessage"/>).
    /// </summary>
    public static AppError WithCatch(this AppError error, string code, Exception exception) =>
        error.WithMessage($"{error.Message} - Cod. {code} --- Catch: {exception}");
}
