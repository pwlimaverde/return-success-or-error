# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Estado do repositório

**Implementado — biblioteca completa e testada.** A solution (`net11.0`, C# 15 em _preview_) tem a lib, testes e samples funcionando; 38 testes verdes; zero dependências de runtime. O tipo central usa `union` **nativo** do C# 15. **Publicação estável aguarda o GA do .NET 11 (nov/2026)** — o alvo é um framework preview e não deve ir ao NuGet como release estável até lá. Build/test exigem o SDK 11 (fixado em `global.json`).

Documentação de referência:

- `docs_dev/PRD.md` — define O QUE é a biblioteca: objetivos, tipos públicos (com assinaturas C# completas), padrões, fluxo de erros e decisões de design. **É a fonte de verdade da API.**
- `docs_dev/DEVELOPMENT_PLAN.md` — define COMO construir: estrutura da solution, `.csproj`, ordem de implementação, matriz de testes, samples, NuGet/CI/CD.
- `CHANGELOG.md` — histórico de versões (formato Keep a Changelog + SemVer).

Ao evoluir o código, **siga essas specs**; não reinvente nomes ou assinaturas. Se divergir da spec por um bom motivo, atualize o doc correspondente no mesmo passo.

> **Analisadores:** `AnalysisLevel=latest-all` + `TreatWarningsAsErrors` ativam todas as regras CA. O `.editorconfig` suprime — com justificativa e escopadas por glob (`src`/`tests`/`samples`) — apenas as regras que conflitam com decisões de design deliberadas do PRD (ex.: CA1034 tipos aninhados, CA1000 fábricas estáticas, CA1031 catch na fronteira). Antes de suprimir uma nova, verifique se é decisão de design já documentada.

Idioma: toda comunicação e documentação em **português (pt-br)**. Identificadores e código em inglês.

## O que está sendo construído

`ReturnSuccessOrError` — biblioteca NuGet (.NET 11 / C# 15) para a camada de domínio em Clean Architecture. **Nota:** o .NET 11 e o C# 15 ainda estão em _preview_ (STS); o SDK é fixado via `global.json` e o `LangVersion` é `preview` (o C# 15 não tem número próprio no SDK ainda). É inspirada no package Dart homônimo (`C:\PROJETOS\FLUTTER\PACKAGES\return_success_or_error`), mas é um produto **nativo .NET** — não um porte literal. Zero dependências de runtime; AOT-friendly.

## Comandos (após a estrutura existir — ver DEVELOPMENT_PLAN seção 2)

```bash
dotnet build -c Release                      # build (TreatWarningsAsErrors=true)
dotnet test                                  # todos os testes
dotnet test --filter "FullyQualifiedName~UsecaseBaseCallDataTests"   # uma classe
dotnet test --filter "Name=CallAsync_QuandoFetchFalha_RetornaDataSourceCatch_ESemChamarProcess"  # um teste
dotnet test --collect:"XPlat Code Coverage"  # cobertura (meta > 90%)
dotnet pack src/ReturnSuccessOrError/ReturnSuccessOrError.csproj -c Release -o ./artifacts
dotnet run --project samples/ReturnSuccessOrError.Samples   # roda as 3 features de exemplo
```

Publicação é automática via tag `vX.Y.Z` (GitHub Actions, ver DEVELOPMENT_PLAN seção 6).

## Arquitetura — visão geral

A biblioteca gira em torno de uma **união discriminada selada** e duas **classes base de caso de uso** que orquestram o fluxo. O desenvolvedor que consome a lib escreve apenas a regra de negócio (`Process`).

**Tipo central — `ReturnSuccessOrError<TValue>`:** `union` **nativo** do C# 15 — `public readonly union ReturnSuccessOrError<TValue>(Success<TValue>, Failure)` — sobre dois `sealed record` **top-level**, `Success<TValue>(TValue Value)` e `Failure(AppError Error)`. O compilador prova a exaustividade: `Match`/`switch` dispensam caso default. Fábricas `.Ok(value)`/`.Err(error)` e conversão implícita de `TValue` (`return value;`). **Pegadinha:** o union é um struct wrapper — `GetType()` devolve o tipo do union, não do caso; testes verificam o caso por pattern matching (`is Success<T>`/`is Failure`), via os helpers em `tests/ResultAssertions.cs`, não `ShouldBeOfType`.

**Fluxo do `UsecaseBaseCallData<TValue, TData>` (3 fases) — o conceito mais importante:**
1. **FETCH** — `await IDataSource<TData>.CallAsync(...)` no contexto da chamada (I/O-bound; nunca vai para background). Exceção aqui é capturada e vira `Failure` com código `ErrorCodes.DataSourceCatch`.
2. **CURTO-CIRCUITO** — se o fetch falhou, retorna o erro imediatamente; `Process` **não** é chamado.
3. **PROCESS** — `Process(data, parameters)` (CPU-bound). Roda direto ou, se `RunInBackground=true`, despachado para o thread pool via `Task.Run`. Exceção aqui vira `Failure` com código `ErrorCodes.BackgroundCatch`.

`UsecaseBase<TValue>` é a variante sem fonte de dados — só fase 3.

**Camada de composição de features:** o único tipo embarcado é `IFeatureService` (marcador vazio, zero dependência, para a Service Layer que orquestra casos de uso). O padrão de Composition Root por feature (`IFeatureModule`/`AddFeature`) **não** é embarcado — é metodologia documentada que o consumidor implementa no container de DI dele, mantendo o core zero-dep e agnóstico de DI. Ver PRD seções 5.9–5.10 e README seção 7.

**Decisões de design que atravessam vários arquivos:**
- **`Process` é método abstrato** (não delegate/typedef). Como `Task.Run` compartilha memória — diferente dos isolates do Dart original — não há restrição de "função estática". Boa prática: não acessar a fonte de dados dentro de `Process`.
- **`RunInBackground` afeta SOMENTE o processamento.** A busca de dados (I/O) jamais vai para o thread pool — despachar I/O assíncrono ao pool é desperdício.
- **Erros são valores, não exceções.** `AppError` é um **`abstract record`** (não interface) — todo erro é, por contrato, um valor imutável. Exceções só existem na fronteira da fonte de dados e são convertidas em `Failure` imediatamente. `AppError.WithMessage(...)` enriquece a mensagem **preservando o tipo concreto** (`ApiError` continua `ApiError`): é implementado **uma única vez** na base via o clone virtual do `record` (`this with {}`), que despacha para o subtipo real. Quem precisar de um erro que não seja `record` usa exceção, não este contrato.
- **Parâmetros também são `record`.** `ParametersReturnResult` é um **`abstract record`** pela mesma razão — só carrega dados. O `Error` é decidido pelo chamador antes da execução; fetch e process o acessam via `parameters.Error`. Subtipos passam o erro à base via `: ParametersReturnResult(Error)`.
- **Distinção dados × serviço:** contratos que só carregam dados (`AppError`, `ParametersReturnResult`) são `abstract record`; contratos de serviço/comportamento (`IDataSource<T>`, `IFeatureService`) seguem `interface`.
- **`Unit` (operação sem valor) e `Nil` (null semântico)** são singletons distintos — `void` não é argumento genérico válido em C#.
- **`Failure`**, não `Error`, para o subtipo de falha (evita colisão com `System` e alinha a `OneOf`/`ErrorOr`).
- **`CancellationToken`** percorre toda a cadeia async (adição idiomática .NET, ausente no original Dart).

## Convenções de implementação

- `Nullable` habilitado; `TreatWarningsAsErrors=true` no projeto principal (testes podem relaxar).
- Imutabilidade via `record` + `with`; flags de caso de uso (`RunInBackground`, `MonitorExecutionTime`) são propriedades `init`.
- Fonte de dados injetada via construtor (compatível com `Microsoft.Extensions.DependencyInjection`).
- Todo `await` nas classes base usa `.ConfigureAwait(false)` (código de biblioteca). Medição de tempo via `Stopwatch.GetTimestamp()`/`GetElapsedTime()` (sem alocação).
- Testes: xUnit v3 + NSubstitute (mock de `IDataSource<T>`) + Shouldly + coverlet. Cobrir obrigatoriamente: curto-circuito, `ErrorCodes.DataSourceCatch`, `ErrorCodes.BackgroundCatch`, paridade direto↔background e propagação do `CancellationToken`.
- Sem reflexão em runtime (manter AOT-friendly). Core com **zero dependências de runtime**; `MinVer` (build-only) versiona pela tag git.
