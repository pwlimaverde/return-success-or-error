# PRD — ReturnSuccessOrError (.NET)

**Produto:** Biblioteca NuGet `ReturnSuccessOrError`
**Plataforma-alvo:** .NET 11 / C# 15 — _preview_ (com suporte a `netstandard2.1` em avaliação)
**Tipo:** Biblioteca de domínio (class library), zero dependências de runtime
**Licença:** MIT
**Data:** 2026-06-22

---

## 1. Visão Geral

`ReturnSuccessOrError` é uma biblioteca C# que fornece abstrações para a camada de domínio (casos de uso, repositórios e fontes de dados) seguindo os princípios da Clean Architecture. Seu elemento central é um **tipo de resultado discriminado e selado** — `ReturnSuccessOrError<TValue, TError>` — que representa o desfecho de qualquer operação como **sucesso** ou **erro**. Com o **erro parametrizado** (`TError` é um `union` fechado por feature), o consumidor é obrigado pelo compilador a tratar **todos** os erros possíveis daquela feature, sem depender de exceções que atravessam camadas.

A biblioteca substitui o padrão de `try/catch` espalhado por um fluxo previsível e tipado, estruturado em **três camadas** (`DataSource → Repository → UseCase`): a fonte de dados (`IDataSource<TData, TParams>`) é **burra** — devolve dado bruto ou lança exceção técnica; o repositório (`IRepository<TData, TParams>`) é a **fronteira** (anti-corruption layer) que traduz a exceção em erro de domínio (`AppError`) via `MapError`; e o caso de uso busca o dado já tratado, faz curto-circuito automático em caso de falha, e processa o dado bruto numa função de negócio — opcionalmente em uma thread de background quando o processamento é intensivo em CPU.

Por depender de uma abstração (`IRepository`), o caso de uso é **portável**: leva-se um `LoginUsecase` de um projeto a outro trocando apenas o datasource, sem tocar na regra de negócio (DIP).

### 1.1 Problemas que a biblioteca resolve

| Problema recorrente em .NET | Solução da biblioteca |
|---|---|
| Exceções de I/O (banco, HTTP) vazam para camadas superiores e quebram o fluxo de forma imprevisível | A fronteira (`RepositoryBase.MapError`) captura a exceção técnica e a traduz em `AppError` tipado; o domínio nunca vê exceção de infraestrutura |
| `try/catch` repetido e inconsistente em cada handler/controller/service | A captura fica em um único lugar (o `Repository`); a base do caso de uso só orquestra curto-circuito e processamento |
| Casos de uso amarrados à infraestrutura concreta, difíceis de portar | O caso de uso depende de `IRepository` (abstração); troca-se o datasource sem tocar na regra de negócio (DIP) |
| Processamento CPU-bound (parsing, agregação) bloqueia a thread de requisição ou a UI | Separação fetch/process: o processamento pesado pode ser delegado a `Task.Run` (thread pool) via flag opt-in |
| Resultado de erro perde o tipo concreto ao subir pelas camadas | `AppError.WithMessage` preserva o tipo concreto do erro ao enriquecer a mensagem |
| Esquecer de tratar o caso de erro é fácil e silencioso | Tipo selado + método `Match` exaustivo tornam o tratamento de erro obrigatório |

---

## 2. Objetivos do Produto

### 2.1 Objetivos Primários

1. **Eliminar o vazamento de exceções entre camadas.** A exceção técnica é traduzida em erro de domínio na fronteira (`Repository`); casos de uso nunca propagam exceções não tratadas — devolvem um resultado tipado.
2. **Tornar o tratamento de erro obrigatório e visível.** O tipo selado com `Match`/`switch` força o consumidor a lidar com sucesso e erro, com verificação do compilador.
3. **Padronizar a camada de domínio em três camadas.** `DataSource → Repository → UseCase`: classes base (`RepositoryBase<TData, TParams>`, `UsecaseBase<TValue, TParams>` e `UsecaseBaseCallData<TValue, TData, TParams>`) definem o esqueleto de execução; o desenvolvedor implementa apenas `MapError` (tradução) e `Process` (regra).
4. **Tornar os casos de uso portáveis (DIP).** O caso de uso depende de `IRepository`, nunca da infraestrutura concreta — leva-se a regra de negócio a outro projeto trocando só o datasource.
5. **Separar I/O de processamento.** A busca de dados (assíncrona, I/O-bound) roda no contexto da chamada; o processamento (CPU-bound) pode ir para o thread pool sem bloquear a thread chamadora.

### 2.2 Objetivos Secundários

6. **Integração idiomática com o ecossistema .NET** — suporte a `CancellationToken`, compatível com injeção de dependência (`Microsoft.Extensions.DependencyInjection`) e ASP.NET Core.
7. **Tipagem forte ponta a ponta** — parâmetros, dado bruto da fonte, valor de sucesso e erro são todos genéricos e verificados em compilação.
8. **Imutabilidade por padrão** — erros e parâmetros são `record`s imutáveis; enriquecimento via expressão `with`.
9. **Zero dependências de runtime** — usa apenas a BCL (`System.*`); não acopla o consumidor a nenhum framework nem a um container de DI específico (a composição de features é convenção documentada, não tipo embarcado).
10. **Observabilidade opt-in** — medição de tempo de execução habilitável por instância, sem custo quando desligada.

### 2.3 Não-objetivos

- Não é um container de injeção de dependência.
- Não é uma biblioteca de gerenciamento de estado de UI.
- Não faz serialização, persistência ou cache.
- Não substitui `MediatR` / pipelines de mensagens — embora possa coexistir com eles.

---

## 3. Público-Alvo

Desenvolvedores .NET (back-end com ASP.NET Core, serviços, workers, ou apps MAUI/desktop) que adotam Clean Architecture / Ports & Adapters e querem:

- Um contrato explícito de sucesso/erro entre as camadas de aplicação e infraestrutura.
- Prevenir erros silenciosos por exceções não tratadas.
- Mover processamento pesado para background de forma controlada e testável.

---

## 4. Escopo

### 4.1 Dentro do Escopo

