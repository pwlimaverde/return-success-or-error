namespace ReturnSuccessOrError;

/// <summary>
/// Parâmetros vazios, para casos de uso que não exigem entrada. Fornece um
/// <see cref="AppError"/> default não-nulo quando nenhum erro é especificado.
/// </summary>
public sealed record NoParams : ParametersReturnResult
{
    /// <summary>Cria parâmetros vazios; sem erro informado, usa um <see cref="ErrorGeneric"/> default.</summary>
    public NoParams(AppError? error = null)
        : base(error ?? new ErrorGeneric("NoParams: unspecified generic error")) { }
}
