# Plano de Desenvolvimento — ReturnSuccessOrError (.NET)

**Produto:** Biblioteca NuGet `ReturnSuccessOrError`
**Alvo:** .NET 9 / C# 13
**Objetivo:** Desenvolver e publicar uma biblioteca de domínio para o ecossistema .NET, implementando um result type discriminado e bases de caso de uso para Clean Architecture.
**Documento de referência:** `docs/PRD.md`
**Data:** 2026-06-22

---

## 1. Estrutura da Solution

```
ReturnSuccessOrError/
├── ReturnSuccessOrError.sln
├── Directory.Build.props                       # propriedades compartilhadas
├── README.md                                   # vai no pacote NuGet
├── LICENSE                                      # MIT
├── .editorconfig                               # convenções de estilo + analyzers
├── src/
│   └── ReturnSuccessOrError/
│       ├── ReturnSuccessOrError.csproj
│       ├── Core/
│       │   ├── ReturnSuccessOrError.cs         # tipo central + Success/Failure + Match
│       │   ├── Unit.cs
│       │   └── Nil.cs
│       ├── Errors/
│       │   ├── IAppError.cs
│       │   └── ErrorGeneric.cs
│       ├── Parameters/
│       │   ├── IParametersReturnResult.cs
│       │   └── NoParams.cs
│       ├── DataSources/
│       │   └── IDataSource.cs
│       └── Usecases/
│           ├── UsecaseBase.cs
│           └── UsecaseBaseCallData.cs
├── tests/
│   └── ReturnSuccessOrError.Tests/
│       ├── ReturnSuccessOrError.Tests.csproj
│       ├── Core/
│       │   └── ReturnSuccessOrErrorTests.cs
│       ├── Errors/
│       │   └── AppErrorTests.cs
│       ├── Parameters/
│       │   └── ParametersTests.cs
│       ├── DataSources/
│       │   └── DataSourceTests.cs
│       └── Usecases/
│           ├── UsecaseBaseTests.cs
│           └── UsecaseBaseCallDataTests.cs
└── samples/
    └── ReturnSuccessOrError.Samples/
        ├── ReturnSuccessOrError.Samples.csproj
        ├── Program.cs
        └── Features/
            ├── CheckConnection/
            ├── Fibonacci/
            └── SalesReport/
```

---

## 2. Configuração dos Projetos

### 2.1 `Directory.Build.props` (raiz)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

### 2.2 `src/ReturnSuccessOrError/ReturnSuccessOrError.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IsAotCompatible>true</IsAotCompatible>

    <!-- Metadados NuGet -->
    <PackageId>ReturnSuccessOrError</PackageId>
    <Version>1.0.0</Version>
    <Authors>pwlimaverde</Authors>
    <Description>Result type discriminado (Success/Failure) e bases de caso de uso para Clean Architecture em .NET. Separa busca de dados (I/O) de processamento (CPU-bound) com background opcional. Zero dependências de runtime.</Description>
    <PackageTags>clean-architecture;usecase;result;error-handling;railway;discriminated-union;functional</PackageTags>
    <RepositoryUrl>https://github.com/pwlimaverde/return-success-or-error-dotnet</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/pwlimaverde/return-success-or-error-dotnet</PackageProjectUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

### 2.3 `tests/ReturnSuccessOrError.Tests/ReturnSuccessOrError.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="FluentAssertions" Version="6.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ReturnSuccessOrError\ReturnSuccessOrError.csproj" />
  </ItemGroup>

</Project>
```

### 2.4 `samples/ReturnSuccessOrError.Samples.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ReturnSuccessOrError\ReturnSuccessOrError.csproj" />
  </ItemGroup>