- Tipo de resultado discriminado selado (`ReturnSuccessOrError<TValue, TError>`) com subtipos `Success<TValue>` e `Failure<TError>`.
- Método `Match` e suporte a `switch` por padrão para consumo exaustivo.
- Base opcional de erro (`AppError`) e caso pronto (`ErrorGeneric`); o erro de cada feature é um `union` fechado (`TError`).
- Contrato de parâmetros **só-dados** (`Parameters`) e implementação concreta (`NoParams`).
- Contrato de fonte de dados **burra** (`IDataSource<TData, TParams>`).
- Camada de **repositório** (fronteira / anti-corruption): contrato (`IRepository<TData, TParams, TError>`) e base com tradução de erro (`RepositoryBase<TData, TParams, TError>` + `MapError` abstrato).
- Classes base de caso de uso: lógica pura e lógica com repositório.
- Orquestração fetch → curto-circuito → process.
- Execução opcional do processamento em thread pool (`Task.Run`).
- Tipos auxiliares `Unit` (ausência de valor) e `Nil` (null semântico).
- Medição opcional de tempo de execução.
- **Metodologia de composição de features** (Service Layer + estrutura de pastas + padrão Module/aggregator) entregue como **convenção documentada / sugestão de implementação** — nenhum tipo de composição é embarcado no core. Ver seções 5.9–5.10.

### 4.2 Fora do Escopo

- Geração de código / source generators (avaliado para versão futura).
- Conversões implícitas avançadas (avaliado, opt-in futuro).
- Integração com bibliotecas de validação (`FluentValidation`) — pode ser usada em conjunto, mas não é incluída.
- **Toda a camada de composição/DI** (Service Layer e `IFeatureModule`/`AddFeatures` baseados em `IServiceCollection`, e qualquer marcador de serviço): para manter o core com **zero dependências de runtime** e agnóstico de DI, a composição é tratada como **sugestão de implementação** (seções 5.9–5.10), não como tipo embarcado. Um pacote satélite opcional para Microsoft.Extensions.DI fica no roadmap.

---

## 5. Arquitetura e Tipos

### 5.1 Hierarquia de Tipos

```
ReturnSuccessOrError<TValue, TError>   (readonly union — C# 15; erro parametrizado)
├── Success<TValue>(TValue Value)      (sealed record top-level)
└── Failure<TError>(TError Error)      (sealed record top-level)

AppError                              (abstract record — base OPCIONAL dos records de erro)
└── ErrorGeneric                       (sealed record — caso pronto p/ "inesperado")

(por feature, no consumidor)  union FeatureError(CasoA, CasoB, ErrorGeneric)   ← usado como TError

Parameters                            (abstract record — só dados)
└── NoParams                           (sealed record — singleton)

IDataSource<TData, TParams>            (interface — fonte burra: dado bruto OU throw)

IRepository<TData, TParams, TError>            (interface — fronteira: ReturnSuccessOrError<TData, TError>)
└── RepositoryBase<TData, TParams, TError>     (abstract class — captura + MapError ABSTRATO)

UsecaseExecutorBase<TValue, TError>                               (abstract class — base comum: medição (hook virtual OnExecutionTimeMeasured) + background + OnUnexpected abstrato + factory Ok/Fail)
├── UsecaseBase<TValue, TParams, TError>                          (abstract class — só processamento)
└── UsecaseBaseCallData<TValue, TData, TParams, TError>           (abstract class — fetch via Repository + processamento)

(Service Layer / composição NÃO mora no core — é sugestão de implementação, seções 5.9–5.10)

Unit                                   (sealed class — singleton)
Nil                                    (sealed class — singleton)
```

### 5.2 `ReturnSuccessOrError<TValue, TError>` — Tipo Central

União discriminada implementada como **`union` nativo do C# 15** sobre dois `sealed record` **top-level**. O **erro é parametrizado** (`TError`): cada feature fecha o seu conjunto de erros possíveis num `union` próprio, usado como `TError`. Assim o `switch`/`Match` sobre o erro é **exaustivo, sem braço `_`** — o compilador obriga a cobrir todos os erros que o `Repository` e o `Process` daquela feature podem produzir.

```csharp
namespace ReturnSuccessOrError;

/// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
public sealed record Success<TValue>(TValue Value);

/// <summary>Resultado com falha, carregando um erro de tipo <typeparamref name="TError"/>.</summary>
public sealed record Failure<TError>(TError Error);

public readonly union ReturnSuccessOrError<TValue, TError>(Success<TValue>, Failure<TError>)
{
    // Criação por conversão implícita (ex.: return value;  /  return error;).
    public static implicit operator ReturnSuccessOrError<TValue, TError>(TValue value) => new Success<TValue>(value);
    public static implicit operator ReturnSuccessOrError<TValue, TError>(TError error) => new Failure<TError>(error);

    /// <summary>Consumo exaustivo: o compilador prova a exaustividade — sem caso default.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onError) => this switch
    {
        Success<TValue> success => onSuccess(success.Value),
        Failure<TError> failure => onError(failure.Error),
    };
}
```

**Padrões de consumo** (com um `union` de erro da feature, ex.: `union LoginError(InvalidCredentials, AccountLocked, ErrorGeneric)`):

```csharp
// Match: cobre sucesso e cada caso de erro — exaustivo, SEM _
string message = result.Match(
    onSuccess: value => $"OK: {value}",
    onError:   e => e switch
    {
        InvalidCredentials => "credenciais inválidas",
        AccountLocked      => "conta bloqueada",
        ErrorGeneric g     => $"inesperado: {g.Message}",
    });
```

