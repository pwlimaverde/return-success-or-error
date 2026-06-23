namespace ReturnSuccessOrError;

/// <summary>
/// Parâmetros vazios, para casos de uso que não exigem entrada. Fornece um
/// <see cref="IAppError"/> default não-nulo quando nenhum erro é especificado.
/// </summary>
public sealed record NoParams(IAppError? Error = null) : IParametersReturnResult
{
    // Implementação explícita de interface: garante Error não-nulo sem
    // alterar a semântica nullable do parâmetro posicional do record.
    IAppError IParametersReturnResult.Error =>
        Error ?? new ErrorGeneric("NoParams: unspecified generic error");
}
