namespace ReturnSuccessOrError;

/// <summary>
/// Erro de domínio como <b>valor</b> imutável — não uma exceção. Descreve uma falha
/// esperada e trafega entre as camadas da aplicação. Por ser um <c>record</c> abstrato,
/// todo erro concreto é também um <c>record</c>: imutável e com igualdade por valor.
/// <para>
/// <b>Base opcional dos erros da feature:</b> herdar de <see cref="AppError"/> dá aos seus
/// records de erro um <c>Message</c> comum, igualdade por valor e <see cref="WithMessage"/>.
/// Os erros concretos de cada feature (ex.: <c>InvalidCredentials</c>, <c>AccountLocked</c>)
/// são então agrupados num <c>union</c> fechado usado como <c>TError</c> em
/// <see cref="ReturnSuccessOrError{TValue, TError}"/>, dando consumo exaustivo. <see cref="ErrorGeneric"/>
/// é um caso pronto para o "inesperado" (alvo típico de <c>OnUnexpected</c>). Herdar de
/// <see cref="AppError"/> é conveniência, não obrigação — <c>TError</c> pode ser qualquer tipo.
/// </para>
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
