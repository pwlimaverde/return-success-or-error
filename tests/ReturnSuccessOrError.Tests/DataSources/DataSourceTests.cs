using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.DataSources;

public class DataSourceTests
{
    private sealed record TestParams(AppError Error) : ParametersReturnResult(Error);

    [Fact]
    public async Task DataSource_EmSucesso_RetornaDadoBruto()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(99);

        var data = await ds.CallAsync(new TestParams(new ErrorGeneric("x")));

        data.ShouldBe(99);
    }

    [Fact]
    public async Task DataSource_EmFalha_LancaExcecao()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("db down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => ds.CallAsync(new TestParams(new ErrorGeneric("x"))));
    }
}
