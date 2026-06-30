using Shouldly;

namespace ReturnSuccessOrError.Tests;

/// <summary>
/// Asserções para o <c>union</c> <see cref="ReturnSuccessOrError{TValue, TError}"/>. Necessárias porque
/// o union é um struct wrapper: <c>ShouldBeOfType&lt;Success&gt;</c> falharia (o <c>GetType()</c>
/// devolve o tipo do union, não do caso). O pattern matching <c>is</c> reconhece o caso corretamente.
/// </summary>
internal static class ResultAssertions
{
    public static Success<TValue> ShouldBeSuccess<TValue, TError>(this ReturnSuccessOrError<TValue, TError> result)
        => result is Success<TValue> success
            ? success
            : throw new ShouldAssertException($"Esperava Success<{typeof(TValue).Name}>, mas foi Failure.");

    public static Failure<TError> ShouldBeFailure<TValue, TError>(this ReturnSuccessOrError<TValue, TError> result)
        => result is Failure<TError> failure
            ? failure
            : throw new ShouldAssertException("Esperava Failure, mas foi Success.");
}
