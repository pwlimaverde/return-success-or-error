using ReturnSuccessOrError;
using Shouldly;
using Xunit;

// Namespace sem o segmento "Parameters" para não sombrear o tipo ReturnSuccessOrError.Parameters.
namespace ReturnSuccessOrError.Tests.ParametersDomain;

public class ParametersTests
{
    [Fact]
    public void NoParams_EUmParameters()
    {
        Parameters p = NoParams.Value;

        p.ShouldBeOfType<NoParams>();
    }

    [Fact]
    public void NoParams_Value_ESingleton()
    {
        NoParams.Value.ShouldBeSameAs(NoParams.Value);
    }

    [Fact]
    public void Parametros_SoCarregamDados_ComIgualdadePorValor()
    {
        // Parameters é só-dados: não há mais AppError embutido (separação do erro).
        Parameters a = new SampleParams(7, "abc");
        Parameters b = new SampleParams(7, "abc");

        a.ShouldBe(b);
    }

    private sealed record SampleParams(int N, string Text) : Parameters;
}
