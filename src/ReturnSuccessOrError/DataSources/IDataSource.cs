namespace ReturnSuccessOrError;

/// <summary>
/// Contrato de fonte de dados (port de infraestrutura). Executa a chamada externa
/// (I/O-bound) e devolve o dado bruto. Em caso de falha, <b>lança</b> uma exceção;
/// a classe base de caso de uso a captura e a converte em <see cref="ReturnSuccessOrError{TValue}.Failure"/>.
/// </summary>
/// <typeparam name="TData">Tipo do dado bruto retornado pela fonte.</typeparam>
public interface IDataSource<TData>
{
    /// <summary>
    /// Executa a chamada externa e devolve o dado bruto, ou lança o
    /// <see cref="IAppError"/> dos parâmetros (como exceção) em caso de falha.
    /// </summary>
    Task<TData> CallAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken = default);
}
