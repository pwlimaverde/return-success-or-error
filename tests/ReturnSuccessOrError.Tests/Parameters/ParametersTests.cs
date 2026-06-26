using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Parameters;

public class ParametersTests
{
    [Fact]
    public void NoParams_SemErro_ExpoeErrorGenericDefault()
    {
        ParametersReturnResult p = new NoParams();

        var error = p.Error;

        error.ShouldBeOfType<ErrorGeneric>();
        error.Message.ShouldBe("NoParams: unspecified generic error");
    }

    [Fact]
    public void NoParams_ComErroCustomizado_ExpoeOErroFornecido()
    {
        var custom = new ErrorGeneric("erro específico");
        ParametersReturnResult p = new NoParams(custom);

        p.Error.ShouldBeSameAs(custom);
    }
}