> **Duplo salto de conversão (pegadinha):** C# não encadeia duas conversões implícitas. Um caso (ex.: `InvalidCredentials`) converte implicitamente para o `union` `LoginError`; e `LoginError` converte para o `ReturnSuccessOrError`. Mas `return new InvalidCredentials();` de um `Process` que retorna `ReturnSuccessOrError<…, LoginError>` exige **dois** saltos → não compila (`error CS0029`).
> **Forma recomendada:** os helpers `Fail(error)` / `Ok(value)` (factory `protected static` da `UsecaseExecutorBase`). Como o `TError` já está fixado pela base, passar o caso concreto é **uma única** conversão de union no argumento (contexto isolado) — sem cadeia: `return Fail(new InvalidCredentials());`. `Ok` é simétrico e opcional (`return value;` segue válido). **Alternativa equivalente:** o cast ao union — `return (LoginError)new InvalidCredentials();`. No `MapError`/`OnUnexpected` nada disso é preciso (o retorno já é o `union`, um salto só).
>
> **Igualdade por valor:** o `union` (struct) recebe `Equals`/`GetHashCode` por valor, e os casos `Success`/`Failure`, por serem `record`s, também.
>
> **Pegadinha (struct wrapper):** o `union` é um struct que encapsula o caso. `GetType()` devolve o tipo do **union**, não de `Success`/`Failure`; e `is Failure` sobre uma referência `object` (boxed) dá `false`. Verifique o caso por pattern matching com o tipo estático do union (`result is Failure f`), nunca por `GetType()`/`ShouldBeOfType`. Ver `tests/ResultAssertions.cs`.
>
> **Pegadinha (`TValue` == `TError`):** se os dois argumentos de tipo forem o **mesmo tipo** (ex.: `ReturnSuccessOrError<string, string>`), as duas conversões implícitas ficam com a mesma assinatura e a criação por conversão torna-se **ambígua** (não compila). Na prática não ocorre — `TError` é um `union` de feature, nunca igual ao valor — mas, se precisar, crie os casos explicitamente (`new Success<T>(...)`/`new Failure<T>(...)` ou `Ok`/`Fail`).

### 5.3 `AppError` — Base Opcional dos Erros + `union` por Feature

O erro de cada feature é um **conjunto fechado** (um `union`). `AppError` é uma **base opcional** dos records de erro: herdar dela dá `Message`, igualdade por valor e `WithMessage` (que preserva o tipo concreto). `WithMessage` é implementado **uma única vez** na base via o clone virtual sintetizado do `record`. Herdar de `AppError` é conveniência — `TError` pode ser qualquer tipo.

```csharp
namespace ReturnSuccessOrError;

public abstract record AppError(string Message)
{
    /// <summary>
    /// Devolve uma nova instância com a mensagem substituída, preservando o tipo
    /// concreto e os demais campos. Implementado uma vez aqui via clone virtual do record.
    /// </summary>
    public AppError WithMessage(string message) => this with { Message = message };
}
```

**Implementação concreta `ErrorGeneric`:**

```csharp
namespace ReturnSuccessOrError;

public sealed record ErrorGeneric(string Message) : AppError(Message)
{
    public override string ToString() => $"{nameof(ErrorGeneric)} - {Message}";
}
```

**Erro fechado por feature (o `union` é `TError`).** Cada feature declara, na sua construção, o conjunto de erros que pode produzir, agrupando os casos num `union`. É esse `union` que vai em `Failure<TError>`, dando consumo **exaustivo**.

```csharp
// Os erros possíveis da feature (records — herdam de AppError por conveniência):
public sealed record InvalidCredentials(string Message) : AppError(Message);
public sealed record AccountLocked(string Message) : AppError(Message);

// O conjunto FECHADO da feature (ErrorGeneric, da lib, como caso "inesperado"):
public readonly union LoginError(InvalidCredentials, AccountLocked, ErrorGeneric);

// Consumo exaustivo — o compilador OBRIGA a cobrir todos; sem _:
var texto = result.Match(
    onSuccess: v => $"OK: {v}",
    onError: e => e switch
    {
        InvalidCredentials => "credenciais inválidas",
        AccountLocked      => "conta bloqueada",
        ErrorGeneric g     => $"inesperado: {g.Message}",
    });
```

> **Por que `AppError` é só base opcional?** Os casos do `union` precisam de uma forma comum de carregar `Message` e enriquecê-la (`WithMessage`); herdar de `AppError` resolve isso sem boilerplate. Mas a discriminação no consumo é feita pelo `union` (exaustivo), não pela hierarquia de `AppError`. `TError` poderia ser qualquer tipo.

> **`ErrorCodes`/`ErrorTrace` foram removidos.** Existiam para anexar códigos (`DataSourceCatch`/`BackgroundCatch`) no catch automático da base. No modelo de erro fechado não há catch que fabrique um erro genérico: a tradução é feita pelo `MapError` (exceção técnica) e pelo `OnUnexpected` (bug), ambos retornando um caso do `TError` escolhido pelo consumidor.

### 5.4 `Parameters` — Contrato de Parâmetros (só dados)

Como `AppError`, `Parameters` é um **`abstract record`**: parâmetros são valores imutáveis que **apenas carregam dados**. Diferente da versão anterior (`ParametersReturnResult`, que carregava o `AppError`), o erro foi **separado dos parâmetros** — o tratamento de falha é decidido por camada (o `Repository` traduz exceções via `MapError`; o `Process` devolve erros de negócio). O mesmo objeto de parâmetros atravessa as três camadas.

```csharp
namespace ReturnSuccessOrError;

public abstract record Parameters;
```

**Implementação concreta `NoParams`** (singleton, para casos de uso sem entrada):

```csharp
namespace ReturnSuccessOrError;

public sealed record NoParams : Parameters
{
    private NoParams() { }
    public static NoParams Value { get; } = new();
}
```

**Parâmetro customizado (exemplo de uso pelo consumidor):**

```csharp
// Só dados — nenhum AppError embutido.
public sealed record FibonacciParameters(int N) : Parameters;
```

### 5.5 `IDataSource<TData, TParams>` — Fonte de Dados (camada burra)

```csharp
namespace ReturnSuccessOrError;

public interface IDataSource<TData, TParams>
    where TParams : Parameters
{
    /// <summary>
    /// Executa a chamada externa e devolve o dado bruto, ou LANÇA uma exceção técnica
    /// em caso de falha. A tradução num erro de domínio é feita pelo Repository.
    /// </summary>
    Task<TData> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default);
}
```

> **Convenção:** a fonte de dados é **burra** — não tem conhecimento de domínio. Em caso de falha, lança uma exceção técnica; quem a captura e traduz é o `Repository` (§5.5.1). O `CancellationToken` é idiomático em .NET e propagado em todo o fluxo.

### 5.5.1 `IRepository<TData, TParams, TError>` / `RepositoryBase` — Fronteira (Anti-Corruption Layer)

