using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Core;

public class ReturnSuccessOrErrorTests
{
    [Fact]
    public void Success_TemIgualdadePorValor()
    {
        ReturnSuccessOrError<string> a = "x";
        ReturnSuccessOrError<string> b = "x";

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Failure_TemIgualdadePorValor()
    {
        ReturnSuccessOrError<string> a = new ErrorGeneric("e");
        ReturnSuccessOrError<string> b = new ErrorGeneric("e");

        a.ShouldBe(b);
    }

    [Fact]
    public void SuccessEFailure_NaoSaoIguais()
    {
        ReturnSuccessOrError<int> success = 1;
        ReturnSuccessOrError<int> failure = new ErrorGeneric("e");

        success.ShouldNotBe(failure);
    }

    [Fact]
    public void Match_ChamaRamoDeSucesso()
    {
        ReturnSuccessOrError<int> result = 10;

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("ok:10");
    }

    [Fact]
    public void Match_ChamaRamoDeErro()
    {
        ReturnSuccessOrError<int> result = new ErrorGeneric("falhou");

        var message = result.Match(
            onSuccess: v => $"ok:{v}",
            onError: e => $"err:{e.Message}");

        message.ShouldBe("err:falhou");
    }

    [Fact]
    public void SwitchExpression_ExaustivoPorPadrao()
    {
        ReturnSuccessOrError<string> result = "v";

        // union (C# 15): o compilador PROVA a exaustividade — dois braços, sem caso default.
        var text = result switch
        {
            Success<string>(var value) => $"S:{value}",
            Failure(var error) => $"F:{error.Message}",
        };

        text.ShouldBe("S:v");
    }

    [Fact]
    public void ConversaoImplicita_DeValor_CriaSuccess()
    {
        ReturnSuccessOrError<int> result = 42; // implicit: TValue -> Success

        result.ShouldBeSuccess().Value.ShouldBe(42);
    }

    [Fact]
    public void ConversaoImplicita_DeAppError_CriaFailure()
    {
        ReturnSuccessOrError<int> result = new ErrorGeneric("falhou"); // implicit: AppError -> Failure

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
