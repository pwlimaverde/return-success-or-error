namespace ReturnSuccessOrError.Samples.Features.CheckConnection;

/// <summary>UseCase (regra de negócio, PORTÁVEL): mapeia o estado em mensagem ou erro de negócio.</summary>
public sealed class CheckConnectionUsecase(IRepository<bool, CheckConnectionParameters, CheckConnectionError> repo)
    : UsecaseBaseCallData<string, bool, CheckConnectionParameters, CheckConnectionError>(repo)
{
    protected override ReturnSuccessOrError<string, CheckConnectionError> Process(
        bool online, CheckConnectionParameters p)
    {
        if (!online)
            return (CheckConnectionError)new Offline("You are offline"); // erro de negócio -> Failure
        return "You are connected";                                      // string -> Success
    }

    protected override CheckConnectionError OnUnexpected(Exception exception)
        => new ErrorGeneric($"Bug no processamento: {exception.Message}");
}