O repositório é a **fronteira** entre a infraestrutura burra e o domínio. Diferente do `IDataSource`, ele **não lança falha de infraestrutura**: devolve sempre um `ReturnSuccessOrError<TData, TError>` — o dado bruto como `Success` ou a exceção já traduzida num dos erros do `union` da feature como `Failure`. `MapError` é **abstrato**: o repositório é obrigado a mapear toda exceção para um caso previsto. **Única exceção do contrato:** o **cancelamento do chamador** (um `OperationCanceledException` com o token do chamador cancelado) **propaga como OCE** em vez de virar `Failure` — cancelamento não é falha de domínio (§6.8). Um OCE *interno* da fonte (sem o token do chamador cancelado) é falha técnica normal e segue para o `MapError`.

```csharp
namespace ReturnSuccessOrError;

public interface IRepository<TData, TParams, TError>
    where TParams : Parameters
{
    Task<ReturnSuccessOrError<TData, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default);
}

public abstract class RepositoryBase<TData, TParams, TError> : IRepository<TData, TParams, TError>
    where TParams : Parameters
{
    private readonly IDataSource<TData, TParams> _dataSource;

    protected RepositoryBase(IDataSource<TData, TParams> dataSource) =>
        _dataSource = dataSource;

    public async Task<ReturnSuccessOrError<TData, TError>> CallAsync(
        TParams parameters, CancellationToken cancellationToken = default)
    {
        try   { return await _dataSource.CallAsync(parameters, cancellationToken).ConfigureAwait(false); } // TData -> Success
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cancelamento do CHAMADOR propaga como OCE — não é falha de domínio (§6.8)
        }
        catch (Exception exception) { return MapError(exception, parameters); }                            // TError -> Failure
    }

    /// <summary>Traduz uma exceção da fonte num caso do union TError. ABSTRATO: obrigatório.</summary>
    protected abstract TError MapError(Exception exception, TParams parameters);
}
```

**Repository de uma feature (o consumidor só implementa `MapError`):**

```csharp
public sealed class ProdutosRepository(IDataSource<IReadOnlyList<Produto>, ProdutosParams> ds)
    : RepositoryBase<IReadOnlyList<Produto>, ProdutosParams, ProdutosError>(ds)
{
    // ProdutosError = union(ApiUnavailable, Timeout, ErrorGeneric)
    protected override ProdutosError MapError(Exception ex, ProdutosParams p) => ex switch
    {
        HttpRequestException => new ApiUnavailable("API indisponível", 503),
        TimeoutException     => new Timeout("tempo esgotado"),
        _                    => new ErrorGeneric($"inesperado: {ex.Message}"), // braço _ → caso "inesperado"
    };
}
```

> **`MapError` é `abstract`** (não virtual): como não há erro universal, o repositório é obrigado a traduzir toda exceção num caso do `TError`. O `switch` interno costuma ter um braço `_` que cai num caso "inesperado" (ex.: `ErrorGeneric`).

> **Base comum `UsecaseExecutorBase<TValue, TError>`.** A medição (`MeasuredAsync` + hook **virtual** `OnExecutionTimeMeasured(TimeSpan)`, §9.10), o despacho ao thread pool (`ProcessStageAsync`), o `OnUnexpected(Exception)` **abstrato** e as factories `Fail(error)`/`Ok(value)` (`protected static`) são compartilhados pelos dois casos de uso. Em **ambos** os modos (direto e background), uma exceção inesperada no `Process` é convertida via `OnUnexpected` num caso do `TError` — o `Process` **nunca propaga** exceção de bug. **Única exceção do contrato:** o **cancelamento do chamador** propaga como `OperationCanceledException` nos dois modos (§6.8) — o `ProcessStageAsync` checa o token antes do `Process` (paridade direto↔background) e faz rethrow de OCE cooperativo quando o token do chamador está cancelado. `Fail`/`Ok` são o caminho recomendado para criar o resultado no `Process` sem o cast do duplo salto (§5.2).

### 5.6 `UsecaseBase<TValue, TParams, TError>` — Caso de Uso de Lógica Pura

Para regras de negócio que não dependem de fonte de dados externa.

```csharp
namespace ReturnSuccessOrError;

public abstract class UsecaseBase<TValue, TParams, TError> : UsecaseExecutorBase<TValue, TError>
    where TParams : Parameters
{
    /// <summary>Regra de negócio implementada pela subclasse. Recebe o token do chamador
    /// para cancelamento cooperativo em processamento longo (ignorá-lo é válido).</summary>
    protected abstract ReturnSuccessOrError<TValue, TError> Process(
        TParams parameters,
        CancellationToken cancellationToken);

    public Task<ReturnSuccessOrError<TValue, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => ProcessStageAsync(() => Process(parameters, cancellationToken), cancellationToken));
}
```

### 5.7 `UsecaseBaseCallData<TValue, TData, TParams, TError>` — Caso de Uso com Repositório

Para regras de negócio que buscam dados externos e os processam. **Depende de `IRepository`** (não de `IDataSource`) — é o que torna o usecase portável.

```csharp
namespace ReturnSuccessOrError;

public abstract class UsecaseBaseCallData<TValue, TData, TParams, TError> : UsecaseExecutorBase<TValue, TError>
    where TParams : Parameters
{
    private readonly IRepository<TData, TParams, TError> _repository;

    protected UsecaseBaseCallData(IRepository<TData, TParams, TError> repository) =>
        _repository = repository;

    /// <summary>Regra de negócio: recebe o dado bruto já carregado, os parâmetros e o token
    /// do chamador para cancelamento cooperativo em processamento longo (ignorá-lo é válido).</summary>
    protected abstract ReturnSuccessOrError<TValue, TError> Process(
        TData data,
        TParams parameters,
        CancellationToken cancellationToken);

    public Task<ReturnSuccessOrError<TValue, TError>> CallAsync(
        TParams parameters,
        CancellationToken cancellationToken = default) =>
        MeasuredAsync(() => RunAsync(parameters, cancellationToken));

    private async Task<ReturnSuccessOrError<TValue, TError>> RunAsync(
        TParams parameters, CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH: o repositório já devolve Success|Failure (a fronteira tratou a exceção).
        var fetchResult = await _repository.CallAsync(parameters, cancellationToken).ConfigureAwait(false);

        // FASE 2 — CURTO-CIRCUITO + FASE 3 — PROCESS (delegado à base: direto ou background).
        return fetchResult switch
        {
            Failure<TError> failure => failure,   // Failure<TError> flui entre genéricos (depende só de TError)
            Success<TData> success =>
                await ProcessStageAsync(() => Process(success.Value, parameters, cancellationToken), cancellationToken)
                    .ConfigureAwait(false),
        };
    }
}
```

