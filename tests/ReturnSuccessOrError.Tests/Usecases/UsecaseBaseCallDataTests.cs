using NSubstitute;
using ReturnSuccessOrError;
using Shouldly;
using Xunit;

namespace ReturnSuccessOrError.Tests.Usecases;

public class UsecaseBaseCallDataTests
{
    // public: é argumento de tipo da interface mockada (NSubstitute/Castle precisa de acesso).
    public sealed record TestParams : Parameters;

    private sealed class StringUsecase(IRepository<int, TestParams, TestError> repo, Action? onProcess = null)
        : UsecaseBaseCallData<string, int, TestParams, TestError>(repo)
    {
        protected override ReturnSuccessOrError<string, TestError> Process(int data, TestParams p)
        {
            onProcess?.Invoke();
            return $"valor: {data}";
        }

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError(exception.Message);
    }

    private sealed class ThrowingProcessUsecase(IRepository<int, TestParams, TestError> repo)
        : UsecaseBaseCallData<string, int, TestParams, TestError>(repo)
    {
        protected override ReturnSuccessOrError<string, TestError> Process(int data, TestParams p)
            => throw new InvalidOperationException("process-boom");

        protected override TestError OnUnexpected(Exception exception)
            => new UnexpectedError($"capturado: {exception.Message}");
    }

    private static IRepository<int, TestParams, TestError> RepoReturning(ReturnSuccessOrError<int, TestError> result)
    {
        var repo = Substitute.For<IRepository<int, TestParams, TestError>>();
        repo.CallAsync(Arg.Any<TestParams>(), Arg.Any<CancellationToken>()).Returns(result);
        return repo;
    }

    [Fact]
    public async Task CallAsync_ComSucesso_ProcessaDadoDoFetch()
    {
        var usecase = new StringUsecase(RepoReturning(42));

        var result = await usecase.CallAsync(new TestParams());

        result.Match(
            onSuccess: v => v,
            onError: e => e switch
            {
                NotFoundError n   => n.Message,
                ValidationError v => v.Message,
                UnexpectedError u => u.Message,
            }
        ).ShouldBe("valor: 42");
    }

    [Fact]
    public async Task CallAsync_QuandoFetchFalha_CurtoCircuito_SemChamarProcess()
    {
        var processChamado = false;
        var repo = RepoReturning((TestError)new NotFoundError("falha de fetch"));
        var usecase = new StringUsecase(repo, () => processChamado = true);

        var result = await usecase.CallAsync(new TestParams());

        var error = result.ShouldBeFailure().Error;
        (error is NotFoundError).ShouldBeTrue();
        processChamado.ShouldBeFalse(); // curto-circuito: Process não é chamado
    }

    [Fact]
    public async Task CallAsync_QuandoFetchFalha_PreservaCasoConcretoDoErro()
    {
        var usecase = new StringUsecase(RepoReturning((TestError)new ValidationError("inválido")));

        var result = await usecase.CallAsync(new TestParams());

        var error = result.ShouldBeFailure().Error;
        (error is ValidationError).ShouldBeTrue();
        error.Text().ShouldBe("inválido");
    }

    [Fact]
    public async Task CallAsync_Background_ExcecaoInesperada_ViraOnUnexpected()
    {
        var usecase = new ThrowingProcessUsecase(RepoReturning(1)) { RunInBackground = true };

        var result = await usecase.CallAsync(new TestParams());

        var error = result.ShouldBeFailure().Error;
        (error is UnexpectedError).ShouldBeTrue();
        error.Text().ShouldContain("process-boom");
    }

    [Fact]
    public async Task CallAsync_Direto_ExcecaoInesperada_TambemViraOnUnexpected()
    {
        // Contrato novo: com fetch OK, uma exceção inesperada no Process vira OnUnexpected
        // tanto direto quanto em background — nada propaga.
        var usecase = new ThrowingProcessUsecase(RepoReturning(1)); // RunInBackground = false

        var result = await usecase.CallAsync(new TestParams());

        (result.ShouldBeFailure().Error is UnexpectedError).ShouldBeTrue();
    }

    [Fact]
    public async Task CallAsync_ParidadeDiretoBackground()
    {
        var repo = RepoReturning(7);
        var direto = new StringUsecase(repo);
        var background = new StringUsecase(repo) { RunInBackground = true };

        var rDireto = await direto.CallAsync(new TestParams());
        var rBackground = await background.CallAsync(new TestParams());

        rBackground.ShouldBe(rDireto);
    }

    [Fact]
    public async Task CallAsync_ComMonitorExecutionTime_NaoAlteraResultado()
    {
        var usecase = new StringUsecase(RepoReturning(42)) { MonitorExecutionTime = true };

        var result = await usecase.CallAsync(new TestParams());

        result.ShouldBeSuccess().Value.ShouldBe("valor: 42");
    }

    [Fact]
    public async Task CallAsync_PropagaCancellationToken()
    {
        var repo = RepoReturning(1);
        var usecase = new StringUsecase(repo);
        using var cts = new CancellationTokenSource();

        await usecase.CallAsync(new TestParams(), cts.Token);

        await repo.Received(1).CallAsync(Arg.Any<TestParams>(), cts.Token);
    }
}
