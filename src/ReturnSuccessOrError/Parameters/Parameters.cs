namespace ReturnSuccessOrError;

/// <summary>
/// Parâmetros de um caso de uso como <b>valor</b> imutável — carrega <b>apenas dados</b>.
/// Atravessa as três camadas (DataSource → Repository → UseCase) sem nunca carregar o erro:
/// o tratamento de falha é decidido por camada (o <see cref="RepositoryBase{TData, TParams, TError}"/>
/// traduz exceções via <c>MapError</c>; o <c>Process</c> devolve erros de negócio). Por ser um
/// <c>record</c> abstrato, todo parâmetro concreto é também um <c>record</c>.
/// </summary>
public abstract record Parameters;
