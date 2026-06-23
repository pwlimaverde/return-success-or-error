namespace ReturnSuccessOrError;

/// <summary>
/// Representa <c>null</c> como resultado válido e esperado. Distingue "null é o
/// resultado correto" de "ausência de valor por erro". Singleton.
/// </summary>
public sealed class Nil
{
    /// <summary>Instância única.</summary>
    public static readonly Nil Value = new();

    private Nil() { }

    /// <inheritdoc />
    public override string ToString() => "Nil - null";
}
