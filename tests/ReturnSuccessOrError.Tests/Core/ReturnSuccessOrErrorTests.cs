using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Core;

public class ReturnSuccessOrErrorTests
{
    [Fact]
    public void Ok_CriaSuccess_ComValorCorreto()
    {
        var result = ReturnSuccessOrError<int>.Ok(42);

        var success = result.ShouldBeOfType<ReturnSuccessOrError<int>.Success>();
        success.Value.ShouldBe(42);
    }

    [Fact]
    public void Err_CriaFailure_ComErroCorreto()
    {
        var error = new ErrorGeneric("boom");

        var result = ReturnSuccessOrError<int>.Err(error);

        var failure = result.ShouldBeOfType<ReturnSuccessOrError<int>.Failure>();
        failure.Error.ShouldBe(error);
    }

    [Fact]
    public void Success_TemIgualdadePorValor()
    {
        var a = ReturnSuccessOrError<string>.Ok("x");
        var b = ReturnSuccessOrError<string>.Ok("x");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Failure_TemIgualdadePorValor()
    {
        var a = ReturnSuccessOrError<string>.Err(new ErrorGeneric("e"));
        var b = ReturnSuccessOrError<string>.Err(new ErrorGeneric("e"));

        a.ShouldBe(b);
    }

    [Fact]
    public void SuccessEFailure_NaoSaoIguais()
    {
        var success = ReturnSuccessOrError<int>.Ok(1);
        var failure = ReturnSuccessOrError<int>.Err(new ErrorGeneric("e"));

        success.ShouldNotBe(failure);
    }

    [Fact]
    public void Match_ChamaRamoDeSucesso()
    {
        var result = ReturnSuccessOrError<int>.Ok(10);

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("ok:10");
    }

    [Fact]
    public void Match_ChamaRamoDeErro()
    {
        var result = ReturnSuccessOrError<int>.Err(new ErrorGeneric("falhou"));

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("err:falhou");
    }

    [Fact]
    public void Switch_ExecutaAcaoDeSucesso()
    {
        var result = ReturnSuccessOrError<int>.Ok(7);
        int? captured = null;

        result.Switch(
            onSuccess: v => captured = v,
            onError: _ => captured = -1);

        captured.ShouldBe(7);
    }

    [Fact]
    public void Switch_ExecutaAcaoDeErro()
    {
        var result = ReturnSuccessOrError<int>.Err(new ErrorGeneric("e"));
        string? captured = null;

        result.Switch(
            onSuccess: _ => captured = "ok",
            onError: e => captured = e.Message);

        captured.ShouldBe("e");
    }

    [Fact]
    public void SwitchExpression_ExaustivoPorPadrao()
    {
        ReturnSuccessOrError<string> result = ReturnSuccessOrError<string>.Ok("v");

        // CS8509: o compilador não prova exaustividade de uma união fechada por
        // construtor privado (comportamento esperado, PRD §5.2). Dois braços bastam.
#pragma warning disable CS8509
        var text = result switch
        {
            ReturnSuccessOrError<string>.Success(var value) => $"S:{value}",
            ReturnSuccessOrError<string>.Failure(var error) => $"F:{error.Message}",
        };
#pragma warning restore CS8509

        text.ShouldBe("S:v");
    }

    [Fact]
    public void Unit_ESingleton_ComToStringCorreto()
    {
        Unit.Value.ShouldBeSameAs(Unit.Value);
        Unit.Value.ToString().ShouldBe("Unit - void");
    }

    [Fact]
    public void Nil_ESingleton_ComToStringCorreto()
    {
        Nil.Value.ShouldBeSameAs(Nil.Value);
        Nil.Value.ToString().ShouldBe("Nil - null");
    }
}
