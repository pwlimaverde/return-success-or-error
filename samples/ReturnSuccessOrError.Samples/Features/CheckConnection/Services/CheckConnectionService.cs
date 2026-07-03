namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>Service da feature (Service Layer) — implementação do contrato público.</summary>
public sealed class CheckConnectionService(CheckConnectionUsecase usecase) : ICheckConnectionService
{
    public Task<ReturnSuccessOrError<string, CheckConnectionError>> CheckAsync(
        CancellationToken cancellationToken = default)
        => usecase.CallAsync(NoParams.Value, cancellationToken); // sem entrada -> singleton NoParams
}
