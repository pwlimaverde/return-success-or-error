using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseTests
{
    private sealed record TestParams(IAppError Error) : IParametersReturnResult;

    // Caso de uso que devolve o dobro do número nos parâmetros.
    private sealed record NumberParams(int N, IAppError Error) : IParametersReturnResult;

    private sealed class DoubleUsecase : UsecaseBase<int>
    {
        protected override ReturnSuccessOrError<int> Process(IParametersReturnResult parameters)
        {
            var p = (NumberParams)parameters;
            return ReturnSuccessOrError<int>.Ok(p.N * 2);
        }
    }

    private sealed class ThrowingUsecase : UsecaseBase<int>
    {
        protected override ReturnSuccessOrError<int> Process(IParametersReturnResult parameters)
            => throw new InvalidOperationException("kaboom");
    }

    private sealed class UnitUsecase : UsecaseBase<Unit>
    {
        protected override ReturnSuccessOrError<Unit> Process(IParametersReturnResult parameters)
            => ReturnSuccessOrError<Unit>.Ok(Unit.Value);
    }

    private sealed class NilUsecase : UsecaseBase<Nil>
    {
        protected override ReturnSuccessOrError<Nil> Process(IParametersReturnResult parameters)
            => ReturnSuccessOrError<Nil>.Ok(Nil.Value);
    }

    [Fact]
    public async Task CallAsync_Direto_RetornaResultadoDeProcess()
    {
        var usecase = new DoubleUsecase();

        var result = await usecase.CallAsync(new NumberParams(21, new ErrorGeneric("e")));

        result.ShouldBeOfType<ReturnSuccessOrError<int>.Success>().Value.ShouldBe(42);
    }

    [Fact]
    public async Task CallAsync_Background_RetornaResultadoIdenticoAoDireto()
    {
        var direto = new DoubleUsecase();
        var background = new DoubleUsecase { RunInBackground = true };
        var p = new NumberParams(21, new ErrorGeneric("e"));

        var rDireto = await direto.CallAsync(p);
        var rBackground = await background.CallAsync(p);

        rBackground.ShouldBe(rDireto);
    }

    [Fact]
    public async Task CallAsync_Background_ExcecaoEmProcess_RetornaBackgroundCatch()
    {
        var usecase = new ThrowingUsecase { RunInBackground = true };

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha base")));

        var failure = result.ShouldBeOfType<ReturnSuccessOrError<int>.Failure>();
        failure.Error.Message.ShouldContain(ErrorCodes.BackgroundCatch);
        failure.Error.Message.ShouldContain("kaboom");
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var usecase = new DoubleUsecase { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new NumberParams(5, new ErrorGeneric("e")));

        result.ShouldBeOfType<ReturnSuccessOrError<int>.Success>().Value.ShouldBe(10);
    }

    [Fact]
    public async Task CallAsync_ResultadoUnit()
    {
        var result = await new UnitUsecase().CallAsync(new TestParams(new ErrorGeneric("e")));

        result.ShouldBeOfType<ReturnSuccessOrError<Unit>.Success>().Value.ShouldBeSameAs(Unit.Value);
    }

    [Fact]
    public async Task CallAsync_ResultadoNil()
    {
        var result = await new NilUsecase().CallAsync(new TestParams(new ErrorGeneric("e")));

        result.ShouldBeOfType<ReturnSuccessOrError<Nil>.Success>().Value.ShouldBeSameAs(Nil.Value);
    }
}