> **Sem try/catch de fetch na base do usecase.** A captura de exceção da fonte mora no `Repository` (§5.5.1). A base apenas consome o `Success|Failure` já tratado e faz o curto-circuito. Uma exceção inesperada no `Process` é convertida pela base via `OnUnexpected` (§5.5.1) — nada propaga.

### 5.8 `Unit` e `Nil`

```csharp
public sealed class Unit
{
    public static readonly Unit Value = new();
    private Unit() { }
    public override string ToString() => "Unit - void";
}

public sealed class Nil
{
    public static readonly Nil Value = new();
    private Nil() { }
    public override string ToString() => "Nil - null";
}
```

| Tipo | Semântica | Uso |
|---|---|---|
| `Unit` | Operação sem valor de retorno (efeito colateral bem-sucedido) | `ReturnSuccessOrError<Unit, TError>` |
| `Nil` | `null` como resultado válido e esperado | `ReturnSuccessOrError<Nil, TError>` |

### 5.9 Service Layer (sugestão de implementação — não embarcada)

A camada de serviços de uma feature (Service Layer) é a "Facade de Domínio" que expõe os casos de uso públicos da feature, orquestrando-os de forma centralizada e ocultando os detalhes de implementação das camadas superiores (Controllers, Handlers). **A biblioteca não embarca tipo algum para isso** — nem mesmo um marcador. Recomenda-se **um service por feature** (não um service-deus único), cada um expondo uma interface própria no projeto do consumidor:

```csharp
// Definido NO PROJETO DO CONSUMIDOR (não vem da biblioteca).
public interface IPerfilService
{
    Task<ReturnSuccessOrError<Perfil, PerfilError>> ObterAsync(string id, CancellationToken ct = default);
}

public sealed class PerfilService(ObterPerfilUseCase usecase) : IPerfilService
{
    public Task<ReturnSuccessOrError<Perfil, PerfilError>> ObterAsync(string id, CancellationToken ct = default)
        => usecase.CallAsync(new PerfilParameters(id), ct);
}
```

> **Sem marcador.** Uma versão anterior embarcava `IFeatureService` (interface vazia). Foi **removida**: um marcador sem membros não padroniza nada mecanicamente e contrariava a régua "composição/DI não mora no core". Quem quiser um selo semântico define `interface IMinhaAppFeatureService {}` no próprio app — em uma linha. Fluxos cross-feature (ex.: checkout) usam uma **feature orquestradora** cujo service compõe os services de outras features (composição, não centralização).

### 5.10 Metodologia de composição de features (convenção, não tipo embarcado)

> **Decisão de design.** O registro de dependências é responsabilidade da **camada de composição** (`Program.cs`/Main), não do domínio. Para manter o core com **zero dependências de runtime** e **agnóstico de container de DI**, a biblioteca **não embarca** uma interface de módulo nem extensões acopladas a `IServiceCollection`. Em vez disso, descreve um **padrão recomendado** que o consumidor implementa no seu próprio projeto, com o container que ele já usa (Microsoft.Extensions.DI, Autofac, Lamar, Pure DI etc.).

O padrão **idiomático do .NET**: cada feature expõe um **método de extensão** sobre `IServiceCollection` (`AddXxxFeature()`) que registra seus `DataSources`, `Repositories`, `UseCases` e o `Service` — exatamente como o ecossistema faz (`AddControllers`, `AddDbContext`, `AddHttpClient`). Um **agregador fino** encadeia as extensões num ponto único de DI. Tudo definido **no app do consumidor**:

```csharp
// Definido NO PROJETO DO CONSUMIDOR (não vem da biblioteca).
// O consumidor escolhe a abstração de DI que usa — aqui, a do .NET.
using Microsoft.Extensions.DependencyInjection;

// 1) "Módulo" idiomático: um método de extensão POR FEATURE (no arquivo da feature).
public static class PerfilServiceCollectionExtensions
{
    public static IServiceCollection AddPerfilFeature(this IServiceCollection services)
    {
        services.AddHttpClient<IPerfilDataSource, PerfilHttpDataSource>(/* baseUrl */);
        services.AddScoped<IRepository<PerfilDto, PerfilParameters, PerfilError>, PerfilRepository>();
        services.AddScoped<ObterPerfilUseCase>();
        services.AddScoped<IPerfilService, PerfilService>();
        return services;
    }
}

// 2) Agregador fino: ponto ÚNICO de DI (o "controle geral" das features).
public static class FeatureRegistration
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
        => services
            .AddPerfilFeature()
            .AddComissaoFeature();
            // .AddAuthFeature();   ← adicionar feature = 1 linha
}
```

No `Program.cs` fica **uma linha**: `builder.Services.AddFeatures();`. Sem interface custom, sem `new XxxModule()`, **sem reflexão** (preserva a meta AOT-friendly). Quem faz **Pure DI** (composição manual) ou usa outro container simplesmente registra os tipos no estilo nativo desse container — o método de extensão é só a convenção idiomática. **Nenhum tipo dessa camada vem da biblioteca.**

> **Por que método de extensão e não uma interface `IFeatureModule`?** A interface de módulo (estilo Autofac) é válida e igualmente SOLID, mas **menos idiomática** em `Microsoft.Extensions.DependencyInjection`: exige instanciar módulos e introduz uma abstração que o container já resolve com `AddXxx()`. O método de extensão é o padrão que a comunidade .NET adota e que aparece no IntelliSense de `services.`.

> **Roadmap.** Se houver demanda, um **pacote satélite opcional** `ReturnSuccessOrError.DependencyInjection` poderá fornecer helpers de composição prontos para `Microsoft.Extensions.DependencyInjection` — isolando a dependência fora do core, que permanece zero-dep.

---

## 6. Padrões de Design (idiomas C#)

### 6.1 União Discriminada via `union` nativo (C# 15)
`readonly union` sobre dois `sealed record` top-level (`Success<TValue>`, `Failure`). A exaustividade do `switch`/`Match` é provada pelo compilador (sem caso default), com igualdade por valor e desconstrução posicional gratuitas. É a forma nativa de discriminated unions a partir do C# 15.

