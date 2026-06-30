namespace ReturnSuccessOrError;

/// <summary>
/// Parâmetros vazios, para casos de uso que não exigem entrada. Por carregar apenas
/// dados (e não ter nenhum), é um singleton imutável reutilizável via <see cref="Value"/>.
/// </summary>
public sealed record NoParams : Parameters
{
    private NoParams() { }

    /// <summary>Instância compartilhada de parâmetros vazios.</summary>
    public static NoParams Value { get; } = new();
}
