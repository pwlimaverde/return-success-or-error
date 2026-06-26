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

        // FASE 2 — CURTO-CIRCUITO no erro
        if (fetchResult is ReturnSuccessOrError<TData>.Failure failure)
            return ReturnSuccessOrError<TValue>.Err(failure.Error);

        var data = ((ReturnSuccessOrError<TData>.Success)fetchResult).Value;

        // FASE 3 — PROCESS (direto ou em background; CPU-bound)
        if (!RunInBackground)
            return Process(data, parameters);

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
