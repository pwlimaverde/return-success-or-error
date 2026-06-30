using System.Diagnostics;

namespace ReturnSuccessOrError;

/// <summary>
/// Base compartilhada pelos casos de uso. Concentra o que é comum aos dois fluxos
/// (<see cref="UsecaseBase{TValue, TParams, TError}"/> e <see cref="UsecaseBaseCallData{TValue, TData, TParams, TError}"/>):
/// a medição opcional do tempo e o despacho opcional do processamento (CPU-bound) ao thread pool.
/// As subclasses definem o fluxo específico e implementam o <c>Process</c>.
/// </summary>
/// <typeparam name="TValue">Tipo do valor de sucesso.</typeparam>
/// <typeparam name="TError">Conjunto fechado de erros da feature (tipicamente um <c>union</c>).</typeparam>
public abstract class UsecaseExecutorBase<TValue, TError>
{
    /// <summary>Se verdadeiro, o processamento (CPU-bound) roda no thread pool (Task.Run).</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Se verdadeiro, mede e registra o tempo de execução.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>
    /// Converte uma exceção <b>inesperada</b> do <c>Process</c> (um bug) num erro do conjunto
    /// fechado da feature. <b>Abstrato</b>: como não há erro universal a fabricar, o caso de uso
    /// decide para qual caso de <typeparamref name="TError"/> o inesperado é mapeado (tipicamente
    /// um caso "Unexpected"/<see cref="ErrorGeneric"/>). Garante que o <c>Process</c> nunca lança
    /// ao chamador — o resultado é sempre um dos casos previstos.
    /// </summary>
    protected abstract TError OnUnexpected(Exception exception);

    /// <summary>Envolve a execução com a medição de tempo, quando habilitada (sem alocação).</summary>
    private protected async Task<ReturnSuccessOrError<TValue, TError>> MeasuredAsync(
        Func<Task<ReturnSuccessOrError<TValue, TError>>> run)
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
    /// <see cref="RunInBackground"/>, no thread pool. Em <b>ambos</b> os modos, uma exceção
    /// inesperada é convertida via <see cref="OnUnexpected"/> em <see cref="Failure{TError}"/> —
    /// o <c>Process</c> nunca propaga exceção ao chamador.
    /// </summary>
    private protected Task<ReturnSuccessOrError<TValue, TError>> ProcessStageAsync(
        Func<ReturnSuccessOrError<TValue, TError>> process,
        CancellationToken cancellationToken)
    {
        if (!RunInBackground)
        {
            try { return Task.FromResult(process()); }
            catch (Exception ex)
            {
                return Task.FromResult<ReturnSuccessOrError<TValue, TError>>(OnUnexpected(ex));
            }
        }

        // Task.Run<...> anotado fixa o tipo do resultado, habilitando a conversão
        // implícita TError -> Failure no braço de catch.
        return Task.Run<ReturnSuccessOrError<TValue, TError>>(() =>
        {
            try { return process(); }
            catch (Exception ex) { return OnUnexpected(ex); }
        }, cancellationToken);
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
