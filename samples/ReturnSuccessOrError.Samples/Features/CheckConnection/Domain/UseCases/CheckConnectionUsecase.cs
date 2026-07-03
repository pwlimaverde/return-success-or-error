namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>UseCase (regra de negócio, PORTÁVEL): mapeia o estado em mensagem ou erro de negócio.</summary>
public sealed class CheckConnectionUsecase(IRepository<bool, NoParams, CheckConnectionError> repo)
    : UsecaseBaseCallData<string, bool, NoParams, CheckConnectionError>(repo)
{
    protected override ReturnSuccessOrError<string, CheckConnectionError> Process(
        bool online, NoParams p, CancellationToken cancellationToken)
    {
        if (!online)
            return Fail(new Offline("You are offline")); // erro de negócio -> Failure (factory, sem cast)
        return Ok("You are connected");                  // string -> Success
    }

    protected override CheckConnectionError OnUnexpected(Exception exception)
        => new ErrorGeneric($"Bug no processamento: {exception.Message}");
}
