using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReturnSuccessOrError;
using Shouldly;
using Xunit;
using Parameters = global::ReturnSuccessOrError.Parameters; // desambigua do namespace Tests.Parameters

namespace ReturnSuccessOrError.Tests.Repositories;

public class RepositoryTests
{
    // public: é argumento de tipo da interface mockada (NSubstitute/Castle precisa de acesso).
    public sealed record TestParams : Parameters;

    // Repository que TRADUZ a exceção técnica num dos erros do conjunto fechado (MapError abstrato).
    private sealed class TranslatingRepository(IDataSource<int, TestParams> ds)
        : RepositoryBase<int, TestParams, TestError>(ds)
    {
        protected override TestError MapError(Exception exception, TestParams parameters) => exception switch
        {
            InvalidOperationException => new NotFoundError($"não encontrado: {exception.Message}"),
            _                         => new UnexpectedError($"inesperado: {exception.Message}"),
        };
    }

    [Fact]
    public async Task CallAsync_EmSucesso_RetornaSuccessComDadoBruto()
    {
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>()).Returns(42);
        var repo = new TranslatingRepository(ds);

        var result = await repo.CallAsync(new TestParams());

        result.ShouldBeSuccess().Value.ShouldBe(42);
    }

    [Fact]
    public async Task CallAsync_QuandoFonteLanca_TraduzExcecaoViaMapError()
    {
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("conn refused"));
        var repo = new TranslatingRepository(ds);

        var result = await repo.CallAsync(new TestParams());

        var error = result.ShouldBeFailure().Error;
        // O erro é o union TestError; o caso concreto é discriminado por pattern matching.
        (error is NotFoundError).ShouldBeTrue();
    }

    [Fact]
    public async Task CallAsync_NoBracoDefaultDoMapError_RetornaCasoInesperado()
    {
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new TimeoutException("boom"));
        var repo = new TranslatingRepository(ds);

        var result = await repo.CallAsync(new TestParams());

        var error = result.ShouldBeFailure().Error;
        var text = error switch
        {
            NotFoundError   => "nf",
            ValidationError => "val",
            UnexpectedError u => u.Message,
        };
        text.ShouldContain("boom");
    }

    [Fact]
    public async Task CallAsync_CancelamentoDoChamador_PropagaOCE_SemMapError()
    {
        // Cancelamento do CHAMADOR não é falha de domínio: a fronteira rethrow o OCE
        // em vez de traduzi-lo num erro do union (idioma .NET / ASP.NET Core).
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new OperationCanceledException());
        var repo = new TranslatingRepository(ds);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => repo.CallAsync(new TestParams(), cts.Token));
    }

    [Fact]
    public async Task CallAsync_OCEInterno_SemCancelamentoDoChamador_TraduzViaMapError()
    {
        // Um OCE lançado pela fonte SEM o token do chamador cancelado é falha técnica
        // como outra qualquer — cai no MapError (braço default), não propaga.
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new OperationCanceledException("timeout interno da fonte"));
        var repo = new TranslatingRepository(ds);

        var result = await repo.CallAsync(new TestParams()); // token default, não cancelado

        var error = result.ShouldBeFailure().Error;
        (error is UnexpectedError).ShouldBeTrue();
        error.Text().ShouldContain("timeout interno da fonte");
    }

    [Fact]
    public async Task CallAsync_PropagaCancellationTokenParaFonte()
    {
        var ds = Substitute.For<IDataSource<int, TestParams>>();
        ds.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>()).Returns(1);
        var repo = new TranslatingRepository(ds);
        using var cts = new CancellationTokenSource();

        await repo.CallAsync(new TestParams(), cts.Token);

        await ds.Received(1).CallAsync(Arg.Any<TestParams>(), cts.Token);
    }
}
