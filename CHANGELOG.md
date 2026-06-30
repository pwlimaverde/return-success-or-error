# Changelog

Todas as mudanças notáveis deste projeto são documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [1.0.0] - Não lançado

Primeira versão (em .NET 11 / C# 15, ainda em _preview_). Biblioteca de domínio para Clean
Architecture, estruturada em **três camadas** (`DataSource → Repository → UseCase`), com result
type discriminado de **erro fechado por feature** e bases de caso de uso. Zero dependências de
runtime; AOT-friendly. A publicação estável aguarda o GA do .NET 11 (nov/2026).

### Adicionado

- **`ReturnSuccessOrError<TValue, TError>`** — união discriminada (`union` nativo do C# 15) sobre
  os casos top-level `Success<TValue>` e `Failure<TError>`. **O erro é parametrizado:** cada feature
  fecha seu conjunto de erros num `union` próprio (`TError`), e o consumo via `Match`/`switch` é
  **exaustivo sem braço `_`** — o compilador obriga a cobrir todos os erros que a feature pode
  produzir. Criação **por conversão implícita** (`return value;` → `Success`; `return error;` →
  `Failure`).
- **`AppError`** (`abstract record`) + **`ErrorGeneric`** — **base opcional** dos records de erro
  da feature: dá `Message`, igualdade por valor e `WithMessage` (que preserva o tipo concreto). Os
  casos são então agrupados num `union` usado como `TError`. `ErrorGeneric` é um caso pronto para o
  "inesperado". Herdar de `AppError` é conveniência — `TError` pode ser qualquer tipo.
- **`Parameters`** (`abstract record`, **só dados**) + **`NoParams`** (singleton `NoParams.Value`).
- **`IDataSource<TData, TParams>`** — contrato de fonte de dados **burra**: devolve dado bruto
  **ou lança** exceção técnica; sem conhecimento de domínio.
- **`IRepository<TData, TParams, TError>`** + **`RepositoryBase<TData, TParams, TError>`** — camada
  de **fronteira** (anti-corruption layer) que traduz a exceção técnica num dos erros do `union`
  via `MapError` (**abstrato**) e devolve `ReturnSuccessOrError<TData, TError>`.
- **`UsecaseBase<TValue, TParams, TError>`** — caso de uso de lógica pura (apenas processamento).
- **`UsecaseBaseCallData<TValue, TData, TParams, TError>`** — caso de uso que depende de
  **`IRepository`** (DIP → usecase **portável**), orquestrando **fetch → curto-circuito → process**.
- **`OnUnexpected(Exception)`** (`abstract` na base dos casos de uso) — converte uma exceção
  inesperada do `Process` (um bug) num caso do `TError`. O `Process` **nunca propaga** exceção:
  direto **e** background convertem via `OnUnexpected`.
- **`RunInBackground`** / **`MonitorExecutionTime`** (`init`) — despacho opcional do processamento
  ao thread pool e medição opt-in do tempo (sem alocação), respectivamente.
- **`Unit`** e **`Nil`** — singletons distintos para "operação sem valor" e "null semântico".
- Propagação de `CancellationToken` por toda a cadeia assíncrona; `ConfigureAwait(false)` em
  todo `await` das classes base.
- Documentação XML completa, README (com seções de portabilidade e de erro fechado), samples
  executáveis (CheckConnection, Fibonacci, SalesReport — este demonstrando **portabilidade**: o
  mesmo usecase com dois datasources) e workflows de CI/CD (GitHub Actions).

### Notas de design

- **Erro fechado por feature (`TError` = `union`).** O conjunto de erros que uma feature pode
  produzir é definido na construção dela (um `union`), e o tratamento final é **obrigado pelo
  compilador a cobrir todos** — sem `_`. `MapError` (Repository) e `Process`/`OnUnexpected`
  (UseCase) só produzem casos desse union. Não há mais um erro universal que a base fabrique.
- **`MapError` e `OnUnexpected` são abstratos.** O consumidor é obrigado a mapear toda exceção
  (técnica → `MapError`; inesperada/bug → `OnUnexpected`) para um caso do `TError`.
- **O `Process` nunca propaga exceção.** Direto e background convertem o inesperado via
  `OnUnexpected` — o resultado é sempre um dos casos previstos.
- **Arquitetura em três camadas (`DataSource → Repository → UseCase`).** O usecase depende de
  `IRepository` (abstração), tornando-o **portável** — troca-se o datasource sem tocar na regra.
- **Separação do erro dos parâmetros.** `Parameters` carrega só dados.
- **Duplo salto de conversão:** C# não encadeia duas conversões implícitas; um erro de negócio no
  `Process` precisa do cast ao union (`return (FeatureError)new Case(...);`).
- Como o `union` é um **struct wrapper**, `GetType()` devolve o tipo do union; verifique o caso por
  pattern matching (`is Success<T>`/`is Failure<TError>`), não por `GetType()`.
- **Composição e DI ficam totalmente fora do core.** Nenhum tipo de composição é embarcado — nem
  marcador de serviço, nem helpers de DI. O padrão recomendado é **idiomático do .NET**: um método
  de extensão por feature (`AddXxxFeature()`) encadeado por um agregador fino (`AddFeatures()`). A
  Service Layer (um service por feature) e esse registro são **sugestão de implementação** (PRD
  §5.9–5.10, README §8) que o consumidor escreve no container de DI dele, mantendo o core zero-dep
  e agnóstico de DI.

### Histórico interno (pré-1.0, não publicado)

- Evolução `ParametersReturnResult` (erro embutido nos parâmetros) → `Parameters` (só dados) +
  camada `Repository` (anti-corruption) → **erro parametrizado por feature** (`TError` = `union`,
  consumo exaustivo). Como a 1.0.0 não foi publicada, estas mudanças não quebram consumidores.

[1.0.0]: https://github.com/pwlimaverde/return-success-or-error/releases/tag/v1.0.0
