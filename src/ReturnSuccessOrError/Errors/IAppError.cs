namespace ReturnSuccessOrError;

/// <summary>
/// Contrato de erro de domínio. O erro é um <b>valor</b> imutável — não uma exceção —
/// que descreve uma falha esperada e trafega entre as camadas da aplicação.
/// </summary>
public interface IAppError
{
    /// <summary>Descrição legível do erro.</summary>
    string Message { get; }

    /// <summary>
    /// Devolve uma nova instância com a mensagem substituída, preservando o
    /// tipo concreto. Implementações baseadas em <c>record</c> usam <c>with</c>.
    /// </summary>
    IAppError WithMessage(string message);
}
