using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Core;

public class ReturnSuccessOrErrorTests
{
    [Fact]
    public void Success_TemIgualdadePorValor()
    {
        ReturnSuccessOrError<string, ErrorGeneric> a = "x";
        ReturnSuccessOrError<string, ErrorGeneric> b = "x";

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Failure_TemIgualdadePorValor()
    {
        ReturnSuccessOrError<string, ErrorGeneric> a = new ErrorGeneric("e");
        ReturnSuccessOrError<string, ErrorGeneric> b = new ErrorGeneric("e");

        a.ShouldBe(b);
    }

    [Fact]
    public void SuccessEFailure_NaoSaoIguais()
    {
        ReturnSuccessOrError<int, ErrorGeneric> success = 1;
        ReturnSuccessOrError<int, ErrorGeneric> failure = new ErrorGeneric("e");

        success.ShouldNotBe(failure);
    }

    [Fact]
    public void Match_ChamaRamoDeSucesso()
    {
        ReturnSuccessOrError<int, ErrorGeneric> result = 10;

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("ok:10");
    }

    [Fact]
    public void Match_ChamaRamoDeErro()
    {
        ReturnSuccessOrError<int, ErrorGeneric> result = new ErrorGeneric("falhou");

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("err:falhou");
    }

    [Fact]
    public void SwitchExpression_ExaustivoPorPadrao()
    {
        ReturnSuccessOrError<string, ErrorGeneric> result = "v";

        // union (C# 15): o compilador PROVA a exaustividade — dois braços, sem caso default.
        var text = result switch
        {
            Success<string>(var value) => $"S:{value}",
            Failure<ErrorGeneric>(var error) => $"F:{error.Message}",
        };

        text.ShouldBe("S:v");
    }

    [Fact]
    public void SwitchNoErro_SobreUnionDaFeature_ExaustivoSemDefault()
    {
        TestError erro = new ValidationError("inválido");
        ReturnSuccessOrError<int, TestError> result = erro;

        // O erro é um union fechado da feature → o switch é exaustivo, SEM braço _.
        var text = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => e switch
            {
                NotFoundError n   => $"nf:{n.Message}",
                ValidationError v => $"val:{v.Message}",
                UnexpectedError u => $"unx:{u.Message}",
            });

        text.ShouldBe("val:inválido");
    }

    [Fact]
    public void ConversaoImplicita_DeValor_CriaSuccess()
    {
        ReturnSuccessOrError<int, ErrorGeneric> result = 42; // implicit: TValue -> Success

        result.ShouldBeSuccess().Value.ShouldBe(42);
    }

    [Fact]
    public void ConversaoImplicita_DeErro_CriaFailure()
    {
        ReturnSuccessOrError<int, ErrorGeneric> result = new ErrorGeneric("falhou"); // implicit: TError -> Failure

        result.ShouldBeFailure().Error.Message.ShouldBe("falhou");
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
