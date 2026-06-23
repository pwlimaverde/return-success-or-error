namespace ReturnSuccessOrError;

/// <summary>
/// Contrato de parâmetros de um caso de uso. Carrega o <see cref="IAppError"/>
/// a ser usado caso a operação falhe — decidido pelo chamador antes da execução.
/// </summary>
public interface IParametersReturnResult
{
    /// <summary>Erro a ser usado caso a operação falhe.</summary>
    IAppError Error { get; }
}
