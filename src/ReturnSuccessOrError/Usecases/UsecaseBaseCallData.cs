using System.Diagnostics;

namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso com fonte de dados. Orquestra o fluxo em três fases:
/// <list type="number">
///   <item><b>FETCH</b> — busca o dado bruto na <see cref="IDataSource{TData}"/> (I/O-bound, no contexto da chamada).</item>
///   <item><b>CURTO-CIRCUITO</b> — se o fetch falha, retorna o erro sem chamar <see cref="Process"/>.</item>
///   <item><b>PROCESS</b> — processa o dado (CPU-bound), direto ou no thread pool conforme <see cref="RunInBackground"/>.</item>
/// </list>
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
/// <typeparam name="TData">Tipo do dado bruto carregado pela fonte.</typeparam>
public abstract class UsecaseBaseCallData<TValue, TData>
{
    private readonly IDataSource<TData> _dataSource;

    /// <summary>Afeta SOMENTE o processamento; a busca de dados nunca vai para background.</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Mede busca + processamento.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>Recebe a fonte de dados (injeção por construtor).</summary>
    protected UsecaseBaseCallData(IDataSource<TData> dataSource) =>
        _dataSource = dataSource;

    /// <summary>Regra de negócio: recebe o dado bruto já carregado e os parâmetros.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(
        TData data,
        ParametersReturnResult parameters);

    /// <summary>Executa o caso de uso (fetch → curto-circuito → process).</summary>
    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunAsync(parameters, cancellationToken).ConfigureAwait(false);

        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await RunAsync(parameters, cancellationToken).ConfigureAwait(false);
        LogTime(GetType().Name, Stopwatch.GetElapsedTime(startTimestamp), RunInBackground);
        return result;
    }

    private async Task<ReturnSuccessOrError<TValue>> RunAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH (no contexto da chamada; I/O-bound)
        var fetchResult = await FetchAsync(parameters, cancellationToken).ConfigureAwait(false);

        // FASE 2 — CURTO-CIRCUITO: se falhou, propaga o erro (Failure flui entre genéricos);
        //          senão, segue para a FASE 3 — PROCESS. O switch é exaustivo (união fechada).
        return fetchResult switch
        {
            Failure failure => failure,
            Success<TData> success =>
                await ProcessStageAsync(success.Value, parameters, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<ReturnSuccessOrError<TValue>> ProcessStageAsync(
        TData data,
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        // PROCESS direto (CPU-bound na thread chamadora)...
        if (!RunInBackground)
            return Process(data, parameters);

        // ...ou despachado ao thread pool. Só o background converte exceção em Failure.
        return await Task.Run(() =>
        {
            try { return Process(data, parameters); }
            catch (Exception ex)
            {
                return ReturnSuccessOrError<TValue>.Err(
                    parameters.Error.WithMessage(
                        $"{parameters.Error.Message} - Cod. {ErrorCodes.BackgroundCatch} --- Catch: {ex}"));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReturnSuccessOrError<TData>> FetchAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await _dataSource.CallAsync(parameters, cancellationToken).ConfigureAwait(false);
            return ReturnSuccessOrError<TData>.Ok(data);
        }
        catch (Exception ex)
        {
            return ReturnSuccessOrError<TData>.Err(
                parameters.Error.WithMessage(
                    $"{parameters.Error.Message} - Cod. {ErrorCodes.DataSourceCatch} --- Catch: {ex}"));
        }
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
