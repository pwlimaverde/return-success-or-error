using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError;
using ReturnSuccessOrError.Samples.Composition;
using ReturnSuccessOrError.Samples.Features.CheckConnection;
using ReturnSuccessOrError.Samples.Features.Fibonacci;
using ReturnSuccessOrError.Samples.Features.SalesReport;

// Composition Root da aplicação: registra as features pela metodologia da PRD §5.10.
// O core não depende de DI; IFeatureModule/AddFeatures são definidos aqui, no consumidor.
var services = new ServiceCollection()
    .AddFeatures(
        new CheckConnectionModule(),
        new FibonacciModule(),
        new SalesReportModule());

await using var provider = services.BuildServiceProvider();

await RunCheckConnectionAsync(provider);
await RunFibonacciAsync(provider);
await RunSalesReportAsync(provider);

return;

// ───────────────────────────── Feature 1: CheckConnection ─────────────────────────────
// Demonstra: sucesso, erro de NEGÓCIO (offline) e exceção de infra capturada (DataSourceCatch).
static async Task RunCheckConnectionAsync(IServiceProvider provider)
{
    Header("1) CheckConnection — UsecaseBaseCallData (3 cenários)");

    // Cenário A: online — resolvido via DI (módulo registra a fonte online).
    var service = provider.GetRequiredService<CheckConnectionService>();
    Print("online (via DI)", await service.CheckAsync());

    // Cenário B: offline — erro de negócio devolvido pelo próprio Process.
    var offline = new CheckConnectionUsecase(new FakeConnectivityDataSource(online: false));
    Print("offline", await offline.CallAsync(
        new CheckConnectionParameters(new ErrorGeneric("Falha ao verificar conectividade"))));

    // Cenário C: exceção na fonte de dados — capturada e enriquecida com DataSourceCatch.
    var throwing = new CheckConnectionUsecase(
        new FakeConnectivityDataSource(online: true, shouldThrow: true));
    Print("exceção na fonte", await throwing.CallAsync(
        new CheckConnectionParameters(new ErrorGeneric("Falha ao verificar conectividade"))));
}

// ───────────────────────────── Feature 2: Fibonacci ─────────────────────────────
// Demonstra: UsecaseBase com RunInBackground = true (cálculo CPU-bound no thread pool).
static async Task RunFibonacciAsync(IServiceProvider provider)
{
    Header("2) Fibonacci — UsecaseBase com RunInBackground");

    var service = provider.GetRequiredService<FibonacciService>();

    Print("Fib(35) em background", await service.CalculateAsync(35));
    Print("Fib(-1) inválido", await service.CalculateAsync(-1));
}

// ───────────────────────────── Feature 3: SalesReport ─────────────────────────────
// Demonstra: fetch + process pesado; comparação direto × background com MonitorExecutionTime.
static async Task RunSalesReportAsync(IServiceProvider provider)
{
    Header("3) SalesReport — fetch + process pesado (direto × background)");

    const int rows = 50_000;
    var dataSource = provider.GetRequiredService<IDataSource<IReadOnlyList<SalesRow>>>();

    // Direto (processamento na thread chamadora).
    var direct = new GenerateSalesReportUsecase(dataSource) { MonitorExecutionTime = true };
    var swDirect = Stopwatch.GetTimestamp();
    var rDirect = await direct.CallAsync(new SalesReportParameters(rows, new ErrorGeneric("Falha")));
    Console.WriteLine($"   [direto]     {Stopwatch.GetElapsedTime(swDirect).TotalMilliseconds:F1}ms");
    Print("relatório (direto)", rDirect);

    // Background (processamento despachado ao thread pool) — resolvido via DI.
    var service = provider.GetRequiredService<SalesReportService>();
    var swBg = Stopwatch.GetTimestamp();
    var rBg = await service.GenerateAsync(rows);
    Console.WriteLine($"   [background] {Stopwatch.GetElapsedTime(swBg).TotalMilliseconds:F1}ms");
    Print("relatório (background)", rBg);
}

// ───────────────────────────── Helpers de apresentação ─────────────────────────────
static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 70));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 70));
}

static void Print<T>(string label, ReturnSuccessOrError<T> result) =>
    Console.WriteLine(result.Match(
        onSuccess: value => $"   [OK]  {label}: {value}",
        onError: error => $"   [ERR] {label}: {error.Message}"));
