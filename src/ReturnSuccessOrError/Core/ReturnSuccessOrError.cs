namespace ReturnSuccessOrError;

/// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TValue">Tipo do valor carregado em caso de sucesso.</typeparam>
public sealed record Success<TValue>(TValue Value);

/// <summary>Resultado com falha, carregando um erro de tipo <typeparamref name="TError"/>.</summary>
/// <typeparam name="TError">Tipo do erro carregado em caso de falha — tipicamente um <c>union</c> fechado da feature.</typeparam>
public sealed record Failure<TError>(TError Error);

/// <summary>
/// União discriminada (C# 15) que representa o desfecho de uma operação como
/// <see cref="Success{TValue}"/> ou <see cref="Failure{TError}"/>. O compilador garante a
/// exaustividade de <see cref="Match{TResult}"/> e do <c>switch</c> — não há terceiro caso.
/// <para>
/// O <b>erro é parametrizado</b> (<typeparamref name="TError"/>): cada feature fecha o seu
/// conjunto de erros possíveis num <c>union</c> próprio, e o consumo é obrigado pelo compilador
/// a cobrir todos os casos — sem braço <c>_</c>. Ver PRD §5.2.
/// </para>
/// </summary>
/// <typeparam name="TValue">Tipo do valor carregado em caso de sucesso.</typeparam>
/// <typeparam name="TError">Tipo do erro carregado em caso de falha.</typeparam>
public readonly union ReturnSuccessOrError<TValue, TError>(Success<TValue>, Failure<TError>)
{
    /// <summary>Açúcar: converte um valor de sucesso diretamente (ex.: <c>return value;</c>).</summary>
    public static implicit operator ReturnSuccessOrError<TValue, TError>(TValue value) => new Success<TValue>(value);

    /// <summary>Açúcar: converte um erro diretamente em falha (ex.: <c>return error;</c>).</summary>
    public static implicit operator ReturnSuccessOrError<TValue, TError>(TError error) => new Failure<TError>(error);

    /// <summary>Consumo exaustivo: obriga tratar sucesso e erro; o compilador prova a exaustividade.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onError) => this switch
        {
            Success<TValue> success => onSuccess(success.Value),
            Failure<TError> failure => onError(failure.Error),
        };
}
