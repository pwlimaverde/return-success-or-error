namespace ReturnSuccessOrError;

/// <summary>
/// Representa a ausência de valor de retorno (operação bem-sucedida sem payload).
/// Singleton — <c>void</c> não é um argumento genérico válido em C#.
/// </summary>
public sealed class Unit
{
    /// <summary>Instância única.</summary>
    public static readonly Unit Value = new();

    private Unit() { }

    /// <inheritdoc />
    public override string ToString() => "Unit - void";
}
