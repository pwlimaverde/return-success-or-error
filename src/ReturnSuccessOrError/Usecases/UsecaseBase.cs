namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso de lógica pura (sem fonte de dados externa). A subclasse implementa
/// apenas <see cref="Process"/>; a base orquestra medição e despacho opcional ao
/// thread pool (ver <see cref="UsecaseExecutorBase{TValue}"/>).
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
public abstract class UsecaseBase<TValue> : UsecaseExecutorBase<TValue>
{
    /// <summary>Regra de negócio implementada pela subclasse.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(ParametersReturnResult parameters);

    /// <summary>Executa o caso de uso (processamento direto ou em background, com medição opcional).</summary>
    public Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => ProcessStageAsync(() => Process(parameters), parameters, cancellationToken));
}
