namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso com fonte de dados, dependente de um <see cref="IRepository{TData, TParams, TError}"/>
/// (DIP — é isso que o torna portável: troca-se o datasource sem tocar na regra). Orquestra o
/// fluxo em três fases:
/// <list type="number">
///   <item><b>FETCH</b> — busca o dado já tratado no <see cref="IRepository{TData, TParams, TError}"/> (a fronteira já traduziu exceções em <see cref="Failure{TError}"/>).</item>
///   <item><b>CURTO-CIRCUITO</b> — se o fetch falha, retorna o erro sem chamar <see cref="Process"/>.</item>
///   <item><b>PROCESS</b> — processa o dado (CPU-bound), direto ou no thread pool conforme <see cref="UsecaseExecutorBase{TValue, TError}.RunInBackground"/>.</item>
/// </list>
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
/// <typeparam name="TData">Tipo do dado bruto carregado pela fonte.</typeparam>
/// <typeparam name="TParams">Tipo dos parâmetros (só dados) do caso de uso.</typeparam>
/// <typeparam name="TError">Conjunto fechado de erros da feature (tipicamente um <c>union</c>).</typeparam>
public abstract class UsecaseBaseCallData<TValue, TData, TParams, TError> : UsecaseExecutorBase<TValue, TError>
    where TParams : Parameters
{
    private readonly IRepository<TData, TParams, TError> _repository;

    /// <summary>Recebe o repositório (injeção por construtor) — a abstração da camada de dados.</summary>
    protected UsecaseBaseCallData(IRepository<TData, TParams, TError> repository) =>
        _repository = repository;

    /// <summary>Regra de negócio: recebe o dado bruto já carregado e os parâmetros.</summary>
    protected abstract ReturnSuccessOrError<TValue, TError> Process(
        TData data,
        TParams parameters);

    /// <summary>Executa o caso de uso (fetch → curto-circuito → process), com medição opcional.</summary>
    public Task<ReturnSuccessOrError<TValue, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => RunAsync(parameters, cancellationToken));

    private async Task<ReturnSuccessOrError<TValue, TError>> RunAsync(
        TParams parameters,
        CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH: o repositório já devolve Success|Failure (a fronteira tratou a exceção).
        var fetchResult = await _repository.CallAsync(parameters, cancellationToken).ConfigureAwait(false);

        // FASE 2 — CURTO-CIRCUITO: Failure<TError> flui entre genéricos (depende só de TError);
        //          o Success é desconstruído por pattern matching.
        // FASE 3 — PROCESS: delegado à base (direto ou background).
        return fetchResult switch
        {
            Failure<TError> failure => failure,
            Success<TData> success =>
                await ProcessStageAsync(() => Process(success.Value, parameters), cancellationToken)
                    .ConfigureAwait(false),
        };
    }
}
