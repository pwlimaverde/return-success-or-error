using System.Diagnostics;

namespace ReturnSuccessOrError;

/// <summary>
/// Base compartilhada pelos casos de uso. Concentra o que é comum aos dois fluxos
/// (<see cref="UsecaseBase{TValue}"/> e <see cref="UsecaseBaseCallData{TValue, TData}"/>):
/// a medição opcional do tempo e o despacho opcional do processamento (CPU-bound) ao
/// thread pool. As subclasses definem o fluxo específico e implementam o <c>Process</c>.
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
public abstract class UsecaseExecutorBase<TValue>
{
    /// <summary>Se verdadeiro, o processamento (CPU-bound) roda no thread pool (Task.Run).</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Se verdadeiro, mede e registra o tempo de execução.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>Envolve a execução com a medição de tempo, quando habilitada (sem alocação).</summary>
    private protected async Task<ReturnSuccessOrError<TValue>> MeasuredAsync(
        Func<Task<ReturnSuccessOrError<TValue>>> run)
    {
        if (!MonitorExecutionTime)
            return await run().ConfigureAwait(false);

        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await run().ConfigureAwait(false);
        LogTime(GetType().Name, Stopwatch.GetElapsedTime(startTimestamp), RunInBackground);
        return result;
    }

    /// <summary>
    /// Executa o <paramref name="process"/> direto na thread chamadora ou, se
    /// <see cref="RunInBackground"/>, no thread pool — caso em que uma exceção vira
    /// <see cref="ErrorCodes.BackgroundCatch"/>. No modo direto, a exceção propaga ao chamador.
    /// </summary>
    private protected Task<ReturnSuccessOrError<TValue>> ProcessStageAsync(
        Func<ReturnSuccessOrError<TValue>> process,
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        if (!RunInBackground)
            return Task.FromResult(process());

        // Task.Run<...> anotado fixa o tipo do resultado, habilitando a conversão
        // implícita AppError -> Failure no braço de catch.
        return Task.Run<ReturnSuccessOrError<TValue>>(() =>
        {
            try { return process(); }
            catch (Exception ex)
            {
                return parameters.Error.WithCatch(ErrorCodes.BackgroundCatch, ex);
            }
        }, cancellationToken);
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
