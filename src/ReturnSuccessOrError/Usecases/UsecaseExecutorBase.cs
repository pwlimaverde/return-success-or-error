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

    /// <summary>Se verdadeiro, mede o tempo de execução e o entrega a <see cref="OnExecutionTimeMeasured"/>.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>
    /// Converte uma exceção <b>inesperada</b> do <c>Process</c> (um bug) num erro do conjunto
    /// fechado da feature. <b>Abstrato</b>: como não há erro universal a fabricar, o caso de uso
    /// decide para qual caso de <typeparamref name="TError"/> o inesperado é mapeado (tipicamente
    /// um caso "Unexpected"/<see cref="ErrorGeneric"/>). Garante que o <c>Process</c> nunca lança
    /// ao chamador — o resultado é sempre um dos casos previstos.
    /// <para>
    /// <b>Exceção do contrato — cancelamento:</b> um <see cref="OperationCanceledException"/>
    /// causado pelo <b>token do chamador</b> não passa por aqui: cancelamento não é falha de
    /// domínio e propaga como exceção, no idioma do .NET.
    /// </para>
    /// </summary>
    protected abstract TError OnUnexpected(Exception exception);

    /// <summary>
    /// Recebe o tempo medido quando <see cref="MonitorExecutionTime"/> está habilitado.
    /// <b>Virtual</b>: sobrescreva para integrar à sua observabilidade (ex.: <c>ILogger</c>) —
    /// a base não impõe dependência de logging. A implementação padrão escreve em
    /// <see cref="Trace"/> (ativa também no binário Release do pacote; <c>Debug.WriteLine</c>
    /// seria removido na compilação da biblioteca e nunca chegaria ao consumidor do NuGet).
    /// </summary>
    /// <param name="elapsed">Duração total da execução do caso de uso.</param>
    protected virtual void OnExecutionTimeMeasured(TimeSpan elapsed) =>
        Trace.WriteLine($"[ReturnSuccessOrError] Execution Time {GetType().Name} " +
                        $"({(RunInBackground ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");

    /// <summary>
    /// Cria um resultado de <b>falha</b> a partir de um caso concreto do <c>union</c> de erro,
    /// <b>sem o cast do "duplo salto"</b>. Como o parâmetro já é <typeparamref name="TError"/>
    /// (fixado por esta base, não inferido), passar um caso concreto exige uma única conversão de
    /// <c>union</c> — no contexto do argumento — em vez das duas conversões implícitas encadeadas
    /// que o C# proíbe (caso → <c>union</c> de erro → <c>Failure</c>). É a forma <b>recomendada</b>
    /// de retornar um erro de negócio no <c>Process</c>. Ver PRD §5.2.
    /// </summary>
    protected static ReturnSuccessOrError<TValue, TError> Fail(TError error) => new Failure<TError>(error);

    /// <summary>
    /// Cria um resultado de <b>sucesso</b> a partir do valor. Conveniência <b>opcional</b> e
    /// simétrica a <see cref="Fail"/>: o caminho feliz nunca sofre do "duplo salto" (<typeparamref name="TValue"/>
    /// é o tipo concreto direto), então <c>return value;</c> continua igualmente idiomático.
    /// </summary>
    protected static ReturnSuccessOrError<TValue, TError> Ok(TValue value) => new Success<TValue>(value);

    /// <summary>Envolve a execução com a medição de tempo, quando habilitada (sem alocação).</summary>
    private protected async Task<ReturnSuccessOrError<TValue, TError>> MeasuredAsync(
        Func<Task<ReturnSuccessOrError<TValue, TError>>> run)
    {
        if (!MonitorExecutionTime)
            return await run().ConfigureAwait(false);

        long startTimestamp = Stopwatch.GetTimestamp();
        ReturnSuccessOrError<TValue, TError> result = await run().ConfigureAwait(false);
        OnExecutionTimeMeasured(Stopwatch.GetElapsedTime(startTimestamp));
        return result;
    }

    /// <summary>
    /// Executa o <paramref name="process"/> direto na thread chamadora ou, se
    /// <see cref="RunInBackground"/>, no thread pool. Em <b>ambos</b> os modos, uma exceção
    /// inesperada é convertida via <see cref="OnUnexpected"/> em <see cref="Failure{TError}"/> —
    /// o <c>Process</c> nunca propaga exceção ao chamador. Única exceção: o <b>cancelamento do
    /// chamador</b> (token cancelado) propaga como <see cref="OperationCanceledException"/> em
    /// ambos os modos — cancelamento não é falha de domínio.
    /// </summary>
    private protected Task<ReturnSuccessOrError<TValue, TError>> ProcessStageAsync(
        Func<ReturnSuccessOrError<TValue, TError>> process,
        CancellationToken cancellationToken)
    {
        // Paridade direto↔background: token já cancelado interrompe ANTES do Process, nos dois modos.
        cancellationToken.ThrowIfCancellationRequested();

        if (!RunInBackground)
        {
            try { return Task.FromResult(process()); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // cancelamento cooperativo do chamador — não é um "inesperado"
            }
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // cancelamento cooperativo do chamador — não é um "inesperado"
            }
            catch (Exception ex) { return OnUnexpected(ex); }
        }, cancellationToken);
    }
}
