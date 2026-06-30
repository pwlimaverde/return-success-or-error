namespace ReturnSuccessOrError.Tests;

// Conjunto FECHADO de erros usado nos testes que exercitam o modelo parametrizado (TError).
// public: são argumentos de tipo de interfaces mockadas (NSubstitute/Castle precisa de acesso).
public sealed record NotFoundError(string Message) : AppError(Message);
public sealed record ValidationError(string Message) : AppError(Message);
public sealed record UnexpectedError(string Message) : AppError(Message);

/// <summary>Union de erros de uma "feature" de teste — consumo exaustivo sem <c>_</c>.</summary>
public readonly union TestError(NotFoundError, ValidationError, UnexpectedError);

/// <summary>Helpers de leitura do union nos testes (extrai a mensagem via switch exaustivo).</summary>
internal static class TestErrorExtensions
{
    public static string Text(this TestError error) => error switch
    {
        NotFoundError n => n.Message,
        ValidationError v => v.Message,
        UnexpectedError u => u.Message,
    };
}
