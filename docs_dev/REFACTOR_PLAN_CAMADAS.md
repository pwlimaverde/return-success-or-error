# Plano de Refatoração — Separação do Erro + Padronização de Camadas (3/2)

> **Status:** proposto (aguardando implementação).
> **Escopo:** evolução arquitetural do núcleo da lib para suportar o padrão
> `DataSource → Repository → UseCase`, separar o erro dos parâmetros e padronizar
> os parâmetros de tipo genérico em **3 (usecase) / 2 (data, repo)**.
> **Pré-condição favorável:** a 1.0.0 ainda **não foi publicada** — *breaking changes*
> agora não quebram consumidores.

---

## 1. Objetivo

Transformar a lib num **padrão de projeto interno** para os projetos do autor:

- Casos de uso **desacoplados** e **portáveis** — levar um `LoginUsecase` de um
  projeto a outro **trocando apenas o datasource**.
- **Clean Architecture + SOLID** aplicados de ponta a ponta.
- Núcleo padronizado, com a complexidade de orquestração abstraída nas classes base.

A regra que torna o usecase portável é o **DIP**: o usecase depende de abstrações
(`IRepository`), nunca da infraestrutura concreta. Troca-se a implementação de I/O
sem tocar na regra de negócio.

---

## 2. Decisões consolidadas (da fase de design)

Estas decisões foram fechadas ao longo da discussão de arquitetura e são a base do plano:

1. **O erro sai dos parâmetros.** `Parameters` carrega **só dados**. O erro deixa de
   ser pré-carregado na entrada (prática incomum confirmada como anti-idiomática frente
   a ErrorOr/FluentResults/MediatR).
2. **Tratamento de erro por camada (Anti-Corruption Layer).** O `DataSource` permanece
   "burro" (retorna dado bruto **ou** lança exceção técnica); o `Repository` é a
   **fronteira** que traduz exceção de infraestrutura em erro de domínio.
3. **Camada `Repository` entre DataSource e UseCase.** Padrão canônico de Clean
   Architecture: a camada de dados retorna `Result<T>`; data sources nunca são
   acessados diretamente por use cases.
4. **Família de erros estruturada previamente.** `AppError` é a raiz **aberta**
   (extensível); o domínio define `ApiError`, `ConError`, `DataError`, etc. O consumo
   discrimina por tipo via pattern matching.
