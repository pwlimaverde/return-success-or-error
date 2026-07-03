using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseTests
{
    private sealed record NumberParams(int N) : Parameters;

    // Usa as factories Fail/Ok da base — a forma RECOMENDADA de criar o resultado no Process.
    private sealed class DoubleUsecase : UsecaseBase<int, NumberParams, TestError>
    {
        protected override ReturnSuccessOrError<int, TestError> Process(
            NumberParams p, CancellationToken cancellationToken)
        {
            if (p.N < 0)
                return Fail(new ValidationError("N deve ser >= 0")); // erro de negócio -> Failure (factory)
            return Ok(p.N * 2);                                      // -> Success (factory)
        }

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError(exception.Message);
    }

    private sealed class ThrowingUsecase : UsecaseBase<int, NumberParams, TestError>
    {
        public Exception ToThrow { get; init; } = new InvalidOperationException("kaboom");

        protected override ReturnSuccessOrError<int, TestError> Process(
            NumberParams p, CancellationToken cancellationToken)
            => throw ToThrow;

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError($"capturado: {exception.Message}");
    }

    // Cancela o próprio token DENTRO do Process e coopera via ThrowIfCancellationRequested —
    // simula o chamador cancelando no meio de um processamento longo.
    private sealed class CooperativeCancelUsecase(CancellationTokenSource cts)
        : UsecaseBase<int, NumberParams, TestError>
    {
        protected override ReturnSuccessOrError<int, TestError> Process(
            NumberParams p, CancellationToken cancellationToken)
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Ok(1);
        }

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError(exception.Message);
    }

    private sealed class MeasuringUsecase : UsecaseBase<int, NumberParams, TestError>
    {
        public TimeSpan? Measured { get; private set; }

        protected override void OnExecutionTimeMeasured(TimeSpan elapsed) => Measured = elapsed;

        protected override ReturnSuccessOrError<int, TestError> Process(
            NumberParams p, CancellationToken cancellationToken) => Ok(p.N);

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError(exception.Message);
    }

    private sealed class UnitUsecase : UsecaseBase<Unit, NoParams, TestError>
    {
        protected override ReturnSuccessOrError<Unit, TestError> Process(
            NoParams p, CancellationToken cancellationToken) => Unit.Value;

        protected override TestError OnUnexpected(Exception exception) => new UnexpectedError(exception.Message);
    }

    private sealed class NilUsecase : UsecaseBase<Nil, NoParams, TestError>
    {
        protected override ReturnSuccessOrError<Nil, TestError> Process(
            NoParams p, CancellationToken cancellationToken) => Nil.Value;

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
    public async Task CallAsync_ErroDeNegocio_ViaFail_RetornaFailureDoTipoPrevisto()
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
        // Contrato: o Process NUNCA propaga exceção de bug ao chamador —
        // direto e background convertem a inesperada via OnUnexpected.
        var usecase = new ThrowingUsecase(); // RunInBackground = false

        var result = await usecase.CallAsync(new NumberParams(0));

        (result.ShouldBeFailure().Error is UnexpectedError).ShouldBeTrue();
    }

    [Fact]
    public async Task CallAsync_TokenJaCancelado_Direto_LancaOCE()
    {
        // Cancelamento NÃO é falha de domínio: propaga como OCE, sem passar por OnUnexpected.
        var usecase = new DoubleUsecase();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => usecase.CallAsync(new NumberParams(1), cts.Token));
    }

    [Fact]
    public async Task CallAsync_TokenJaCancelado_Background_LancaOCE()
    {
        // Paridade direto↔background também sob cancelamento: mesmo comportamento (OCE).
        var usecase = new DoubleUsecase { RunInBackground = true };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => usecase.CallAsync(new NumberParams(1), cts.Token));
    }

    [Fact]
    public async Task CallAsync_CancelamentoCooperativoNoProcess_Direto_LancaOCE()
    {
        using var cts = new CancellationTokenSource();
        var usecase = new CooperativeCancelUsecase(cts);

        await Should.ThrowAsync<OperationCanceledException>(
            () => usecase.CallAsync(new NumberParams(1), cts.Token));
    }

    [Fact]
    public async Task CallAsync_CancelamentoCooperativoNoProcess_Background_LancaOCE()
    {
        using var cts = new CancellationTokenSource();
        var usecase = new CooperativeCancelUsecase(cts) { RunInBackground = true };

        await Should.ThrowAsync<OperationCanceledException>(
            () => usecase.CallAsync(new NumberParams(1), cts.Token));
    }

    [Fact]
    public async Task CallAsync_OCESemTokenCancelado_EInesperada_ViraOnUnexpected()
    {
        // Um OCE lançado pelo Process SEM o token do chamador cancelado é um bug como outro
        // qualquer — cai no OnUnexpected, não propaga.
        var usecase = new ThrowingUsecase { ToThrow = new OperationCanceledException("oce interno") };

        var result = await usecase.CallAsync(new NumberParams(0));

        var error = result.ShouldBeFailure().Error;
        (error is UnexpectedError).ShouldBeTrue();
        error.Text().ShouldContain("oce interno");
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var usecase = new DoubleUsecase { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new NumberParams(5));

        result.ShouldBeSuccess().Value.ShouldBe(10);
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_ChamaOnExecutionTimeMeasured()
    {
        // O hook virtual é o ponto de observabilidade (funciona no binário Release do pacote,
        // diferente de Debug.WriteLine); a sobrescrita pluga o logger do consumidor.
        var usecase = new MeasuringUsecase { MonitorExecutionTime = true };

        await usecase.CallAsync(new NumberParams(1));

        usecase.Measured.ShouldNotBeNull();
        usecase.Measured.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task CallAsync_SemMonitorExecutionTime_NaoChamaOnExecutionTimeMeasured()
    {
        var usecase = new MeasuringUsecase(); // MonitorExecutionTime = false (padrão)

        await usecase.CallAsync(new NumberParams(1));

        usecase.Measured.ShouldBeNull();
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
