using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Errors;

public class AppErrorTests
{
    // Erro de domínio customizado: exercita a preservação de tipo concreto em WithMessage
    // (herdado da base AppError — não reimplementado).
    private sealed record ApiError(string Message, int StatusCode) : AppError(Message);

    [Fact]
    public void ErrorGeneric_TemIgualdadePorValor()
    {
        new ErrorGeneric("msg").ShouldBe(new ErrorGeneric("msg"));
    }

    [Fact]
    public void ErrorGeneric_ToString_TemFormatoEsperado()
    {
        new ErrorGeneric("algo deu errado").ToString()
            .ShouldBe("ErrorGeneric - algo deu errado");
    }

    [Fact]
    public void WithMessage_EmErrorGeneric_DevolveErrorGenericComNovaMensagem()
    {
        AppError original = new ErrorGeneric("antiga");

        var enriched = original.WithMessage("nova");

        enriched.ShouldBeOfType<ErrorGeneric>();
        enriched.Message.ShouldBe("nova");
    }

    [Fact]
    public void WithMessage_EmErroCustomizado_PreservaTipoConcretoEDemaisCampos()
    {
        AppError original = new ApiError("não encontrado", 404);

        var enriched = original.WithMessage("recurso ausente");

        var api = enriched.ShouldBeOfType<ApiError>();
        api.Message.ShouldBe("recurso ausente");
        api.StatusCode.ShouldBe(404); // campo extra preservado
    }
}
