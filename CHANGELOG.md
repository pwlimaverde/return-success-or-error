# Changelog

Todas as mudanças notáveis deste projeto são documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não lançado]

### Alterado

- **Tipo central migrado para `union` nativo do C# 15.** `ReturnSuccessOrError<TValue>` deixa de
  ser um `abstract record` com construtor privado e casos aninhados, passando a
  `public readonly union ReturnSuccessOrError<TValue>(Success<TValue>, Failure)` sobre os
  `sealed record` **top-level** `Success<TValue>` e `Failure`. O compilador prova a exaustividade
  (sem caso default). Adicionada conversão implícita de `TValue` (`return value;`).
- **Alvo `net11.0` + C# 15 (`LangVersion preview`).** Requer o SDK do .NET 11 (fixado em
  `global.json`). Publicação estável aguarda o GA do .NET 11 (nov/2026).

> Nota: como o `union` é um struct wrapper, `GetType()` devolve o tipo do union (não do caso);
> verifique o caso por pattern matching (`is Success<T>`/`is Failure`), não por `GetType()`.

## [1.0.0] - 2026-06-23

Primeira versão (em .NET 11 preview). Biblioteca de domínio para Clean Architecture,
com result type discriminado e bases de caso de uso. Zero dependências de runtime; AOT-friendly.

### Adicionado

- **`ReturnSuccessOrError<TValue>`** — união discriminada (`union` do C# 15) com os casos
  top-level `Success<TValue>` e `Failure`. Fábricas `Ok`/`Err`, conversão implícita de `TValue`,
  consumo exaustivo via `Match` (com retorno) e `Switch` (efeitos colaterais).
- **`AppError`** (`abstract record`) + **`ErrorGeneric`** — erro de domínio como valor imutável;
  `WithMessage` (implementado uma vez na base, via clone virtual do `record`) enriquece a mensagem
  **preservando o tipo concreto**. Subtipos herdam — não reimplementam.
- **`ErrorCodes`** — códigos de rastreio centralizados em constantes: `DataSourceCatch`
  (falha na busca de dados) e `BackgroundCatch` (exceção no processamento em background).
- **`ParametersReturnResult`** (`abstract record`) + **`NoParams`** — contrato de parâmetros que
  carrega o erro a ser usado em caso de falha (com erro default não-nulo em `NoParams`).
- **`IDataSource<TData>`** — contrato de fonte de dados (I/O-bound) com `CancellationToken`.
- **`UsecaseBase<TValue>`** — caso de uso de lógica pura (apenas processamento).
- **`UsecaseBaseCallData<TValue, TData>`** — caso de uso com fonte de dados, orquestrando o
  fluxo em três fases: **fetch → curto-circuito → process**.
- **`RunInBackground`** (`init`) — despacha **apenas o processamento** (CPU-bound) ao thread
  pool via `Task.Run`; a busca de dados (I/O) nunca vai para o pool.
- **`MonitorExecutionTime`** (`init`) — medição opt-in do tempo de execução, sem alocação
  (`Stopwatch.GetTimestamp`/`GetElapsedTime`), com log via `Debug.WriteLine`.
- **`IFeatureService`** — contrato marcador (zero dependência) para a Service Layer de uma feature.
- **`Unit`** e **`Nil`** — singletons distintos para "operação sem valor" e "null semântico".
- Propagação de `CancellationToken` por toda a cadeia assíncrona; `ConfigureAwait(false)` em
  todo `await` das classes base.
- Documentação XML completa, README, samples executáveis (CheckConnection, Fibonacci,
  SalesReport) e workflows de CI/CD (GitHub Actions).

### Notas de design

- Em **modo direto**, uma exceção lançada por `Process` **propaga** ao chamador; apenas o
  **modo background** a converte em `Failure` com `BackgroundCatch`. Exceções na fonte de
  dados são sempre convertidas em `Failure` com `DataSourceCatch`.
- O padrão de composição de features (`IFeatureModule`/`AddFeature`) **não** é embarcado no
  core — é metodologia documentada (PRD §5.10) que o consumidor implementa no container de DI
  dele, mantendo o core agnóstico de DI e com zero dependências de runtime.

[Não lançado]: https://github.com/pwlimaverde/return-success-or-error/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/pwlimaverde/return-success-or-error/releases/tag/v1.0.0
