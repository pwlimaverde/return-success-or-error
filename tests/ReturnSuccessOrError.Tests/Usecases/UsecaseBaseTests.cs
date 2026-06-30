using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseTests
{
    private sealed record NumberParams(int N) : Parameters;

    private sealed class DoubleUsecase : UsecaseBase<int, NumberParams, TestError>
    {
        protected override ReturnSuccessOrError<int, TestError> Process(NumberParams p)
        {
            if (p.N < 0)
                return (TestError)new ValidationError("N deve ser >= 0"); // erro de negócio -> Failure
            return p.N * 2;                                               // int -> Success
        }

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError(exception.Message);
    }

    private sealed class ThrowingUsecase : UsecaseBase<int, NumberParams, TestError>
    {
        protected override ReturnSuccessOrError<int, TestError> Process(NumberParams p)
            => throw new InvalidOperationException("kaboom");

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError($"capturado: {exception.Message}");
    }

    private sealed class UnitUsecase : UsecaseBase<Unit, NoParams, TestError>
    {
        protected override ReturnSuccessOrError<Unit, TestError> Process(NoParams p) => Unit.Value;
        protected override TestError OnUnexpected(Exception exception) => new UnexpectedError(exception.Message);
    }

    private sealed class NilUsecase : UsecaseBase<Nil, NoParams, TestError>
    {
        protected override ReturnSuccessOrError<Nil, TestError> Process(NoParams p) => Nil.Value;
        protected override TestError OnUnexpected(Exception exception) => new UnexpectedError(exception.Message);
    }

    [Fact]
    public async Task CallAsync_Direto_RetornaResultadoDeProcess()
    {
        var usecase = new DoubleUsecase();

        var result = await usecase.CallAsync(new NumberParams(21));

        result.ShouldBeSuccess().Value.ShouldBe(42);
    }

    [Fact]
    public async Task CallAsync_ErroDeNegocio_RetornaFailureDoTipoPrevisto()
    {
        var usecase = new DoubleUsecase();

        var result = await usecase.CallAsync(new NumberParams(-1));

        (result.ShouldBeFailure().Error is ValidationError).ShouldBeTrue();
    }

    [Fact]
    public async Task CallAsync_Background_RetornaResultadoIdenticoAoDireto()
    {
        var direto = new DoubleUsecase();
        var background = new DoubleUsecase { RunInBackground = true };
        var p = new NumberParams(21);

        var rDireto = await direto.CallAsync(p);
        var rBackground = await background.CallAsync(p);

        rBackground.ShouldBe(rDireto);
    }

    [Fact]
    public async Task CallAsync_Background_ExcecaoInesperada_ViraOnUnexpected()
    {
        var usecase = new ThrowingUsecase { RunInBackground = true };

        var result = await usecase.CallAsync(new NumberParams(0));

        var error = result.ShouldBeFailure().Error;
        (error is UnexpectedError).ShouldBeTrue();
        error.Text().ShouldContain("kaboom");
    }

    [Fact]
    public async Task CallAsync_Direto_ExcecaoInesperada_TambemViraOnUnexpected()
    {
        // Contrato novo (modelo TError): o Process NUNCA propaga exceção ao chamador —
        // direto e background convertem a inesperada via OnUnexpected. Nada escapa como throw.
        var usecase = new ThrowingUsecase(); // RunInBackground = false

        var result = await usecase.CallAsync(new NumberParams(0));

        (result.ShouldBeFailure().Error is UnexpectedError).ShouldBeTrue();
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var usecase = new DoubleUsecase { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new NumberParams(5));

        result.ShouldBeSuccess().Value.ShouldBe(10);
    }

    [Fact]
    public async Task CallAsync_ResultadoUnit()
    {
        var result = await new UnitUsecase().CallAsync(NoParams.Value);

        result.ShouldBeSuccess().Value.ShouldBeSameAs(Unit.Value);
    }

    [Fact]
    public async Task CallAsync_ResultadoNil()
    {
        var result = await new NilUsecase().CallAsync(NoParams.Value);

        result.ShouldBeSuccess().Value.ShouldBeSameAs(Nil.Value);
    }
}
