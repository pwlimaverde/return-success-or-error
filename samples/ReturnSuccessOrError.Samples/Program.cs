using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError;
using ReturnSuccessOrError.Samples.Composition;
using ReturnSuccessOrError.Samples.Features.CheckConnection;
using ReturnSuccessOrError.Samples.Features.Fibonacci;
using ReturnSuccessOrError.Samples.Features.SalesReport;

// Composition Root da aplicação: ponto ÚNICO de DI (PRD §5.9–5.10).
// O core não depende de DI; os métodos AddXxxFeature() e o agregador AddFeatures()
// são definidos aqui, no consumidor.
var services = new ServiceCollection()
    .AddFeatures();

await using var provider = services.BuildServiceProvider();

await RunCheckConnectionAsync(provider);
await RunFibonacciAsync(provider);
await RunSalesReportAsync(provider);

return;

// ───────────────────────────── Feature 1: CheckConnection ─────────────────────────────
// Demonstra: sucesso, erro de NEGÓCIO (offline, no Process) e exceção TRADUZIDA pelo
// Repository.MapError (timeout → ConnectionTimeout) — tudo dentro do union fechado da feature.
static async Task RunCheckConnectionAsync(IServiceProvider provider)
{
    Header("1) CheckConnection — union de erro fechado (Offline | ConnectionTimeout | ErrorGeneric)");

    var service = provider.GetRequiredService<ICheckConnectionService>();
    Print("online (via DI)", await service.CheckAsync(), CheckConnectionErrorText.Describe);

    var offline = new CheckConnectionUsecase(
        new CheckConnectionRepository(new FakeConnectivityDataSource(online: false)));
    Print("offline", await offline.CallAsync(NoParams.Value), CheckConnectionErrorText.Describe);

    var throwing = new CheckConnectionUsecase(
        new CheckConnectionRepository(new FakeConnectivityDataSource(online: true, shouldThrow: true)));
    Print("exceção na fonte (→ MapError)", await throwing.CallAsync(NoParams.Value),
        CheckConnectionErrorText.Describe);
}

// ───────────────────────────── Feature 2: Fibonacci ─────────────────────────────
static async Task RunFibonacciAsync(IServiceProvider provider)
{
    Header("2) Fibonacci — UsecaseBase (lógica pura) com RunInBackground");

    var service = provider.GetRequiredService<IFibonacciService>();

    Print("Fib(35) em background", await service.CalculateAsync(35), FibonacciErrorText.Describe);
    Print("Fib(-1) inválido", await service.CalculateAsync(-1), FibonacciErrorText.Describe);
}

// ───────────────────────────── Feature 3: SalesReport ─────────────────────────────
// Demonstra PORTABILIDADE: o MESMO usecase com dois datasources (in-memory × CSV).
static async Task RunSalesReportAsync(IServiceProvider provider)
{
    Header("3) SalesReport — portabilidade (mesmo usecase, 2 datasources) + direto × background");

    const int rows = 50_000;

    var fromMemory = new GenerateSalesReportUsecase(
        new SalesRepository(new InMemorySalesDataSource()));
    Print("via InMemoryDataSource", await fromMemory.CallAsync(new SalesReportParameters(rows)),
        SalesErrorText.Describe);

    var csv = BuildCsv(rows);
    var fromCsv = new GenerateSalesReportUsecase(
        new SalesRepository(new CsvSalesDataSource(csv)));
    Print("via CsvDataSource (mesmo usecase!)", await fromCsv.CallAsync(new SalesReportParameters(rows)),
        SalesErrorText.Describe);

    var directRepo = new SalesRepository(new InMemorySalesDataSource());
    var direct = new GenerateSalesReportUsecase(directRepo) { MonitorExecutionTime = true };
    var swDirect = Stopwatch.GetTimestamp();
    var rDirect = await direct.CallAsync(new SalesReportParameters(rows));
    Console.WriteLine($"   [direto]     {Stopwatch.GetElapsedTime(swDirect).TotalMilliseconds:F1}ms");
    Print("relatório (direto)", rDirect, SalesErrorText.Describe);

    var service = provider.GetRequiredService<ISalesReportService>();
    var swBg = Stopwatch.GetTimestamp();
    var rBg = await service.GenerateAsync(rows);
    Console.WriteLine($"   [background] {Stopwatch.GetElapsedTime(swBg).TotalMilliseconds:F1}ms");
    Print("relatório (background)", rBg, SalesErrorText.Describe);
}

// CSV determinístico, mesmo conteúdo da fonte em memória — para a demonstração de portabilidade.
static string BuildCsv(int rowCount)
{
    string[] products = ["Mouse", "Teclado", "Monitor", "Headset", "Webcam"];
    var sb = new System.Text.StringBuilder(rowCount * 16);
    for (var i = 0; i < rowCount; i++)
        sb.Append(products[i % products.Length]).Append(';')
          .Append((i % 5) + 1).Append(';')
          .Append(50 + (i % 100)).Append('\n');
    return sb.ToString();
}

// ───────────────────────────── Helpers de apresentação ─────────────────────────────
static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 70));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 70));
}

// O erro é um union fechado por feature; cada feature fornece o Describe exaustivo.
static void Print<TValue, TError>(
    string label, ReturnSuccessOrError<TValue, TError> result, Func<TError, string> describe) =>
    Console.WriteLine(result.Match(
        onSuccess: value => $"   [OK]  {label}: {value}",
        onError: error => $"   [ERR] {label}: {describe(error)}"));