5. **Parâmetros de tipo explícitos (3/2).** C# não tem *higher-kinded types* nem
   *associated types* — não há "utilitário" (`class`/`enum`/`union`/`Parameters<…>`)
   que empacote e desempacote os tipos. O caminho idiomático é:
   - `IDataSource<TData, TParams>` — **2**
   - `IRepository<TData, TParams>` — **2**
   - `UsecaseBaseCallData<TValue, TData, TParams>` — **3** (só aqui)
   Mitigações: alias `using` (C# 12) para `TData` longo + nomes concretos por feature.
6. **Context object descartado.** Empacotar tudo num objeto mutável (`FeatureParams`
   com `Data`/`Result`) reduziria os type params, mas reintroduz mutabilidade, estados
   ilegais representáveis, *race conditions* com `RunInBackground` e acoplamento entre
   camadas — contrário aos pilares da lib.
7. **Criação só por conversão implícita** (já vigente): `return value;` / `return error;`.

---

## 3. Arquitetura-alvo

```
            Parameters (só dados, tipado) ── atravessa as 3 camadas ──┐
                                                                      │
  DADOS   │  IDataSource<TData,TParams>   →  dado bruto OU throw       │  infra (burra)
          │  IRepository<TData,TParams>   →  ReturnSuccessOrError<TData>   ← fronteira (MapError)
  ────────┼──────────────────────────────────────────────────────────
  DOMÍNIO │  Usecase<TValue,TData,TParams> →  Process(data, params)    │  regra de negócio

  ERROS   │  AppError (raiz) → família do domínio (ApiError, ConError, DataError…)
          │  ErrorGeneric → default do MapError + rede de segurança do inesperado
```

**Regra da dependência (DIP):** `Usecase → IRepository → IDataSource`, sempre apontando
para abstrações. O usecase nunca conhece a infra concreta → é isso que o torna portável.

### Fluxo de erro

| Origem | Como se expressa | Quem decide |
|---|---|---|
| Falha de I/O **esperada** (URL fora, timeout) | exceção técnica lançada pelo DataSource | `Repository.MapError` traduz em `ApiError`/`ConError` |
| Erro de **negócio** (filtro sem match) | `return new DataError(...)` | `Usecase.Process` |
| Exceção **inesperada** (bug) | exceção escapa | rede de segurança da base → `ErrorGeneric` |

---

## 4. Contratos (desenho de referência)

```csharp
// Camada de dados — infra burra
public interface IDataSource<TData, TParams> where TParams : Parameters
{
    Task<TData> CallAsync(TParams parameters, CancellationToken ct = default);
}

// Camada de dados — fronteira / tradução
public interface IRepository<TData, TParams> where TParams : Parameters
{
    Task<ReturnSuccessOrError<TData>> CallAsync(TParams parameters, CancellationToken ct = default);
}

public abstract class RepositoryBase<TData, TParams>(IDataSource<TData, TParams> dataSource)
    : IRepository<TData, TParams> where TParams : Parameters
{
    public async Task<ReturnSuccessOrError<TData>> CallAsync(TParams p, CancellationToken ct = default)
    {
        try   { return await dataSource.CallAsync(p, ct).ConfigureAwait(false); } // TData → Success
        catch (Exception ex) { return MapError(ex, p); }                          // AppError → Failure
    }

    protected virtual AppError MapError(Exception exception, TParams parameters)
        => new ErrorGeneric($"Falha na fonte de dados - {exception.Message}");
}

// Camada de domínio — regra de negócio
public abstract class UsecaseBaseCallData<TValue, TData, TParams>(IRepository<TData, TParams> repository)
    : UsecaseExecutorBase<TValue> where TParams : Parameters
{
    protected abstract ReturnSuccessOrError<TValue> Process(TData data, TParams parameters);

    public Task<ReturnSuccessOrError<TValue>> CallAsync(TParams p, CancellationToken ct = default)
        => MeasuredAsync(async () =>
        {
            var fetch = await repository.CallAsync(p, ct).ConfigureAwait(false); // já é Success|Failure
            return fetch switch
            {
                Failure f        => f,                                           // curto-circuito
                Success<TData> s => await ProcessStageAsync(() => Process(s.Value, p), p, ct)
                                          .ConfigureAwait(false),
            };
        });
}
```

### Uma feature completa (o que o consumidor escreve)

```csharp
// 1) Parâmetros — só dados
public sealed record ProdutosParams(string Url, string Filtro) : Parameters;

// 2) DataSource — só I/O, lança exceção TÉCNICA
public sealed class ProdutosApiDataSource(HttpClient http)
    : IDataSource<IReadOnlyList<Produto>, ProdutosParams>
{
    public async Task<IReadOnlyList<Produto>> CallAsync(ProdutosParams p, CancellationToken ct = default)
    {
        var resp = await http.GetAsync(p.Url, ct);
        resp.EnsureSuccessStatusCode();                                   // lança HttpRequestException
        return (await resp.Content.ReadFromJsonAsync<List<Produto>>(ct))!;
    }
}

// 3) Repository — só a tradução de erro (a base faz o resto)
public sealed class ProdutosRepository(IDataSource<IReadOnlyList<Produto>, ProdutosParams> ds)
    : RepositoryBase<IReadOnlyList<Produto>, ProdutosParams>(ds)
{
    protected override AppError MapError(Exception ex, ProdutosParams p) => ex switch
    {
        HttpRequestException => new ApiError("API indisponível", 503),
        TimeoutException     => new ConError("tempo esgotado"),
        _                    => new ErrorGeneric($"inesperado: {ex.Message}"),
    };
}

// 4) UseCase — só a regra de negócio (PORTÁVEL: depende de IRepository)
public sealed class ProdutosUsecase(IRepository<IReadOnlyList<Produto>, ProdutosParams> repo)
    : UsecaseBaseCallData<Relatorio, IReadOnlyList<Produto>, ProdutosParams>(repo)
{
    protected override ReturnSuccessOrError<Relatorio> Process(IReadOnlyList<Produto> dados, ProdutosParams p)
    {
        var filtrados = dados.Where(d => d.Categoria == p.Filtro).ToList();
        if (filtrados.Count == 0) return new DataError("sem produtos na categoria", p.Filtro);
        return new Relatorio(filtrados);
    }
}
```

---

## 5. Fases de implementação

Cada fase termina com **checkpoint**: `dotnet build -c Release` sem warnings + `dotnet test` verde.

### Fase 1 — Erros e Parâmetros
- `ParametersReturnResult` → **`Parameters`**: `abstract record` marcador, **só dados** (remove `AppError Error`).
- `NoParams` ajustado (sem `Error`).
- `AppError` (mantém `abstract record`) + `ErrorGeneric` (mantém). Documentar o **padrão de família**.
- `ErrorCodes` (`DataSourceCatch`/`BackgroundCatch`) e `ErrorTrace.WithCatch` mantidos como utilitário de rastreio opt-in.
- **Commit:** `refactor!: separa o erro dos parâmetros (Parameters vira só-dados)`

### Fase 2 — DataSource burro
- `IDataSource<TData, TParams>` `where TParams : Parameters`: `Task<TData> CallAsync(TParams, ct)`.
- Retorna dado cru **ou** `throw`. Zero conhecimento de domínio.
- **Commit:** `refactor!: IDataSource burro, tipado em TParams`

### Fase 3 — Repository (camada nova)
- `IRepository<TData, TParams>` + `RepositoryBase<TData, TParams>` com `MapError` (`virtual`, default `ErrorGeneric`).
- **Commit:** `feat: camada Repository (anti-corruption com MapError)`

### Fase 4 — UseCases
- `UsecaseExecutorBase<TValue>`: exceção no `Process` em background → **`ErrorGeneric`** (rede de segurança; sem `parameters.Error`).
- `UsecaseBaseCallData<TValue, TData, TParams>`: recebe **`IRepository`**; curto-circuito + `Process`; sem try/catch de fetch.
- `UsecaseBase<TValue, TParams>`: lógica pura, ganha `TParams`.
- **Commit:** `refactor!: usecases recebem Repository e tipam TParams`

### Fase 5 — Samples + Testes
- Reescrever os 3 samples no padrão de 4 peças. **Um sample demonstra portabilidade**: o mesmo usecase com dois datasources diferentes.
- Testes: Repository (`MapError`: sucesso, exceção→erro tratável, default), curto-circuito, paridade direto↔background, `CancellationToken`. Atualizar os existentes.
- **Commit:** `test+docs(samples): padrão de 3 camadas com portabilidade`

### Fase 6 — Documentação
- PRD (§5 contratos), DEVELOPMENT_PLAN, README (guia do padrão + **seção de portabilidade**), CHANGELOG, CLAUDE.md.
- Documentar o **padrão de projeto**: como criar uma feature, a regra de dependência, e como portar um usecase trocando o datasource.
- **Commit:** `docs: padrão de projeto (3 camadas, família de erros, portabilidade)`

---

## 6. Decisões embutidas (recomendações)

| # | Decisão | Recomendação |
|---|---|---|
| 1 | Nome do marcador de parâmetros | **`Parameters`** (era `ParametersReturnResult`; não retorna mais nada) |
| 2 | `MapError` no Repository | **`virtual`** com default `ErrorGeneric` (repo pode ser vazio quando não traduz) |
| 3 | Exceção inesperada (fetch/background) | vira **`ErrorGeneric`** — rede de segurança; `parameters.Error` deixa de existir |
| 4 | `UsecaseBase` puro | ganha só `TParams` (sem Repository/MapError — não tem fonte de dados) |

---

## 7. Impacto

- **Breaking changes** assumidos (lib não publicada): `IDataSource`, `UsecaseBaseCallData`, `ParametersReturnResult`.
- **Novos arquivos:** `IRepository.cs`, `RepositoryBase.cs`.
- **Renomeações:** `ParametersReturnResult.cs` → `Parameters.cs`.

## 8. Validação final

```bash
dotnet build -c Release                                     # sem warnings (TreatWarningsAsErrors)
dotnet test                                                 # 100% verde
dotnet test --collect:"XPlat Code Coverage"                 # cobertura mantida
dotnet run --project samples/ReturnSuccessOrError.Samples   # samples executam
```

---

## 9. Referências de fundamentação

- Anti-Corruption Layer — Microsoft Learn (Azure Architecture Center).
- Android Clean Architecture: Data Layer — ASOS Tech Blog / Android Developers.
- ErrorOr vs OneOf vs FluentResults — comparativo de Result pattern em .NET.
- SOLID / Dependency Inversion aplicado ao Repository Pattern.
- C# sem higher-kinded types — dotnet/csharplang #339.
