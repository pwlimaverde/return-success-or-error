using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Features;

public class FeatureTests
{
    // Serviço de feature de exemplo: prova que IFeatureService é um marcador utilizável.
    private sealed class SampleFeatureService : IFeatureService
    {
        public string Name => "sample";
    }

    [Fact]
    public void ServicoDeFeature_PodeImplementarIFeatureService_ESerReferenciado()
    {
        IFeatureService service = new SampleFeatureService();

        service.ShouldBeAssignableTo<IFeatureService>();
        service.ShouldBeOfType<SampleFeatureService>().Name.ShouldBe("sample");
    }
}
