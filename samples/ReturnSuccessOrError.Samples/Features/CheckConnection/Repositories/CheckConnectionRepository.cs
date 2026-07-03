namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>Repository (fronteira): traduz a exceção técnica num dos erros do union (MapError abstrato).</summary>
public sealed class CheckConnectionRepository(IDataSource<bool, NoParams> ds)
    : RepositoryBase<bool, NoParams, CheckConnectionError>(ds)
{
    protected override CheckConnectionError MapError(Exception exception, NoParams parameters) =>
        exception switch
        {
            TimeoutException => new ConnectionTimeout("Sem conexão (timeout)"),
            _                => new ErrorGeneric($"Falha inesperada: {exception.Message}"),
        };
}