</Project>
```

---

## 3. Ordem de Implementação

A ordem respeita as dependências entre tipos (de baixo para cima):

| # | Componente | Arquivo | Depende de |
|---|---|---|---|
| 1 | `IAppError` | `Errors/IAppError.cs` | — |
| 2 | `ErrorGeneric` | `Errors/ErrorGeneric.cs` | `IAppError` |
| 3 | `IParametersReturnResult` | `Parameters/IParametersReturnResult.cs` | `IAppError` |
| 4 | `NoParams` | `Parameters/NoParams.cs` | `IParametersReturnResult`, `ErrorGeneric` |
| 5 | `Unit`, `Nil` | `Core/Unit.cs`, `Core/Nil.cs` | — |
| 6 | `ReturnSuccessOrError<T>` | `Core/ReturnSuccessOrError.cs` | `IAppError` |
| 7 | `IDataSource<T>` | `DataSources/IDataSource.cs` | `IParametersReturnResult` |
| 8 | `UsecaseBase<T>` | `Usecases/UsecaseBase.cs` | itens 1–6 |
| 9 | `UsecaseBaseCallData<T,D>` | `Usecases/UsecaseBaseCallData.cs` | itens 1–7 |

> O código de referência completo de cada componente está na **Seção 5 do PRD** (`docs/PRD.md`). Este plano não duplica o código; usa-o como fonte de verdade.

### 3.1 Convenções de API (resumo)

| Conceito | Nome em C# | Observação |
|---|---|---|
| Tipo de resultado | `ReturnSuccessOrError<TValue>` | `abstract record`, construtor privado |
| Caso de sucesso | `ReturnSuccessOrError<TValue>.Success` | `sealed record`, `.Value` |
| Caso de erro | `ReturnSuccessOrError<TValue>.Failure` | `sealed record`, `.Error` |
| Fábricas | `.Ok(value)` / `.Err(error)` | estáticas |
| Consumo | `.Match(onSuccess, onError)` / `switch` | exaustivo |
| Erro | `IAppError` + `ErrorGeneric` | `WithMessage` preserva tipo |
| Parâmetros | `IParametersReturnResult` + `NoParams` | expõem `Error` |
| Fonte de dados | `IDataSource<TData>` | `CallAsync(parameters, ct)` |
| Caso de uso puro | `UsecaseBase<TValue>` | método abstrato `Process(parameters)` |
| Caso de uso c/ dados | `UsecaseBaseCallData<TValue, TData>` | método abstrato `Process(data, parameters)` |
| Flag background | `RunInBackground` (`init`) | afeta só o processamento |
| Flag medição | `MonitorExecutionTime` (`init`) | log via `Debug.WriteLine` |
| Códigos de erro | `Cod. 02-1` (fetch), `Cod. BackgroundCatch` (process) | rastreabilidade |

---

## 4. Estratégia de Testes

### 4.1 Frameworks

- **xUnit** — runner e asserts base.
- **NSubstitute** — substitutos para `IDataSource<T>`.
- **FluentAssertions** — asserts legíveis (`result.Should().BeOfType<...>()`).

### 4.2 Matriz de Cenários

**`Core/ReturnSuccessOrErrorTests.cs`**
- `Ok` cria `Success` com `Value` correto.
- `Err` cria `Failure` com `Error` correto.
- Igualdade por valor: dois `Success` com mesmo valor são iguais; dois `Failure` com mesmo erro são iguais.
- `Match` chama o ramo correto e retorna o valor esperado.
- `Switch` executa a ação correta.
- `Unit.Value`/`Nil.Value` são singletons; `ToString` correto.

**`Errors/AppErrorTests.cs`**
- `ErrorGeneric` — comparação por valor, `ToString` = `"ErrorGeneric - <msg>"`.
- `WithMessage` em `ErrorGeneric` devolve `ErrorGeneric` com nova mensagem.
- `WithMessage` em erro customizado preserva o tipo concreto e demais campos.

**`Parameters/ParametersTests.cs`**
- `NoParams()` sem erro → `Error` é `ErrorGeneric` com mensagem default.
- `NoParams(erroCustom)` → `Error` é o erro fornecido.

**`DataSources/DataSourceTests.cs`**
- Implementação fake retorna dado em sucesso.
- Implementação fake lança em falha (validar via caso de uso).

**`Usecases/UsecaseBaseTests.cs`**
- Execução direta retorna resultado de `Process`.
- `RunInBackground = true` retorna resultado idêntico ao direto (paridade).
- Exceção em `Process` com background → `Failure` com `Cod. BackgroundCatch`.
- `MonitorExecutionTime = true` não altera o resultado.
- Resultado `Unit` e `Nil`.

**`Usecases/UsecaseBaseCallDataTests.cs`**
- Sucesso completo: `Process` recebe o dado do fetch e devolve `Success`.
- **Curto-circuito**: quando o fetch lança, `Process` **não** é chamado (verificar com flag/spy).
- Falha no fetch → `Failure` com `Cod. 02-1` e tipo de erro preservado.
- Exceção no `Process` em background → `Cod. BackgroundCatch`.
- Paridade direto ↔ background (mesmo resultado).
- `CancellationToken` é repassado ao `IDataSource.CallAsync`.

### 4.3 Exemplos de Teste

```csharp
public class UsecaseBaseCallDataTests
{
    private sealed record TestParams(IAppError Error) : IParametersReturnResult;

