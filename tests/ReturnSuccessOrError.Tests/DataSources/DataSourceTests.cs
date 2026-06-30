using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.DataSources;

public class DataSourceTests
{
    // public: é argumento de tipo da interface mockada (NSubstitute/Castle precisa de acesso).
    public sealed record TestParams : Parameters;

    [Fact]
    public async Task DataSource_EmSucesso_RetornaDadoBruto()
    {
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .Returns(99);

        var data = await ds.CallAsync(new TestParams());

        data.ShouldBe(99);
    }

    [Fact]
    public async Task DataSource_EmFalha_LancaExcecao()
    {
        // Contrato: o DataSource é burro — devolve dado cru OU lança exceção técnica.
        // Traduzir a exceção é responsabilidade do Repository, não da fonte.
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("db down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => ds.CallAsync(new TestParams()));
    }
}
