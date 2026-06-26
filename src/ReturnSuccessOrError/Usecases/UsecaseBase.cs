using System.Diagnostics;

namespace ReturnSuccessOrError;

/// <summary>
/// Caso de uso de lógica pura (sem fonte de dados externa). A subclasse implementa
/// apenas <see cref="Process"/>; a base orquestra medição e despacho opcional ao
/// thread pool.
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
public abstract class UsecaseBase<TValue>
{
    /// <summary>Se verdadeiro, o processamento roda no thread pool (Task.Run).</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Se verdadeiro, mede e registra o tempo de execução.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>Regra de negócio implementada pela subclasse.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(
        ParametersReturnResult parameters);

    /// <summary>Executa o caso de uso.</summary>
    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunStageAsync(parameters, cancellationToken).ConfigureAwait(false);

        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await RunStageAsync(parameters, cancellationToken).ConfigureAwait(false);
        LogTime(GetType().Name, Stopwatch.GetElapsedTime(startTimestamp), RunInBackground);
        return result;
    }

    private Task<ReturnSuccessOrError<TValue>> RunStageAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        if (!RunInBackground)
            return Task.FromResult(Process(parameters));

        return Task.Run(() =>
        {
            try { return Process(parameters); }
            catch (Exception ex)
            {
                return ReturnSuccessOrError<TValue>.Err(
                    parameters.Error.WithMessage(
                        $"{parameters.Error.Message} - Cod. {ErrorCodes.BackgroundCatch} --- Catch: {ex}"));
            }
        }, cancellationToken);
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