    private sealed class StringUsecase(IDataSource<int> ds, Action? onProcess = null)
        : UsecaseBaseCallData<string, int>(ds)
    {
        protected override ReturnSuccessOrError<string> Process(int data, IParametersReturnResult p)
        {
            onProcess?.Invoke();
            return ReturnSuccessOrError<string>.Ok($"valor: {data}");
        }
    }

    [Fact]
    public async Task CallAsync_QuandoFetchFalha_RetornaCod021_ESemChamarProcess()
    {
        // Arrange
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<IParametersReturnResult>(), Arg.Any<CancellationToken>())
          .ThrowsAsync(new InvalidOperationException("db down"));

        var processChamado = false;
        var usecase = new StringUsecase(ds, () => processChamado = true);

        // Act
        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha")));

        // Assert
        var failure = result.Should().BeOfType<ReturnSuccessOrError<string>.Failure>().Subject;
        failure.Error.Message.Should().Contain("Cod. 02-1");
        processChamado.Should().BeFalse();
    }

    [Fact]
    public async Task CallAsync_ComSucesso_ProcessaDadoDoFetch()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<IParametersReturnResult>(), Arg.Any<CancellationToken>())
          .Returns(42);

        var usecase = new StringUsecase(ds);

        var result = await usecase.CallAsync(new TestParams(new ErrorGeneric("falha")));

        result.Match(
            onSuccess: v => v,
            onError: e => e.Message
        ).Should().Be("valor: 42");
    }

    [Fact]
    public async Task CallAsync_PropagaCancellationToken()
    {
        var ds = Substitute.For<IDataSource<int>>();
        ds.CallAsync(Arg.Any<IParametersReturnResult>(), Arg.Any<CancellationToken>()).Returns(1);
        var usecase = new StringUsecase(ds);
        using var cts = new CancellationTokenSource();

        await usecase.CallAsync(new TestParams(new ErrorGeneric("x")), cts.Token);

        await ds.Received(1).CallAsync(Arg.Any<IParametersReturnResult>(), cts.Token);
    }
}
```

### 4.4 Cobertura

- Meta: **> 90%** de cobertura de linhas no projeto principal.
- Coletar com `dotnet test --collect:"XPlat Code Coverage"` (coverlet) + relatório via `reportgenerator`.

---

## 5. Samples

Três features demonstrando os modos de uso, em um único Console App (`Program.cs` executa as três em sequência):

### 5.1 `CheckConnection` — `UsecaseBaseCallData`, 3 cenários
- `FakeConnectivityDataSource(online, shouldThrow)`.
- `CheckConnectionUsecase` mapeia `bool` → mensagem.
- Demonstra: sucesso, erro de negócio (offline), exceção capturada (`Cod. 02-1`).

```csharp
public sealed class CheckConnectionUsecase(IDataSource<bool> ds)
    : UsecaseBaseCallData<string, bool>(ds)
{
    protected override ReturnSuccessOrError<string> Process(bool online, IParametersReturnResult p)
        => online
            ? ReturnSuccessOrError<string>.Ok("You are connected")
            : ReturnSuccessOrError<string>.Err(p.Error.WithMessage("You are offline"));
}
```

### 5.2 `Fibonacci` — `UsecaseBase` com `RunInBackground`
- `FibonacciParameters(N, Error)`.
- Cálculo CPU-bound; demonstra `RunInBackground = true` despachando para o thread pool.

```csharp
public sealed class FibonacciUsecase : UsecaseBase<long>
{
    protected override ReturnSuccessOrError<long> Process(IParametersReturnResult p)
    {
        var fp = (FibonacciParameters)p;
        if (fp.N < 0)
            return ReturnSuccessOrError<long>.Err(p.Error.WithMessage("N deve ser >= 0"));
        return ReturnSuccessOrError<long>.Ok(Fib(fp.N));
    }

