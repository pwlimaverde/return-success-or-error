namespace ReturnSuccessOrError.Samples.Features.Fibonacci;

/// <summary>Erro de negócio: entrada inválida.</summary>
public sealed record NegativeInput(string Message) : AppError(Message);

/// <summary>Conjunto fechado de erros da feature.</summary>
public readonly union FibonacciError(NegativeInput, ErrorGeneric);

/// <summary>Descrição exaustiva do erro.</summary>
public static class FibonacciErrorText
{
    public static string Describe(this FibonacciError error) => error switch
    {
        NegativeInput n => $"[negócio] {n.Message}",
        ErrorGeneric g  => $"[inesperado] {g.Message}",
    };
}
