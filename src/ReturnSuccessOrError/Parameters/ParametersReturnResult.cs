namespace ReturnSuccessOrError;

/// <summary>
/// Parâmetros de um caso de uso como <b>valor</b> imutável. Carrega o <see cref="AppError"/>
/// a ser usado caso a operação falhe — decidido pelo chamador antes da execução. Por ser um
/// <c>record</c> abstrato, todo parâmetro concreto é também um <c>record</c>.
/// </summary>
/// <param name="Error">Erro a ser usado caso a operação falhe.</param>
public abstract record ParametersReturnResult(AppError Error);
