using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseTests
{
    private sealed record TestParams(AppError Error) : ParametersReturnResult(Error);

    // Caso de uso que devolve o dobro do número nos parâmetros.
    private sealed record NumberParams(int N, AppError Error) : ParametersReturnResult(Error);

    private sealed class DoubleUsecase : UsecaseBase<int>
    {
        protected override ReturnSuccessOrError<int> Process(ParametersReturnResult parameters)
        {
            var p = (NumberParams)parameters;
            return p.N * 2;
        }
    }

    private sealed class ThrowingUsecase : UsecaseBase<int>
    {
        protected override ReturnSuccessOrError<int> Process(ParametersReturnResult parameters)
            => throw new InvalidOperationException("kaboom");
    }

    private sealed class UnitUsecase : UsecaseBase<Unit>
    {
        protected override ReturnSuccessOrError<Unit> Process(ParametersReturnResult parameters)
            => Unit.Value;
    }

    private sealed class NilUsecase : UsecaseBase<Nil>
    {
        protected override ReturnSuccessOrError<Nil> Process(ParametersReturnResult parameters)
            => Nil.Value;
    }

    [Fact]
    public async Task CallAsync_Direto_RetornaResultadoDeProcess()
    {
        var usecase = new DoubleUsecase();

        var result = await usecase.CallAsync(new NumberParams(21, new ErrorGeneric("e")));

        result.ShouldBeSuccess().Value.ShouldBe(42);
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

        var failure = result.ShouldBeFailure();
        failure.Error.Message.ShouldContain(ErrorCodes.BackgroundCatch);
        failure.Error.Message.ShouldContain("kaboom");
    }

    [Fact]
    public async Task CallAsync_Direto_ExcecaoEmProcess_Propaga()
    {
        // Contrato (PRD §5.6): em modo direto, Process não é envolto em try/catch —
        // a exceção propaga ao chamador. Só o modo background a converte em Failure.
        var usecase = new ThrowingUsecase(); // RunInBackground = false

        await Should.ThrowAsync<InvalidOperationException>(
            () => usecase.CallAsync(new TestParams(new ErrorGeneric("e"))));
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var usecase = new DoubleUsecase { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new NumberParams(5, new ErrorGeneric("e")));

        result.ShouldBeSuccess().Value.ShouldBe(10);
    }

    [Fact]
    public async Task CallAsync_ResultadoUnit()
    {
        var result = await new UnitUsecase().CallAsync(new TestParams(new ErrorGeneric("e")));

        result.ShouldBeSuccess().Value.ShouldBeSameAs(Unit.Value);
    }

    [Fact]
    public async Task CallAsync_ResultadoNil()
    {
        var result = await new NilUsecase().CallAsync(new TestParams(new ErrorGeneric("e")));

        result.ShouldBeSuccess().Value.ShouldBeSameAs(Nil.Value);
    }
}
