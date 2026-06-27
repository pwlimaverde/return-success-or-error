using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseCallDataTests
{
    private sealed record TestParams(AppError Error) : ParametersReturnResult(Error);

    private sealed class StringUsecase(IDataSource<int> ds, Action? onProcess = null)
        : UsecaseBaseCallData<string, int>(ds)
    {
        protected override ReturnSuccessOrError<string> Process(int data, ParametersReturnResult p)
        {
            onProcess?.Invoke();
            return ReturnSuccessOrError<string>.Ok($"valor: {data}");
        }
    }

    private sealed class ThrowingProcessUsecase(IDataSource<int> ds)
        : UsecaseBaseCallData<string, int>(ds)
    {
        protected override ReturnSuccessOrError<string> Process(int data, ParametersReturnResult p)
            => throw new InvalidOperationException("process-boom");
    }

    // Erro de domínio customizado: valida preservação de tipo no fetch-catch.
    private sealed record ApiError(string Message, int StatusCode) : AppError(Message);

    [Fact]
    public async Task CallAsync_ComSucesso_ProcessaDadoDoFetch()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(42);

        var usecase = new StringUsecase(ds);

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha")));

        result.Match(
            onSuccess: v => v,
            onError: e => e.Message
        ).ShouldBe("valor: 42");
    }

    [Fact]
    public async Task CallAsync_QuandoFetchFalha_RetornaDataSourceCatch_ESemChamarProcess()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("db down"));

        var processChamado = false;
        var usecase = new StringUsecase(ds, () => processChamado = true);

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha")));

        var failure = result.ShouldBeFailure();
        failure.Error.Message.ShouldContain(ErrorCodes.DataSourceCatch);
        processChamado.ShouldBeFalse(); // curto-circuito: Process não é chamado
    }

    [Fact]
    public async Task CallAsync_QuandoFetchFalha_PreservaTipoConcretoDoErro()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("db down"));

        var usecase = new StringUsecase(ds);

        var result = await usecase.CallAsync(new TestParams(new ApiError("falha api", 503)));

        var failure = result.ShouldBeFailure();
        var api = failure.Error.ShouldBeOfType<ApiError>();
        api.StatusCode.ShouldBe(503);
        api.Message.ShouldContain(ErrorCodes.DataSourceCatch);
    }

    [Fact]
    public async Task CallAsync_Background_ExcecaoEmProcess_RetornaBackgroundCatch()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(1);

        var usecase = new ThrowingProcessUsecase(ds) { RunInBackground = true };

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha base")));

        var failure = result.ShouldBeFailure();
        failure.Error.Message.ShouldContain(ErrorCodes.BackgroundCatch);
        failure.Error.Message.ShouldContain("process-boom");
    }

    [Fact]
    public async Task CallAsync_Direto_ExcecaoEmProcess_Propaga()
    {
        // Contrato (PRD §5.7): com fetch OK e modo direto, Process não é envolto em
        // try/catch — a exceção propaga. Só o modo background vira BackgroundCatch.
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(1);

        var usecase = new ThrowingProcessUsecase(ds); // RunInBackground = false

        await Should.ThrowAsync<InvalidOperationException>(
            () => usecase.CallAsync(new TestParams(new ErrorGeneric("e"))));
    }

    [Fact]
    public async Task CallAsync_ParidadeDiretoBackground()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(7);

        var direto = new StringUsecase(ds);
        var background = new StringUsecase(ds) { RunInBackground = true };
        var p = new TestParams(new ErrorGeneric("e"));

        var rDireto = await direto.CallAsync(p);
        var rBackground = await background.CallAsync(p);

        rBackground.ShouldBe(rDireto);
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(42);

        var usecase = new StringUsecase(ds) { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("e")));

        result.ShouldBeSuccess().Value.ShouldBe("valor: 42");
    }

    [Fact]
    public async Task CallAsync_PropagaCancellationToken()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<ParametersReturnResult>(), Arg.Any<CancellationToken>()).Returns(1);
        var usecase = new StringUsecase(ds);
        using var cts = new CancellationTokenSource();

        await usecase.CallAsync(new TestParams(new ErrorGeneric("x")), cts.Token);

        await ds.Received(1).CallAsync(Arg.Any<ParametersReturnResult>(), cts.Token);
    }
}
