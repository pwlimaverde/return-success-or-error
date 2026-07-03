# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Estado do repositório

**Implementado — biblioteca completa e testada.** A solution (`net11.0`, C# 15 em _preview_) tem a lib, testes e samples funcionando; 50 testes verdes; zero dependências de runtime. O tipo central é um `union` **nativo** do C# 15 com o **erro parametrizado** (`ReturnSuccessOrError<TValue, TError>`), fechado por feature. **Publicação estável aguarda o GA do .NET 11 (nov/2026)** — o alvo é um framework preview e não deve ir ao NuGet como release estável até lá. Build/test exigem o SDK 11 (fixado em `global.json`).

Documentação de referência:

- `docs_dev/PRD.md` — define O QUE é a biblioteca: objetivos, tipos públicos (com assinaturas C# completas), padrões, fluxo de erros e decisões de design. **É a fonte de verdade da API.**
- `docs_dev/DEVELOPMENT_PLAN.md` — define COMO construir: estrutura da solution, `.csproj`, ordem de implementação, matriz de testes, samples, NuGet/CI/CD.
- `CHANGELOG.md` — histórico de versões (formato Keep a Changelog + SemVer).

Ao evoluir o código, **siga essas specs**; não reinvente nomes ou assinaturas. Se divergir da spec por um bom motivo, atualize o doc correspondente no mesmo passo.

> **Analisadores:** `AnalysisLevel=latest-all` + `TreatWarningsAsErrors` ativam todas as regras CA. O `.editorconfig` suprime — com justificativa e escopadas por glob (`src`/`tests`/`samples`) — apenas as regras que conflitam com decisões de design deliberadas do PRD (ex.: CA1815 igualdade por valor do union, CA2225 conversão implícita, CA1031 catch na fronteira). Antes de suprimir uma nova, verifique se é decisão de design já documentada.

Idioma: toda comunicação e documentação em **português (pt-br)**. Identificadores e código em inglês.

## O que está sendo construído

`ReturnSuccessOrError` — biblioteca NuGet (.NET 11 / C# 15) para a camada de domínio em Clean Architecture. **Nota:** o .NET 11 e o C# 15 ainda estão em _preview_ (STS); o SDK é fixado via `global.json` e o `LangVersion` é `preview` (o C# 15 não tem número próprio no SDK ainda). É inspirada no package Dart homônimo (`C:\PROJETOS\FLUTTER\PACKAGES\return_success_or_error`), mas é um produto **nativo .NET** — não um porte literal. Zero dependências de runtime; AOT-friendly.

## Comandos (após a estrutura existir — ver DEVELOPMENT_PLAN seção 2)

```bash
dotnet build -c Release                      # build (TreatWarningsAsErrors=true)
dotnet test                                  # todos os testes
dotnet test --filter "FullyQualifiedName~UsecaseBaseCallDataTests"   # uma classe
dotnet test --filter "Name=CallAsync_QuandoFetchFalha_CurtoCircuito_SemChamarProcess"  # um teste
dotnet test --collect:"XPlat Code Coverage"  # cobertura (meta > 90%)
dotnet pack src/ReturnSuccessOrError/ReturnSuccessOrError.csproj -c Release -o ./artifacts
dotnet run --project samples/ReturnSuccessOrError.Samples   # roda as 3 features de exemplo
```

Publicação é automática via tag `vX.Y.Z` (GitHub Actions, ver DEVELOPMENT_PLAN seção 6).

## Arquitetura — visão geral

A biblioteca gira em torno de uma **união discriminada selada** e duas **classes base de caso de uso** que orquestram o fluxo. O desenvolvedor que consome a lib escreve apenas a regra de negócio (`Process`).

**Tipo central — `ReturnSuccessOrError<TValue, TError>`:** `union` **nativo** do C# 15 — `public readonly union ReturnSuccessOrError<TValue, TError>(Success<TValue>, Failure<TError>)` — sobre dois `sealed record` **top-level**, `Success<TValue>(TValue Value)` e `Failure<TError>(TError Error)`. **O erro é parametrizado** (`TError`): cada feature fecha o seu conjunto de erros num `union` próprio, e o consumo é **exaustivo, sem `_`** (o compilador obriga a cobrir todos os erros que o `Repository`/`Process` podem produzir). A criação é por **conversão implícita** — de `TValue` (`return value;`) e de `TError` (`return error;`). **Pegadinha do duplo salto:** C# não encadeia duas conversões implícitas; retornar um **caso concreto** do union de erro a partir do `Process` exigiria dois saltos (caso → union de erro → `Failure`) e **não compila**. Forma **recomendada:** os helpers `Fail(error)`/`Ok(value)` da `UsecaseExecutorBase` (factory protegida — o `TError` já está fixado pela base, então não há cadeia de conversões): `return Fail(new SomeCase(...));`. Alternativa equivalente: o cast ao union — `return (FeatureError)new SomeCase(...);`. No `MapError`/`OnUnexpected` nada disso é preciso (o retorno já é o `union` de erro, um salto só). **Pegadinha do struct wrapper:** `GetType()` devolve o tipo do union; verifique o caso por pattern matching (`is Success<T>`/`is Failure<TError>`), via os helpers em `tests/ResultAssertions.cs`. Para ler campos de um caso a partir do union, use pattern matching (não cast direto `(Case)union`).

**Arquitetura em três camadas (`DataSource → Repository → UseCase`).** A `IDataSource<TData, TParams>` é **burra**: devolve dado bruto **ou lança** exceção técnica (sem `TError`). O `IRepository<TData, TParams, TError>`/`RepositoryBase` é a **fronteira** (anti-corruption layer): captura a exceção e a traduz num dos erros do union via `MapError` (**abstrato** — o repositório é obrigado a mapear toda exceção para um caso previsto), devolvendo `ReturnSuccessOrError<TData, TError>`. O usecase depende de `IRepository` (DIP) → é **portável**.

**Fluxo do `UsecaseBaseCallData<TValue, TData, TParams, TError>` (3 fases) — o conceito mais importante:**
1. **FETCH** — `await IRepository<TData, TParams, TError>.CallAsync(...)`. O repositório já devolve `Success|Failure` (a fronteira tratou a exceção via `MapError`); a base do usecase **não** tem try/catch de fetch.
2. **CURTO-CIRCUITO** — se o fetch falhou, retorna o erro imediatamente; `Process` **não** é chamado.
3. **PROCESS** — `Process(data, parameters, cancellationToken)` (CPU-bound). Roda direto ou, se `RunInBackground=true`, no thread pool via `Task.Run`. Uma exceção **inesperada** (bug) **nunca propaga**: em **ambos** os modos é convertida via `OnUnexpected(Exception)` (**abstrato**) num caso do `TError` da feature.

**Contrato de cancelamento (PRD §6.8): cancelamento NÃO é falha de domínio.** O token do chamador percorre `CallAsync → IRepository → IDataSource` **e o `Process`** (último parâmetro). Quando o token do chamador está cancelado, o `OperationCanceledException` **propaga** (não vira `Failure`): a fronteira (`RepositoryBase`) e o `ProcessStageAsync` fazem rethrow via filtro `when (cancellationToken.IsCancellationRequested)`, e o `ProcessStageAsync` checa o token **antes** do `Process` nos dois modos (paridade direto↔background). Um OCE *interno* (token do chamador não cancelado) é falha comum → `MapError`/`OnUnexpected`.

`UsecaseBase<TValue, TParams, TError>` é a variante sem fonte de dados — só fase 3. Parâmetros de tipo: `UsecaseBaseCallData` (4), `UsecaseBase`/`IRepository` (3), `IDataSource` (2).

**Camada de composição de features:** **nada de composição/DI é embarcado no core.** A Service Layer (um service por feature como Facade de Domínio) e o registro de DI **não** são tipos da biblioteca — são **metodologia documentada** (sugestão de implementação). O padrão recomendado é **idiomático do .NET**: um método de extensão por feature (`AddXxxFeature()` sobre `IServiceCollection`, no arquivo da feature) encadeado por um agregador fino (`FeatureRegistration.AddFeatures()` em `Composition/`) — sem interface `IFeatureModule`, sem `new XxxModule()`, sem reflexão (AOT-friendly). Não há marcador `IFeatureService` (removido — um marcador vazio não padroniza nada e contrariava a régua "composição não mora no core"). Ver PRD seções 5.9–5.10 e README seção 8.

**Decisões de design que atravessam vários arquivos:**
- **`Process` é método abstrato** (não delegate/typedef). Como `Task.Run` compartilha memória — diferente dos isolates do Dart original — não há restrição de "função estática". Boa prática: não acessar a fonte de dados dentro de `Process`.
- **`RunInBackground` afeta SOMENTE o processamento.** A busca de dados (I/O) jamais vai para o thread pool — despachar I/O assíncrono ao pool é desperdício.
- **Erro fechado por feature (`TError` = `union`).** Cada feature define o **conjunto fechado** dos erros que pode produzir num `union` próprio (ex.: `public readonly union LoginError(InvalidCredentials, AccountLocked, ErrorGeneric)`). É esse union que vai em `Failure<TError>` — dando consumo **exaustivo**. O `MapError` (Repository) e o `Process`/`OnUnexpected` (UseCase) só produzem casos desse union, então o tratamento final contempla todos. **Não há mais erro universal** que a base fabrique.
- **`AppError` é base opcional dos records de erro.** Herdar de `AppError` (`abstract record`) dá `Message`/`WithMessage`/igualdade por valor aos seus erros; os casos são então agrupados no `union`. `ErrorGeneric` é um caso pronto para o "inesperado" (alvo típico de `OnUnexpected`). Herdar de `AppError` é conveniência — `TError` pode ser qualquer tipo. **`ErrorCodes`/`ErrorTrace` foram removidos** (existiam para o catch automático que não existe mais).
- **`MapError` (Repository) e `OnUnexpected` (UseCase) são abstratos.** Como não há erro universal, o consumidor é obrigado a mapear toda exceção (técnica no `MapError`; inesperada/bug no `OnUnexpected`) para um caso do `TError`. Consequência: o `Process` **nunca propaga** exceção — direto e background convertem via `OnUnexpected`.
- **O erro saiu dos parâmetros.** `Parameters` é um **`abstract record`** que carrega **só dados** (antes era `ParametersReturnResult`). `NoParams` é um singleton (`NoParams.Value`).
- **Distinção dados × serviço:** contratos que só carregam dados (`AppError`, `Parameters`) são `abstract record`; contratos de serviço/comportamento (`IDataSource<T,P>`, `IRepository<T,P,E>`) seguem `interface`.
- **`Unit` (operação sem valor) e `Nil` (null semântico)** são singletons distintos — `void` não é argumento genérico válido em C#.
- **`Failure`**, não `Error`, para o subtipo de falha (evita colisão com `System` e alinha a `OneOf`/`ErrorOr`).
- **`CancellationToken`** percorre toda a cadeia async, inclusive o `Process` (adição idiomática .NET, ausente no original Dart). Cancelamento do chamador propaga como OCE — ver contrato acima.
- **Medição via hook virtual `OnExecutionTimeMeasured(TimeSpan)`** (padrão `Trace.WriteLine`, que sobrevive ao binário Release do pacote — `Debug.WriteLine` seria removido na compilação da lib). O consumidor sobrescreve para plugar `ILogger`/métricas.

## Convenções de implementação

- `Nullable` habilitado; `TreatWarningsAsErrors=true` no projeto principal (testes podem relaxar).
- Imutabilidade via `record` + `with`; flags de caso de uso (`RunInBackground`, `MonitorExecutionTime`) são propriedades `init`.
- Fonte de dados injetada via construtor (compatível com `Microsoft.Extensions.DependencyInjection`).
- Todo `await` nas classes base usa `.ConfigureAwait(false)` (código de biblioteca). Medição de tempo via `Stopwatch.GetTimestamp()`/`GetElapsedTime()` (sem alocação).
- Testes: xUnit v3 + NSubstitute (mock de `IDataSource<T,P>`/`IRepository<T,P,E>`) + Shouldly + coverlet. Há um `union` de erro compartilhado em `tests/TestErrors.cs` (`TestError`) com helper `Text()`. Cobrir obrigatoriamente: `RepositoryBase.MapError` (sucesso, exceção→caso traduzido, braço default), curto-circuito, preservação do caso concreto do erro, `OnUnexpected` (direto **e** background — nada propaga), exaustividade do `switch` no erro, paridade direto↔background, propagação do `CancellationToken`, o **contrato de cancelamento** (token do chamador cancelado → OCE propaga, direto e background, na fronteira e no `Process`; OCE interno sem token cancelado → `MapError`/`OnUnexpected`), as factories `Fail`/`Ok` e o hook `OnExecutionTimeMeasured` (chamado com `MonitorExecutionTime=true`; não chamado quando desligado). **Notas:** (1) tipos de fixture usados como argumento de tipo de mocks (ex.: `TestParams`, `TestError`) precisam ser `public` (Castle/NSubstitute); (2) namespaces de teste não devem ter um segmento `Parameters` (sombrearia o tipo `ReturnSuccessOrError.Parameters`); (3) ler campos de um caso a partir do union exige pattern matching, não cast.
- Sem reflexão em runtime (manter AOT-friendly). Core com **zero dependências de runtime**; `MinVer` (build-only) versiona pela tag git.
