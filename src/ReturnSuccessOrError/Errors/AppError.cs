namespace ReturnSuccessOrError;

/// <summary>
/// Erro de domínio como <b>valor</b> imutável — não uma exceção. Descreve uma falha
/// esperada e trafega entre as camadas da aplicação. Por ser um <c>record</c> abstrato,
/// todo erro concreto é também um <c>record</c>: imutável e com igualdade por valor.
/// </summary>
/// <param name="Message">Descrição legível do erro.</param>
public abstract record AppError(string Message)
{
    /// <summary>
    /// Devolve uma nova instância com a mensagem substituída, <b>preservando o tipo
    /// concreto</b> e os demais campos. Implementado uma única vez aqui: o operador
    /// <c>with</c> usa o clone virtual do <c>record</c>, que despacha para o subtipo real.
    /// </summary>
    public AppError WithMessage(string message) => this with { Message = message };
}
