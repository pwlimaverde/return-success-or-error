namespace ReturnSuccessOrError;

/// <summary>
/// Contrato da camada de dados (fronteira / <i>anti-corruption layer</i>). Diferente da
/// <see cref="IDataSource{TData, TParams}"/> burra, o repositório <b>nunca lança</b>: devolve
/// sempre um <see cref="ReturnSuccessOrError{TValue, TError}"/> — o dado bruto como <see cref="Success{TValue}"/>
/// ou a exceção de infraestrutura já traduzida num dos erros do conjunto fechado da feature
/// (<typeparamref name="TError"/>) como <see cref="Failure{TError}"/>. É a abstração da qual o
/// caso de uso depende (DIP), o que o torna portável.
/// </summary>
/// <typeparam name="TData">Tipo do dado bruto entregue à camada de domínio.</typeparam>
/// <typeparam name="TParams">Tipo dos parâmetros (só dados) da chamada.</typeparam>
/// <typeparam name="TError">Conjunto fechado de erros da feature (tipicamente um <c>union</c>).</typeparam>
public interface IRepository<TData, TParams, TError>
    where TParams : Parameters
{
    /// <summary>
    /// Busca o dado e devolve-o como resultado já tratado — <see cref="Success{TValue}"/> com o
    /// dado bruto ou <see cref="Failure{TError}"/> com o erro de domínio traduzido da fonte.
    /// </summary>
    Task<ReturnSuccessOrError<TData, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default);
}