### 6.2 Railway-Oriented Programming
O fluxo fetch → process é um "trilho": se a busca falha, o processamento é pulado e o erro segue direto até o chamador. Curto-circuito implementado com `is Failure`.

### 6.3 Template Method
`CallAsync` define o algoritmo (medição, fetch, curto-circuito, despacho para background); a subclasse fornece apenas `Process`.

### 6.4 `Process` como método abstrato (não delegate)
Diferente de abordagens baseadas em delegate/typedef, `Process` é um **método abstrato** sobrescrito pela subclasse — o idioma natural de polimorfismo em C#. Reduz boilerplate e é diretamente testável.

### 6.5 Tradução de erro na fronteira (`MapError`)
O `RepositoryBase.MapError` é o ponto único de tradução de exceção técnica → erro de domínio. É **abstrato**: o repositório é obrigado a mapear toda exceção para um caso do `union` `TError` da feature (com um braço `_` para o caso "inesperado").

### 6.6 Imutabilidade com `record` + `with`
Erros e parâmetros são imutáveis. O enriquecimento de mensagem usa `with { Message = ... }`, preservando o tipo concreto — substitui o `copyWith` manual de outras linguagens.

### 6.7 Injeção de Dependência via Construtor
A fonte de dados é injetada no construtor, compatível com `Microsoft.Extensions.DependencyInjection`. Facilita substituição por fakes/substitutes em teste.

### 6.8 Async/await + `CancellationToken` (contrato de cancelamento)
Toda operação é assíncrona e cooperativamente cancelável, alinhada às convenções da BCL e do ASP.NET Core. O token do chamador percorre **toda** a cadeia: `CallAsync` → `IRepository` → `IDataSource` e também o **`Process`** (último parâmetro) — processamento CPU-bound longo pode cooperar via `ThrowIfCancellationRequested()`.

**Cancelamento não é falha de domínio — é a "terceira via" idiomática do .NET:**
- Um `OperationCanceledException` causado pelo **token do chamador** cancelado **propaga como exceção** (não vira `Failure`): a fronteira (`RepositoryBase`) e a base do usecase (`ProcessStageAsync`) fazem rethrow via filtro `when (cancellationToken.IsCancellationRequested)`.
- O `ProcessStageAsync` checa o token **antes** do `Process`, nos **dois** modos (direto e background) — paridade de comportamento sob cancelamento.
- Um OCE **interno** (lançado sem o token do chamador cancelado) é tratado como falha comum: `MapError` na fronteira, `OnUnexpected` no `Process`.

Assim, os consumidores tratam o resultado (`Success|Failure`) exaustivamente e deixam o cancelamento fluir para quem cancelou — exatamente como o ASP.NET Core espera num request abortado.

---

## 7. Fluxo de Erros (todos caem num caso do `union` `TError`)

| Origem | Quem traduz | Como ocorre |
|---|---|---|
| Erro de **negócio** deliberado | o próprio `Process` | `return Fail(new AlgumCaso(...))` (recomendado; ou o cast ao union — ver duplo salto §5.2) |
| Falha de **I/O** (URL fora, timeout) | `RepositoryBase.MapError` (abstrato) | `IDataSource.CallAsync` lança exceção técnica; `MapError` a mapeia num caso do `union` |
| Exceção **inesperada** (bug) no `Process` | `OnUnexpected(Exception)` (abstrato) | em direto **ou** background, a base captura e mapeia num caso do `union` — nada propaga |

Em todos os casos, o resultado é **um dos casos do `union` da feature** — por isso o consumo final é exaustivo e contempla todas as origens. **Fora da tabela, por não ser falha:** o **cancelamento do chamador** propaga como `OperationCanceledException` (§6.8) — não vira caso do `union`.

---

## 8. Ciclo de Vida de uma Operação

```
Chamador
  │  await usecase.CallAsync(parameters, ct)
  ▼
[UsecaseBase]                          [UsecaseBaseCallData]
  Process(parameters, ct)                FASE 1: await repository.CallAsync(...)
   └─ direto ou Task.Run                   │  [RepositoryBase] try dataSource.CallAsync(...)
   └─ exceção inesperada → OnUnexpected     │   ├─ sucesso → Success<TData>(dado bruto)
   └─ ct cancelado → OCE propaga            │   ├─ exceção técnica → MapError → Failure<TError>
                                          │   └─ ct do chamador cancelado → OCE propaga
                                          FASE 2: is Failure? → propaga Failure<TError>
                                          FASE 3: Process(dado, parameters, ct)
                                            └─ direto ou Task.Run
                                            └─ exceção inesperada → OnUnexpected (caso do TError)
                                            └─ ct cancelado → OCE propaga (paridade nos 2 modos)
  │
  ▼
ReturnSuccessOrError<TValue, TError>
  │
  ▼
result.Match(onSuccess: ..., onError: e => e switch { ... })   // exaustivo, sem _
```

---

## 9. Peculiaridades e Decisões de Design (específicas de C#)

### 9.1 Por que `union` (e não `abstract record` com construtor privado)?
O `union` do C# 15 fecha a hierarquia **na linguagem**: o compilador prova que só existem `Success<TValue>` e `Failure`, garantindo a exaustividade do `switch`/`Match` sem caso default. Antes do C# 15, a forma canônica era um `abstract record` com construtor privado e casos aninhados (emulando a selagem); o `union` torna isso nativo, com os casos **top-level**. **Atenção:** o `union` é um struct wrapper — `GetType()` devolve o tipo do union, não do caso; verifique o caso por pattern matching (`is Failure`), nunca por `GetType()` (ver §5.2 e `tests/ResultAssertions.cs`).

### 9.2 Por que `Task.Run` em vez de algo "mais isolado"?
Processamento CPU-bound bloqueia a thread chamadora (de requisição no ASP.NET, ou de UI no MAUI). `Task.Run` despacha para o thread pool. Diferente de modelos de memória isolada, threads .NET compartilham memória — então **não há restrição de serialização** e o `Process` pode ser um método de instância normal. A busca de dados (I/O-bound) **não** vai para o thread pool: I/O assíncrono já libera a thread sem precisar de `Task.Run`, e despachar I/O para o pool é desperdício.

