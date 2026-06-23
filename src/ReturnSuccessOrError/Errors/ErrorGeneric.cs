namespace ReturnSuccessOrError;

/// <summary>
/// Implementação concreta e genérica de <see cref="IAppError"/>. Use quando não
/// houver necessidade de um tipo de erro de domínio específico.
/// </summary>
public sealed record ErrorGeneric(string Message) : IAppError
{
    /// <inheritdoc />
    public IAppError WithMessage(string message) => this with { Message = message };

    /// <inheritdoc />
    public override string ToString() => $"{nameof(ErrorGeneric)} - {Message}";
}
