namespace ReturnSuccessOrError;

/// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TValue">Tipo do valor carregado em caso de sucesso.</typeparam>
public sealed record Success<TValue>(TValue Value);

/// <summary>Resultado com falha, carregando um <see cref="AppError"/>.</summary>
public sealed record Failure(AppError Error);

/// <summary>
/// União discriminada (C# 15) que representa o desfecho de uma operação como
/// <see cref="Success{TValue}"/> ou <see cref="Failure"/>. O compilador garante a
/// exaustividade de <see cref="Match{TResult}"/> e do <c>switch</c> — não há terceiro caso.
/// </summary>
/// <typeparam name="TValue">Tipo do valor carregado em caso de sucesso.</typeparam>
public readonly union ReturnSuccessOrError<TValue>(Success<TValue>, Failure)
{
    /// <summary>Cria um resultado de sucesso.</summary>
    public static ReturnSuccessOrError<TValue> Ok(TValue value) => new Success<TValue>(value);

    /// <summary>Cria um resultado de falha.</summary>
    public static ReturnSuccessOrError<TValue> Err(AppError error) => new Failure(error);

    /// <summary>Açúcar: converte um valor de sucesso diretamente (ex.: <c>return value;</c>).</summary>
    public static implicit operator ReturnSuccessOrError<TValue>(TValue value) => new Success<TValue>(value);

    /// <summary>Consumo exaustivo: obriga tratar sucesso e erro; o compilador prova a exaustividade.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<AppError, TResult> onError) => this switch
        {
            Success<TValue> success => onSuccess(success.Value),
            Failure failure => onError(failure.Error),
        };

    /// <summary>Variante sem retorno, para efeitos colaterais (logging, side effects).</summary>
    public void Switch(Action<TValue> onSuccess, Action<AppError> onError)
    {
        switch (this)
        {
            case Success<TValue> success: onSuccess(success.Value); break;
            case Failure failure: onError(failure.Error); break;
        }
    }
}