### 9.3 Por que `Process` é método abstrato e não um delegate?
Em C#, polimorfismo por método sobrescrito é o caminho idiomático e gera o melhor código. Não há a restrição (presente em modelos de isolamento de memória) de que a função precise ser estática para evitar capturar estado não-serializável. Mantemos a recomendação de **não acessar a fonte de dados dentro de `Process`** como boa prática de separação de responsabilidades, mas não é imposto pelo runtime.

### 9.4 Por que `Failure` e não `Error` como nome do subtipo?
`Error` colide semanticamente com `System.Error`/erros graves de runtime. `Failure` é mais claro e alinhado a bibliotecas funcionais consolidadas no ecossistema (`OneOf`, `ErrorOr`, `LanguageExt`).

### 9.5 Por que `AppError` e não usar `Exception` diretamente?
Exceções são caras (captura de stack trace) e semanticamente representam fluxo excepcional. Aqui o erro é um **valor de domínio esperado** — modelá-lo como `record` imutável é mais barato e expressa melhor a intenção. A biblioteca só usa exceções na fronteira da fonte de dados, onde elas inevitavelmente ocorrem, convertendo-as imediatamente em valores.

### 9.6 Por que `WithMessage` na interface, se `record` já tem `with`?
`with` exige conhecer o tipo concreto. Quando se tem apenas uma referência `AppError`, `with` não está disponível. `WithMessage` é o ponto polimórfico que permite enriquecer a mensagem sem conhecer o tipo concreto, mantendo-o preservado.

### 9.7 Por que o erro **saiu** de `Parameters` (separação do erro)?
Na versão anterior, `ParametersReturnResult` carregava o `AppError` a usar em caso de falha — prática incomum e anti-idiomática frente a `ErrorOr`/`FluentResults`/`MediatR`. Agora `Parameters` carrega **só dados**, e o erro é decidido **por camada**: a fonte de dados lança exceção técnica; o `Repository.MapError` a traduz no tipo de domínio adequado (a fronteira é quem conhece o significado da falha de I/O); e o `Process` devolve erros de **negócio** diretamente. Isso elimina estados ilegais (parâmetro com erro "pré-carregado" que pode nunca ser usado) e coloca a decisão do erro onde há contexto para tomá-la.

### 9.7.1 Por que uma camada `Repository` entre `DataSource` e `UseCase`?
Padrão canônico de Clean Architecture (Anti-Corruption Layer): a camada de dados retorna `Result<T>`, nunca expõe a infra ao domínio, e data sources não são acessados diretamente por casos de uso. Mantém o `DataSource` burro (testável como I/O puro), concentra a tradução de erro num único ponto (`MapError`) e, sobretudo, torna o usecase **portável** — ele depende de `IRepository`, então trocar o datasource (HTTP→cache, real→fake) não toca na regra de negócio.

### 9.7.2 Por que o erro é **parametrizado** (`TError` = `union` fechado por feature)?
Para que o tratamento final seja **obrigado pelo compilador a contemplar todos os erros** que a feature pode produzir. Se `Failure` carregasse um `AppError` aberto, o `switch` no consumo precisaria de um braço `_` — e seria fácil esquecer um caso. Fechando o conjunto num `union` por feature, o `MapError` (Repository) e o `Process`/`OnUnexpected` (UseCase) só produzem casos previstos, e o `switch`/`Match` é exaustivo. **Custo aceito:** +1 parâmetro de tipo na cadeia (`UsecaseBaseCallData` tem 4); o duplo salto de conversão no `Process` (mitigado pela factory `Fail`/`Ok`, ou contornável com o cast ao union); e a base não fabrica mais erro genérico — por isso `MapError`/`OnUnexpected` são abstratos.

### 9.7.3 Por que `OnUnexpected` (e o `Process` nunca propaga)?
No modelo de erro fechado não existe um erro universal para a base usar quando o `Process` lança uma exceção inesperada (um bug). Em vez de propagar (deixar escapar como `throw`) ou inventar um erro, a base delega ao `OnUnexpected(Exception)` **abstrato**: o consumidor mapeia o inesperado para um caso do seu `union` (tipicamente um `ErrorGeneric`/`Unexpected`). Vale para os **dois** modos (direto e background) — o resultado é sempre um dos casos previstos, e o sistema fica robusto (nada escapa sem ser um valor de erro tratável). A única exceção que atravessa é o **cancelamento do chamador** (`OperationCanceledException` com o token cancelado) — cancelamento não é erro a tratar, ver §6.8.

### 9.8 Por que `RunInBackground` é `init` e não parâmetro de método?
É uma característica da configuração do caso de uso (decidida na composição/DI), não da chamada individual. `init` permite definir na inicialização do objeto e mantém imutável depois.

### 9.9 Por que `Unit` e `Nil` separados?
`void` não é um tipo de primeira classe em C# (não pode ser argumento genérico). `Unit` resolve isso para operações sem valor. `Nil` distingue "null é o resultado correto e esperado" de "ausência de valor por erro", evitando ambiguidade com `null`.

### 9.10 Por que um hook virtual (`OnExecutionTimeMeasured`) para o monitoramento?
A primeira versão usava `Debug.WriteLine`, mas `[Conditional("DEBUG")]` é avaliado na compilação **da biblioteca** — no binário Release publicado no NuGet a chamada é removida do IL, e `MonitorExecutionTime` viraria letra morta para todo consumidor do pacote (mesmo em build Debug do app dele). A solução é o hook **virtual** `protected virtual void OnExecutionTimeMeasured(TimeSpan elapsed)`: a implementação padrão escreve em `Trace.WriteLine` (o símbolo `TRACE` fica ativo também em Release, então funciona no pacote publicado), e o consumidor sobrescreve o hook para integrar à observabilidade dele (`ILogger`, métricas) — sem a base impor dependência de logging. O hook só é invocado quando `MonitorExecutionTime = true`; desligado, custo zero.

### 9.11 Por que `ConfigureAwait(false)` em todos os `await` das classes base?
Por ser uma **biblioteca**, ela não deve capturar nem voltar ao `SynchronizationContext` do chamador (UI/ASP.NET legado). `ConfigureAwait(false)` evita deadlocks em chamadores que bloqueiam (`.Result`/`.Wait()`) e elimina o custo de re-despacho ao contexto original — recomendação canônica para código de biblioteca (analyzer `CA2007`). Como `Process` é síncrono e o I/O roda na fonte de dados, nenhum estado de contexto precisa ser preservado entre as fases.

