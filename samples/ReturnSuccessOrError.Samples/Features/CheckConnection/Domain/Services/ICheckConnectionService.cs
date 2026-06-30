namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>Contrato do service da feature (Service Layer) — ponto de entrada público.</summary>
public interface ICheckConnectionService
{
    Task<ReturnSuccessOrError<string, CheckConnectionError>> CheckAsync(
        CancellationToken cancellationToken = default);
}
