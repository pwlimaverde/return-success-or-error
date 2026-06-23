using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Errors;

public class AppErrorTests
{
    // Erro de domínio customizado: exercita a preservação de tipo concreto em WithMessage.
    private sealed record ApiError(string Message, int StatusCode) : IAppError
    {
        public IAppError WithMessage(string message) => this with { Message = message };
    }

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
        IAppError original = new ErrorGeneric("antiga");

        var enriched = original.WithMessage("nova");

        enriched.ShouldBeOfType<ErrorGeneric>();
        enriched.Message.ShouldBe("nova");
    }

    [Fact]
    public void WithMessage_EmErroCustomizado_PreservaTipoConcretoEDemaisCampos()
    {
        IAppError original = new ApiError("não encontrado", 404);

        var enriched = original.WithMessage("recurso ausente");

        var api = enriched.ShouldBeOfType<ApiError>();
        api.Message.ShouldBe("recurso ausente");
        api.StatusCode.ShouldBe(404); // campo extra preservado
    }
}
