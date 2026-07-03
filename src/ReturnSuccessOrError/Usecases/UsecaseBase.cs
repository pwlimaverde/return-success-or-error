namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso de lógica pura (sem fonte de dados externa). A subclasse implementa
/// <see cref="Process"/> (regra) e <c>OnUnexpected</c> (mapeamento do inesperado); a base
/// orquestra medição e despacho opcional ao thread pool (ver <see cref="UsecaseExecutorBase{TValue, TError}"/>).
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
/// <typeparam name="TParams">Tipo dos parâmetros (só dados) do caso de uso.</typeparam>
/// <typeparam name="TError">Conjunto fechado de erros da feature (tipicamente um <c>union</c>).</typeparam>
public abstract class UsecaseBase<TValue, TParams, TError> : UsecaseExecutorBase<TValue, TError>
    where TParams : Parameters
{
    /// <summary>
    /// Regra de negócio implementada pela subclasse. Recebe o <paramref name="cancellationToken"/>
    /// do chamador para cancelamento cooperativo em processamento longo (cheque-o em pontos
    /// convenientes via <c>ThrowIfCancellationRequested()</c>; ignorá-lo é válido em regras curtas).
    /// </summary>
    protected abstract ReturnSuccessOrError<TValue, TError> Process(
        TParams parameters,
        CancellationToken cancellationToken);

    /// <summary>Executa o caso de uso (processamento direto ou em background, com medição opcional).</summary>
    public Task<ReturnSuccessOrError<TValue, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => ProcessStageAsync(() => Process(parameters, cancellationToken), cancellationToken));
}
