namespace ReturnSuccessOrError;

/// <summary>
/// Base da camada de dados: a <b>fronteira</b> (anti-corruption layer) entre a infraestrutura
/// burra (<see cref="IDataSource{TData, TParams}"/>) e o domínio. Chama a fonte e, na captura,
/// traduz a exceção técnica num dos erros do conjunto fechado da feature (<typeparamref name="TError"/>)
/// via <see cref="MapError"/> — convertendo o resultado em <see cref="Success{TValue}"/> (dado bruto)
/// ou <see cref="Failure{TError}"/> (erro de domínio).
/// <para>
/// <see cref="MapError"/> é <b>abstrato</b>: o repositório é obrigado a mapear toda exceção para
/// um dos erros previstos. Como o consumo é exaustivo sobre <typeparamref name="TError"/>, isso
/// garante que tudo que o repositório pode produzir é contemplado no tratamento final.
/// </para>
/// </summary>
/// <typeparam name="TData">Tipo do dado bruto entregue à camada de domínio.</typeparam>
/// <typeparam name="TParams">Tipo dos parâmetros (só dados) da chamada.</typeparam>
/// <typeparam name="TError">Conjunto fechado de erros da feature (tipicamente um <c>union</c>).</typeparam>
public abstract class RepositoryBase<TData, TParams, TError> : IRepository<TData, TParams, TError>
    where TParams : Parameters
{
    private readonly IDataSource<TData, TParams> _dataSource;

    /// <summary>Recebe a fonte de dados (injeção por construtor).</summary>
    protected RepositoryBase(IDataSource<TData, TParams> dataSource) =>
        _dataSource = dataSource;

    /// <summary>
    /// Chama a fonte e devolve o resultado já tratado. A exceção técnica eventualmente lançada
    /// pela fonte é capturada e traduzida por <see cref="MapError"/> — a fronteira nunca propaga
    /// exceção de infraestrutura ao domínio.
    /// </summary>
    public async Task<ReturnSuccessOrError<TData, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TData -> Success (conversão implícita)
            return await _dataSource.CallAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // TError -> Failure (conversão implícita)
            return MapError(exception, parameters);
        }
    }

    /// <summary>
    /// Traduz uma exceção da fonte de dados num erro do conjunto fechado da feature. <b>Abstrato</b>:
    /// o repositório define o mapeamento de toda exceção para um dos erros previstos (o <c>switch</c>
    /// interno costuma ter um braço <c>_</c> que cai num caso "inesperado" do <typeparamref name="TError"/>).
    /// </summary>
    /// <param name="exception">Exceção técnica lançada pela fonte de dados.</param>
    /// <param name="parameters">Parâmetros da chamada (contexto para o mapeamento).</param>
    protected abstract TError MapError(Exception exception, TParams parameters);
}