    private static long Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);
}
```

### 5.3 `SalesReport` — fetch + process pesado, comparativo direto × background
- `FakeSalesDataSource(rowCount)` gera N linhas cruas (`List<Dictionary<string, object>>` ou `IReadOnlyList<SalesRow>`).
- `GenerateSalesReportUsecase` agrega (faturamento, ticket médio, produto mais vendido).
- Demonstra `MonitorExecutionTime = true` comparando processamento direto vs. background com 50.000 linhas.

```csharp
public sealed record SalesReport(
    int TotalItems, decimal TotalRevenue, decimal AverageTicket, string TopProduct);

public sealed class GenerateSalesReportUsecase(IDataSource<IReadOnlyList<SalesRow>> ds)
    : UsecaseBaseCallData<SalesReport, IReadOnlyList<SalesRow>>(ds)
{
    protected override ReturnSuccessOrError<SalesReport> Process(
        IReadOnlyList<SalesRow> rows, IParametersReturnResult p)
    {
        if (rows.Count == 0)
            return ReturnSuccessOrError<SalesReport>.Err(
                p.Error.WithMessage("Sem vendas no período"));

        decimal revenue = 0; int items = 0;
        var byProduct = new Dictionary<string, decimal>();
        foreach (var r in rows)
        {
            var total = r.Quantity * r.UnitPrice;
            revenue += total; items += r.Quantity;
            byProduct[r.Product] = byProduct.GetValueOrDefault(r.Product) + total;
        }
        var top = byProduct.MaxBy(kv => kv.Value).Key;

        return ReturnSuccessOrError<SalesReport>.Ok(new SalesReport(
            items, revenue, revenue / rows.Count, top));
    }
}
```

---

## 6. Publicação no NuGet

### 6.1 Empacotamento local

```bash
dotnet pack src/ReturnSuccessOrError/ReturnSuccessOrError.csproj \
  --configuration Release \
  --output ./artifacts
```

### 6.2 Validação do pacote

```bash
dotnet tool install --global dotnet-validate
dotnet-validate package local ./artifacts/ReturnSuccessOrError.1.0.0.nupkg
```

### 6.3 Publicação manual

```bash
dotnet nuget push ./artifacts/ReturnSuccessOrError.1.0.0.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key "$NUGET_API_KEY"
```

### 6.4 CI/CD — GitHub Actions

```yaml
# .github/workflows/ci.yml — build + test em cada push/PR
name: CI
on: [push, pull_request]
jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage"
```

```yaml
# .github/workflows/publish.yml — publica ao criar tag vX.Y.Z
name: Publish NuGet
on:
  push:
    tags: [ 'v*' ]
jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet test --configuration Release
      - run: dotnet pack src/ReturnSuccessOrError/ReturnSuccessOrError.csproj
               --configuration Release --output ./artifacts
      - run: dotnet nuget push ./artifacts/*.nupkg
               --source https://api.nuget.org/v3/index.json
               --api-key ${{ secrets.NUGET_API_KEY }}
               --skip-duplicate
```

> **Pré-requisito:** registrar o segredo `NUGET_API_KEY` no repositório e confirmar que o `PackageId` `ReturnSuccessOrError` está disponível no NuGet.org.

### 6.5 Versionamento

Seguir **SemVer**:
- `1.0.0` — primeira release estável.
- `1.x` — adições retrocompatíveis (ex.: conversões implícitas, `ILogger`).
- `2.0.0` — mudanças que quebram a API pública.

---

## 7. Documentação

### 7.1 `README.md` (raiz, incluído no pacote)
- O que é e qual problema resolve.
- Instalação: `dotnet add package ReturnSuccessOrError`.
- Guia rápido (3 exemplos progressivos: lógica pura, com fonte de dados, em ASP.NET Core).
- Tabela de API.
- Como testar casos de uso.
- Posicionamento vs. `OneOf`/`ErrorOr`/`FluentResults`.

### 7.2 Documentação XML
- Todos os tipos e membros públicos com `///` (já habilitado via `GenerateDocumentationFile`).

### 7.3 Integração com DI (seção do README)

```csharp
// Registro
services.AddScoped<IDataSource<IReadOnlyList<SalesRow>>, SqlSalesDataSource>();
services.AddScoped<GenerateSalesReportUsecase>(sp =>
    new GenerateSalesReportUsecase(sp.GetRequiredService<IDataSource<IReadOnlyList<SalesRow>>>())
    { RunInBackground = true });

// Consumo em um endpoint Minimal API
app.MapGet("/sales", async (GenerateSalesReportUsecase usecase, CancellationToken ct) =>
{
    var result = await usecase.CallAsync(
        new SalesReportParameters(6, 2026, new ErrorGeneric("Falha ao gerar relatório")), ct);

    return result.Match(
        onSuccess: report => Results.Ok(report),
        onError:   error  => Results.Problem(error.Message));
});
```

---

## 8. Cronograma Estimado

| Fase | Tarefas | Estimativa |
|---|---|---|
| 1 | Solution, projetos, `Directory.Build.props`, `.editorconfig` | 1 dia |
| 2 | Implementação do core (itens 1–9 da Seção 3) | 3–4 dias |
| 3 | Testes unitários (matriz da Seção 4) | 3–4 dias |
| 4 | Samples (3 features) | 1–2 dias |
| 5 | README + documentação XML | 1 dia |
| 6 | NuGet + CI/CD (GitHub Actions) | 1 dia |
| **Total** | | **10–13 dias** |

---

## 9. Checklist de Qualidade (antes de publicar)

### Build & Testes
- [ ] `dotnet build -c Release` sem warnings (`TreatWarningsAsErrors=true`).
- [ ] `dotnet test` 100% verde.
- [ ] Cobertura > 90% no projeto principal.
- [ ] Nullable reference types sem warnings.

### API
- [ ] `ReturnSuccessOrError<T>` selado (construtor privado); `Success`/`Failure` aninhados.
- [ ] `Match` e `Switch` exaustivos.
- [ ] `IAppError.WithMessage` preserva tipo concreto (coberto por teste).
- [ ] `IParametersReturnResult` + `NoParams` com erro default.
- [ ] `IDataSource<T>.CallAsync` com `CancellationToken`.
- [ ] `UsecaseBase<T>` e `UsecaseBaseCallData<T,D>` com `Process` abstrato.
- [ ] `Cod. 02-1` (fetch) e `Cod. BackgroundCatch` (process) cobertos por teste.
- [ ] Curto-circuito verificado (process não chamado em falha de fetch).
- [ ] `Unit` e `Nil` como singletons.

### Empacotamento
- [ ] `dotnet pack` gera `.nupkg` + `.snupkg`.
- [ ] `dotnet-validate` sem erros.
- [ ] README, LICENSE (MIT) e documentação XML incluídos no pacote.
- [ ] `PackageId` disponível no NuGet.org.
- [ ] Instalação testada em projeto consumidor separado.

### Compatibilidade
- [ ] Compila e roda em `net9.0`.
- [ ] Verificado AOT-friendly (sem reflexão; `IsAotCompatible=true`).

---

## 10. Roadmap Pós-1.0

| Versão | Item | Justificativa |
|---|---|---|
| 1.1 | Conversões implícitas (`TValue` → `Success`, `IAppError` → `Failure`) | Reduz verbosidade na criação de resultados |
| 1.2 | Métodos de composição (`Map`, `Bind`/`Then`, `Ensure`) | Encadeamento estilo railway |
| 1.3 | Injeção opcional de `ILogger<T>` para `MonitorExecutionTime` | Observabilidade estruturada em produção |
| 1.4 | Multi-targeting `netstandard2.1` | Ampliar alcance a consumidores legados |
| 2.0 | Avaliar source generator para reduzir boilerplate de casos de uso | Ergonomia, se houver demanda |

---

## 11. Referências

- **PRD:** `docs/PRD.md`
- **C# records:** https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record
- **Pattern matching:** https://learn.microsoft.com/dotnet/csharp/language-reference/operators/patterns
- **Task Parallel Library:** https://learn.microsoft.com/dotnet/standard/parallel-programming/task-parallel-library-tpl
- **Publicar pacote NuGet:** https://learn.microsoft.com/nuget/nuget-org/publish-a-package
- **Native AOT:** https://learn.microsoft.com/dotnet/core/deploying/native-aot/
- **Railway Oriented Programming:** https://fsharpforfunandprofit.com/rop/