### 9.12 Por que medir com `Stopwatch.GetTimestamp()`/`GetElapsedTime()` e não `new Stopwatch()`?
A API estática (`Stopwatch.GetTimestamp()` + `Stopwatch.GetElapsedTime(start)`, disponível desde .NET 7) mede o intervalo **sem alocar** o objeto `Stopwatch` no heap. Como `MonitorExecutionTime` pode estar ligado em caminhos quentes, evitar a alocação mantém o overhead mínimo e é coerente com a meta AOT/zero-overhead.

---

## 10. Posicionamento no Ecossistema .NET

Existem bibliotecas de "result type" em .NET. O diferencial de `ReturnSuccessOrError` é combinar o tipo de resultado com **abstrações de caso de uso e fonte de dados** (Clean Architecture) e com a **separação fetch/process com background opcional**.

| Biblioteca | Foco | Diferença |
|---|---|---|
| `OneOf` | União discriminada genérica `OneOf<T0, T1...>` | Genérica; não traz conceito de erro de domínio nem casos de uso |
| `ErrorOr` | `ErrorOr<T>` com lista de erros | Foco no result type; sem orquestração de caso de uso/fetch |
| `FluentResults` | `Result<T>` com reasons/errors encadeáveis | Rico em metadados; sem base de caso de uso nem background |
| `LanguageExt` | Programação funcional ampla em C# | Curva de aprendizado alta; escopo muito maior |
| **`ReturnSuccessOrError`** | **Result type + caso de uso + fonte de dados + fetch/process** | **Opinativo para Clean Architecture; mínimo e sem dependências** |

Pode coexistir com `MediatR`: um handler do MediatR pode delegar a um caso de uso desta biblioteca e devolver `ReturnSuccessOrError<T, E>`.

---

## 11. Estratégia de Testes (resumo)

Framework: **xUnit v3** + **NSubstitute** (mocking de `IDataSource`/`IRepository`) + **Shouldly** (asserts) + **coverlet** (cobertura).

| Área | Cenários |
|---|---|
| `ReturnSuccessOrError<T,E>` | igualdade por valor, `Match`, `switch` nativo, **exaustividade do switch no erro (union, sem `_`)** |
| `UsecaseBase<T,P,E>` | execução direta, em background, `MonitorExecutionTime` (resultado inalterado + hook `OnExecutionTimeMeasured` chamado/não chamado), erro de negócio via `Fail`/`Ok`, exceção inesperada → `OnUnexpected` (direto **e** background), OCE sem token cancelado → `OnUnexpected`, cancelamento (token pré-cancelado e cooperativo no `Process`, direto **e** background → OCE propaga), resultado `Unit`/`Nil` |
| `UsecaseBaseCallData<T,D,P,E>` | sucesso fetch+process, curto-circuito (process não chamado em `Failure` do repo), preservação do **caso concreto** do erro, `OnUnexpected`, paridade direto↔background, `CancellationToken`, token pré-cancelado → OCE sem chamar `Process` |
| `RepositoryBase<D,P,E>` | sucesso (dado→`Success`), exceção→`MapError` (caso do union traduzido), braço default, `CancellationToken` propagado, cancelamento do chamador → OCE propaga (sem `MapError`), OCE interno sem token cancelado → `MapError` |
| `AppError`/`ErrorGeneric` | comparação por valor, `WithMessage` preserva tipo concreto |
| `Parameters`/`NoParams` | só-dados (igualdade por valor), singleton `NoParams.Value` |
| `IDataSource<T,P>` | sucesso e exceção (fonte burra) |

Detalhamento completo no documento `DEVELOPMENT_PLAN.md`.

---

## 12. Dependências

| Tipo | Pacote | Uso |
|---|---|---|
| Runtime | **(nenhuma)** | O core depende apenas da BCL (`System.*`) — zero pacotes de runtime, AOT puro |
| Dev/Test | `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio` | Testes (xUnit v3) |
| Dev/Test | `NSubstitute` | Mocking de `IDataSource<T,P>` / `IRepository<T,P>` |
| Dev/Test | `Shouldly` | Asserts legíveis (`.ShouldBe(...)`) |
| Dev/Test | `coverlet.collector` | Cobertura de código (`--collect:"XPlat Code Coverage"`) |

---

## 13. Requisitos Não-Funcionais

- **Compatibilidade:** `net11.0` (alvo principal; STS/preview no momento). Avaliar multi-targeting com `netstandard2.1` para ampliar alcance, condicionado ao uso de `record`/pattern matching disponíveis.
- **Nullable reference types:** habilitado e sem warnings.
- **Warnings como erros:** `TreatWarningsAsErrors=true`.
- **Documentação XML:** gerada (`GenerateDocumentationFile=true`) para IntelliSense no consumidor.
- **Símbolos:** `snupkg` publicado para depuração.
- **AOT-friendly:** sem reflexão em runtime; compatível com Native AOT e trimming.

---

## 14. Resumo Executivo

`ReturnSuccessOrError` é uma biblioteca .NET mínima e opinativa para a camada de domínio que:

1. Modela o desfecho de operações como uma **união discriminada selada** (`Success<TValue>`/`Failure<TError>`) com **erro parametrizado** — o tratamento de erro é obrigatório e **exaustivo** (o `union` por feature obriga a cobrir todos os casos, sem `_`).
2. Fornece **bases de caso de uso** que orquestram busca de dados, curto-circuito de erros e processamento — o desenvolvedor escreve apenas `Process`, `MapError` e `OnUnexpected`.
3. Separa **I/O (busca)** de **CPU-bound (processamento)**, permitindo despachar o processamento pesado ao thread pool com uma flag.
4. Concentra a tradução de erro em pontos únicos e obrigatórios: `MapError` (exceção técnica → caso do `union`) e `OnUnexpected` (bug → caso do `union`); o `Process` nunca propaga exceção.
5. É **idiomática em .NET**: async/await, `CancellationToken`, records, pattern matching, DI por construtor, zero dependências de runtime, AOT-friendly.
