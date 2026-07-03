namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>DataSource burro: devolve o estado de conectividade (bool) ou lança exceção técnica.</summary>
public sealed class FakeConnectivityDataSource(bool online, bool shouldThrow = false)
    : IDataSource<bool, NoParams>
{
    public Task<bool> CallAsync(
        NoParams parameters, CancellationToken cancellationToken = default)
    {
        if (shouldThrow)
            throw new TimeoutException("Tempo esgotado ao verificar conectividade");
        return Task.FromResult(online);
    }
}
