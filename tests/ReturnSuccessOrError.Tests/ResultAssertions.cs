using Shouldly;

namespace ReturnSuccessOrError.Tests;

/// <summary>
/// Asserções para o <c>union</c> <see cref="ReturnSuccessOrError{TValue}"/>. Necessárias porque
/// o union é um struct wrapper: <c>ShouldBeOfType&lt;Success&gt;</c> falharia (o <c>GetType()</c>
/// devolve o tipo do union, não do caso). O pattern matching <c>is</c> reconhece o caso corretamente.
/// </summary>
internal static class ResultAssertions
{
    public static Success<T> ShouldBeSuccess<T>(this ReturnSuccessOrError<T> result)
        => result is Success<T> success
            ? success
            : throw new ShouldAssertException($"Esperava Success<{typeof(T).Name}>, mas foi Failure.");

    public static Failure ShouldBeFailure<T>(this ReturnSuccessOrError<T> result)
        => result is Failure failure
            ? failure
            : throw new ShouldAssertException("Esperava Failure, mas foi Success.");
}
