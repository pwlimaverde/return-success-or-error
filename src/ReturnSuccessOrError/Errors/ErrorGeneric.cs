namespace ReturnSuccessOrError;

/// <summary>
/// Implementação concreta e genérica de <see cref="AppError"/>. Use quando não
/// houver necessidade de um tipo de erro de domínio específico. Herda
/// <see cref="AppError.WithMessage(string)"/> da base — não reimplementa nada.
/// </summary>
public sealed record ErrorGeneric(string Message) : AppError(Message)
{
    /// <inheritdoc />
    public override string ToString() => $"{nameof(ErrorGeneric)} - {Message}";
}
