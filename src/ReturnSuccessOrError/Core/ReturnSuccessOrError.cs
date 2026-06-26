using System.Diagnostics;

namespace ReturnSuccessOrError;

/// <summary>
/// União discriminada selada que representa o desfecho de uma operação como
/// <see cref="Success"/> ou <see cref="Failure"/>. O construtor privado fecha a
/// hierarquia: nenhum subtipo pode ser declarado fora desta biblioteca, o que
/// garante a exaustividade de <see cref="Match{TResult}"/> e do <c>switch</c>.
/// </summary>
/// <typeparam name="TValue">Tipo do valor carregado em caso de sucesso.</typeparam>
public abstract record ReturnSuccessOrError<TValue>
{
    // Construtor privado: impede subtipos externos — a união é fechada.
    private ReturnSuccessOrError() { }

    /// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
    public sealed record Success(TValue Value) : ReturnSuccessOrError<TValue>;

    /// <summary>Resultado com falha, carregando um <see cref="AppError"/>.</summary>
    public sealed record Failure(AppError Error) : ReturnSuccessOrError<TValue>;

    /// <summary>Cria um resultado de sucesso.</summary>
    public static ReturnSuccessOrError<TValue> Ok(TValue value) => new Success(value);

    /// <summary>Cria um resultado de falha.</summary>
    public static ReturnSuccessOrError<TValue> Err(AppError error) => new Failure(error);

    /// <summary>Consumo exaustivo: obriga tratar sucesso e erro; nunca cai no caso default.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<AppError, TResult> onError) => this switch
    {
        Success success => onSuccess(success.Value),
        Failure failure => onError(failure.Error),
        _ => throw new UnreachableException(),
    };

    /// <summary>Variante sem retorno, para efeitos colaterais (logging, side effects).</summary>
    public void Switch(Action<TValue> onSuccess, Action<AppError> onError)
    {
        switch (this)
        {
            case Success success: onSuccess(success.Value); break;
            case Failure failure: onError(failure.Error); break;
            default: throw new UnreachableException();
        }
    }
}
