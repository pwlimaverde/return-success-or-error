namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso com fonte de dados. Orquestra o fluxo em três fases:
/// <list type="number">
///   <item><b>FETCH</b> — busca o dado bruto na <see cref="IDataSource{TData}"/> (I/O-bound, no contexto da chamada).</item>
///   <item><b>CURTO-CIRCUITO</b> — se o fetch falha, retorna o erro sem chamar <see cref="Process"/>.</item>
///   <item><b>PROCESS</b> — processa o dado (CPU-bound), direto ou no thread pool conforme <see cref="UsecaseExecutorBase{TValue}.RunInBackground"/>.</item>
/// </list>
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
/// <typeparam name="TData">Tipo do dado bruto carregado pela fonte.</typeparam>
public abstract class UsecaseBaseCallData<TValue, TData> : UsecaseExecutorBase<TValue>
{
    private readonly IDataSource<TData> _dataSource;

    /// <summary>Recebe a fonte de dados (injeção por construtor).</summary>
    protected UsecaseBaseCallData(IDataSource<TData> dataSource) =>
        _dataSource = dataSource;

    /// <summary>Regra de negócio: recebe o dado bruto já carregado e os parâmetros.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(
        TData data,
        ParametersReturnResult parameters);

    /// <summary>Executa o caso de uso (fetch → curto-circuito → process), com medição opcional.</summary>
    public Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => RunAsync(parameters, cancellationToken));

    private async Task<ReturnSuccessOrError<TValue>> RunAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH (no contexto da chamada; I/O-bound)
        var fetchResult = await FetchAsync(parameters, cancellationToken).ConfigureAwait(false);

        // FASE 2 — CURTO-CIRCUITO: switch exaustivo. Failure flui entre genéricos; o Success é
        //          desconstruído por pattern matching (cast direto não é permitido em union).
        // FASE 3 — PROCESS: delegado à base (direto ou background).
        return fetchResult switch
        {
            Failure failure => failure,
            Success<TData> success =>
                await ProcessStageAsync(() => Process(success.Value, parameters), parameters, cancellationToken)
                    .ConfigureAwait(false),
        };
    }

    private async Task<ReturnSuccessOrError<TData>> FetchAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            // TData -> Success (conversão implícita)
            return await _dataSource.CallAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // AppError -> Failure (conversão implícita)
            return parameters.Error.WithCatch(ErrorCodes.DataSourceCatch, ex);
        }
    }
}
