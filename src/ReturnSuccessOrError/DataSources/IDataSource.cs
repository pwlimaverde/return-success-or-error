namespace ReturnSuccessOrError;

/// <summary>
/// Contrato de fonte de dados (port de infraestrutura). É a camada <b>burra</b>: executa a
/// chamada externa (I/O-bound) e devolve o <b>dado bruto</b>, <b>ou lança</b> uma exceção
/// técnica em caso de falha. Não tem nenhum conhecimento de domínio — não traduz erros.
/// A tradução de exceção técnica num erro de domínio é responsabilidade da fronteira
/// (<see cref="RepositoryBase{TData, TParams, TError}"/>, via <c>MapError</c>).
/// </summary>
/// <typeparam name="TData">Tipo do dado bruto retornado pela fonte.</typeparam>
/// <typeparam name="TParams">Tipo dos parâmetros (só dados) da chamada.</typeparam>
public interface IDataSource<TData, TParams>
    where TParams : Parameters
{
    /// <summary>
    /// Executa a chamada externa e devolve o dado bruto, ou <b>lança</b> uma exceção técnica
    /// em caso de falha — a fronteira (<see cref="RepositoryBase{TData, TParams, TError}"/>) a captura
    /// e a traduz em <see cref="Failure{TError}"/>.
    /// </summary>
    Task<TData> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default);
}
