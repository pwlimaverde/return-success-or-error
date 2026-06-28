# PRD — ReturnSuccessOrError (.NET)

**Produto:** Biblioteca NuGet `ReturnSuccessOrError`
**Plataforma-alvo:** .NET 11 / C# 15 — _preview_ (com suporte a `netstandard2.1` em avaliação)
**Tipo:** Biblioteca de domínio (class library), zero dependências de runtime
**Licença:** MIT
**Data:** 2026-06-22

---

## 1. Visão Geral

`ReturnSuccessOrError` é uma biblioteca C# que fornece abstrações para a camada de domínio (casos de uso e fontes de dados) seguindo os princípios da Clean Architecture. Seu elemento central é um **tipo de resultado discriminado e selado** — `ReturnSuccessOrError<TValue>` — que representa o desfecho de qualquer operação como **sucesso** ou **erro**, obrigando o consumidor a tratar ambos os casos explicitamente em vez de depender de exceções que atravessam camadas.

A biblioteca substitui o padrão de `try/catch` espalhado por um fluxo previsível e tipado: o caso de uso busca dados de uma fonte (`IDataSource<T>`), faz curto-circuito automático em caso de falha, e processa o dado bruto numa função de negócio — opcionalmente em uma thread de background quando o processamento é intensivo em CPU.

### 1.1 Problemas que a biblioteca resolve

| Problema recorrente em .NET | Solução da biblioteca |
|---|---|
| Exceções de I/O (banco, HTTP) vazam para camadas superiores e quebram o fluxo de forma imprevisível | Toda operação retorna `ReturnSuccessOrError<TValue>`; exceções são capturadas e encapsuladas como erro tipado |
| `try/catch` repetido e inconsistente em cada handler/controller/service | A classe base orquestra captura, enriquecimento e curto-circuito de erros uma única vez |
| Processamento CPU-bound (parsing, agregação) bloqueia a thread de requisição ou a UI | Separação fetch/process: o processamento pesado pode ser delegado a `Task.Run` (thread pool) via flag opt-in |
| Resultado de erro perde o tipo concreto ao subir pelas camadas | `AppError.WithMessage` preserva o tipo concreto do erro ao enriquecer a mensagem |
| Esquecer de tratar o caso de erro é fácil e silencioso | Tipo selado + método `Match` exaustivo tornam o tratamento de erro obrigatório |

---

## 2. Objetivos do Produto

### 2.1 Objetivos Primários

1. **Eliminar o vazamento de exceções entre camadas.** Casos de uso nunca propagam exceções não tratadas para o chamador — devolvem um resultado tipado.
2. **Tornar o tratamento de erro obrigatório e visível.** O tipo selado com `Match`/`switch` força o consumidor a lidar com sucesso e erro, com verificação do compilador.
3. **Padronizar a camada de domínio.** Duas classes base (`UsecaseBase<T>` e `UsecaseBaseCallData<T, TData>`) definem o esqueleto de execução; o desenvolvedor implementa apenas a regra de negócio.
4. **Separar I/O de processamento.** A busca de dados (assíncrona, I/O-bound) roda no contexto da chamada; o processamento (CPU-bound) pode ir para o thread pool sem bloquear a thread chamadora.

### 2.2 Objetivos Secundários

5. **Integração idiomática com o ecossistema .NET** — suporte a `CancellationToken`, compatível com injeção de dependência (`Microsoft.Extensions.DependencyInjection`) e ASP.NET Core.
6. **Tipagem forte ponta a ponta** — parâmetros, dado bruto da fonte, valor de sucesso e erro são todos genéricos e verificados em compilação.
7. **Imutabilidade por padrão** — erros e parâmetros são `record`s imutáveis; enriquecimento via expressão `with`.
8. **Zero dependências de runtime** — usa apenas a BCL (`System.*`); não acopla o consumidor a nenhum framework nem a um container de DI específico (a composição de features é convenção documentada, não tipo embarcado).
9. **Observabilidade opt-in** — medição de tempo de execução habilitável por instância, sem custo quando desligada.

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

- Tipo de resultado discriminado selado (`ReturnSuccessOrError<TValue>`) com subtipos `Success` e `Failure`.
- Método `Match` e suporte a `switch` por padrão para consumo exaustivo.
- Contrato de erro (`AppError`) e implementação concreta (`ErrorGeneric`).
- Códigos de rastreio centralizados em constantes (`ErrorCodes.DataSourceCatch` / `ErrorCodes.BackgroundCatch`).
- Contrato de parâmetros (`ParametersReturnResult`) e implementação concreta (`NoParams`).
- Contrato de fonte de dados (`IDataSource<TData>`).
- Classes base de caso de uso: lógica pura e lógica com fonte de dados.
- Orquestração fetch → curto-circuito → process.
- Execução opcional do processamento em thread pool (`Task.Run`).
- Tipos auxiliares `Unit` (ausência de valor) e `Nil` (null semântico).
- Medição opcional de tempo de execução.
- Contrato marcador para serviços de feature (Service Layer) — `IFeatureService` (interface vazia, zero dependência).
- **Metodologia de composição de features** (estrutura de pastas + padrão Module/Service) entregue como **convenção documentada** — ver seção 5.10.

### 4.2 Fora do Escopo

- Geração de código / source generators (avaliado para versão futura).
- Conversões implícitas avançadas (avaliado, opt-in futuro).
- Integração com bibliotecas de validação (`FluentValidation`) — pode ser usada em conjunto, mas não é incluída.
- **Tipos acoplados a um container de DI** (`IFeatureModule`/`FeatureModuleExtensions` baseados em `IServiceCollection`): para manter o core com **zero dependências de runtime** e agnóstico de DI, o registro de serviços é tratado como padrão recomendado (seção 5.10), não como tipo embarcado. Um pacote satélite opcional para Microsoft.Extensions.DI fica no roadmap.

---

## 5. Arquitetura e Tipos

### 5.1 Hierarquia de Tipos

```
ReturnSuccessOrError<TValue>            (readonly union — C# 15)
├── Success<TValue>(TValue Value)      (sealed record top-level)
└── Failure(AppError Error)           (sealed record top-level)

AppError                              (interface)
└── ErrorGeneric                       (sealed record)

ErrorCodes                             (static class — constantes de rastreio)

ParametersReturnResult                (interface)
└── NoParams                           (sealed record)

IDataSource<TData>                     (interface)

UsecaseExecutorBase<TValue>                        (abstract class — base comum: medição + background)
├── UsecaseBase<TValue>                            (abstract class — só processamento)
└── UsecaseBaseCallData<TValue, TData>             (abstract class — fetch + processamento)

IFeatureService                         (interface — marcador para Service Layer; zero dependência)

Unit                                   (sealed class — singleton)
Nil                                    (sealed class — singleton)
```

### 5.2 `ReturnSuccessOrError<TValue>` — Tipo Central

União discriminada implementada como **`union` nativo do C# 15** sobre dois `sealed record` **top-level**. O `union` **fecha a hierarquia** na linguagem: o compilador prova a exaustividade do `switch`/`Match` — não há caso default nem subtipo externo possível. Os casos são `record`s, permitindo desconstrução posicional e padrões de propriedade no `switch`.

```csharp
namespace ReturnSuccessOrError;

/// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
public sealed record Success<TValue>(TValue Value);

/// <summary>Resultado com falha, carregando um <see cref="AppError"/>.</summary>
public sealed record Failure(AppError Error);

public readonly union ReturnSuccessOrError<TValue>(Success<TValue>, Failure)
{
    // Criação por conversão implícita (ex.: return value;  /  return parameters.Error;).
    // Não há fábricas Ok/Err públicas: o consumidor nunca constrói a união, só a consome.
    public static implicit operator ReturnSuccessOrError<TValue>(TValue value) => new Success<TValue>(value);
    public static implicit operator ReturnSuccessOrError<TValue>(AppError error) => new Failure(error);

    /// <summary>Consumo exaustivo: o compilador prova a exaustividade — sem caso default.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<AppError, TResult> onError) => this switch
    {
        Success<TValue> success => onSuccess(success.Value),
        Failure failure => onError(failure.Error),
    };
}
```

**Padrões de consumo:**

```csharp
// 1) Via Match (recomendado — exaustivo por construção)
string message = result.Match(
    onSuccess: value => $"OK: {value}",
    onError:   error => $"Erro: {error.Message}");

// 2) Via switch expression (exaustivo: o union dispensa caso default)
string message = result switch
{
    Success<string>(var value) => $"OK: {value}",
    Failure(var error) => $"Erro: {error.Message}",
};
```

> **Criação só por conversão implícita:** não há fábricas `Ok`/`Err` públicas. A união nasce de um `return value;` (→ `Success`) ou `return error;` (→ `Failure`), tanto no `Process` do consumidor quanto na base da lib. O consumidor nunca constrói a união — só a consome via `Match` ou `switch` nativo.
>
> **Igualdade por valor:** o `union` (struct) recebe `Equals`/`GetHashCode` por valor, e os casos `Success`/`Failure`, por serem `record`s, também — duas conversões `(ReturnSuccessOrError<int>)1` são iguais entre si.
>
> **Pegadinha (struct wrapper):** o `union` é um struct que encapsula o caso. `GetType()` devolve o tipo do **union**, não de `Success`/`Failure`; e `is Failure` sobre uma referência `object` (boxed) dá `false`. Verifique o caso por pattern matching com o tipo estático do union (`result is Failure f`), nunca por `GetType()`/`ShouldBeOfType`. Ver `tests/ResultAssertions.cs`.

### 5.3 `AppError` — Contrato de Erro

`AppError` é um **`abstract record`** (não interface): todo erro é, por contrato, um valor imutável com igualdade por valor. `WithMessage` é implementado **uma única vez** na base — o operador `with` usa o clone virtual sintetizado do `record`, que despacha para o subtipo real, preservando o tipo concreto e os campos extras. Subtipos não reimplementam nada.

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

**Erro de domínio customizado (exemplo de uso pelo consumidor):**

```csharp
// Uma linha: herda WithMessage da base, ganha igualdade por valor e preserva StatusCode.
public sealed record ApiError(string Message, int StatusCode) : AppError(Message);
```

> **Por que `abstract record` e não `interface`?** O erro só carrega dados — não tem comportamento próprio além de `WithMessage`, que é idêntico para todos. Forçar `record` codifica a intenção ("erro é valor imutável") no tipo e elimina o boilerplate de reimplementar `WithMessage` em cada filho. Quem precisar de um erro que não seja `record` deve usar exceção, não este contrato.

#### 5.3.1 `ErrorCodes` — Códigos de Rastreio (constantes)

Os códigos que a biblioteca anexa às mensagens ao capturar exceções **não** são literais mágicos espalhados pelo código: ficam centralizados em constantes nomeadas e descritivas. Isso dá um nome único de verdade (o consumidor referencia `ErrorCodes.DataSourceCatch` em testes/filtros em vez de copiar a string) e torna os códigos coerentes entre si (`DataSourceCatch` ↔ `BackgroundCatch`, ambos no padrão `<Origem>Catch`).

```csharp
namespace ReturnSuccessOrError;

/// <summary>
/// Códigos de rastreio anexados às mensagens de erro pela infraestrutura da
/// biblioteca ao converter exceções em <see cref="AppError"/>. Públicos para
/// permitir asserções e filtros sem depender de strings literais.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Exceção lançada pela fonte de dados durante o fetch (fase 1).</summary>
    public const string DataSourceCatch = "DataSourceCatch";

    /// <summary>Exceção lançada por <c>Process</c> ao rodar em background (fase 3).</summary>
    public const string BackgroundCatch = "BackgroundCatch";
}
```

> A mensagem final fica, por exemplo: `Falha ao buscar vendas - Cod. DataSourceCatch --- Catch: System.InvalidOperationException: ...`. O prefixo `Cod.` é mantido por legibilidade; o token após ele é sempre uma constante de `ErrorCodes`.

### 5.4 `ParametersReturnResult` — Contrato de Parâmetros

Como `AppError`, `ParametersReturnResult` é um **`abstract record`**: parâmetros são valores imutáveis que apenas carregam dados (incluindo o `AppError` a usar em caso de falha). Subtipos passam o erro à base via `: ParametersReturnResult(Error)`.

```csharp
namespace ReturnSuccessOrError;

public abstract record ParametersReturnResult(AppError Error);
```

**Implementação concreta `NoParams`:**

```csharp
namespace ReturnSuccessOrError;

public sealed record NoParams : ParametersReturnResult
{
    // Sem erro informado, fornece um ErrorGeneric default não-nulo à base.
    public NoParams(AppError? error = null)
        : base(error ?? new ErrorGeneric("NoParams: unspecified generic error")) { }
}
```

**Parâmetro customizado (exemplo de uso pelo consumidor):**

```csharp
// Campo próprio (N) + o Error exigido pela base.
public sealed record FibonacciParameters(int N, AppError Error) : ParametersReturnResult(Error);
```

### 5.5 `IDataSource<TData>` — Contrato de Fonte de Dados

```csharp
namespace ReturnSuccessOrError;

public interface IDataSource<TData>
{
    /// <summary>
    /// Executa a chamada externa e devolve o dado bruto, ou lança o
    /// <see cref="AppError"/> dos parâmetros (como exceção) em caso de falha.
    /// </summary>
    Task<TData> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default);
}
```

> **Convenção:** em caso de falha, a fonte de dados lança uma exceção. A classe base a captura e a converte em `Failure`. O `CancellationToken` é idiomático em .NET e propagado em todo o fluxo.

### 5.6 `UsecaseBase<TValue>` — Caso de Uso de Lógica Pura

Para regras de negócio que não dependem de fonte de dados externa.

```csharp
namespace ReturnSuccessOrError;

using System.Diagnostics;

public abstract class UsecaseBase<TValue>
{
    /// <summary>Se verdadeiro, o processamento roda no thread pool (Task.Run).</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Se verdadeiro, mede e registra o tempo de execução.</summary>
    public bool MonitorExecutionTime { get; init; }

    /// <summary>Regra de negócio implementada pela subclasse.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(
        ParametersReturnResult parameters);

    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunStageAsync(parameters, cancellationToken).ConfigureAwait(false);

        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await RunStageAsync(parameters, cancellationToken).ConfigureAwait(false);
        LogTime(GetType().Name, Stopwatch.GetElapsedTime(startTimestamp), RunInBackground);
        return result;
    }

    private Task<ReturnSuccessOrError<TValue>> RunStageAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        if (!RunInBackground)
            return Task.FromResult(Process(parameters));

        return Task.Run<ReturnSuccessOrError<TValue>>(() =>
        {
            try { return Process(parameters); }
            catch (Exception ex)
            {
                // AppError -> Failure (conversão implícita); Task.Run<...> anotado fixa o tipo.
                return parameters.Error.WithMessage(
                    $"{parameters.Error.Message} - Cod. {ErrorCodes.BackgroundCatch} --- Catch: {ex}");
            }
        }, cancellationToken);
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
```

### 5.7 `UsecaseBaseCallData<TValue, TData>` — Caso de Uso com Fonte de Dados

Para regras de negócio que buscam dados externos e os processam.

```csharp
namespace ReturnSuccessOrError;

using System.Diagnostics;

public abstract class UsecaseBaseCallData<TValue, TData>
{
    private readonly IDataSource<TData> _dataSource;

    /// <summary>Afeta SOMENTE o processamento; a busca de dados nunca vai para background.</summary>
    public bool RunInBackground { get; init; }

    /// <summary>Mede busca + processamento.</summary>
    public bool MonitorExecutionTime { get; init; }

    protected UsecaseBaseCallData(IDataSource<TData> dataSource) =>
        _dataSource = dataSource;

    /// <summary>Regra de negócio: recebe o dado bruto já carregado e os parâmetros.</summary>
    protected abstract ReturnSuccessOrError<TValue> Process(
        TData data,
        ParametersReturnResult parameters);

    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunAsync(parameters, cancellationToken).ConfigureAwait(false);

        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await RunAsync(parameters, cancellationToken).ConfigureAwait(false);
        LogTime(GetType().Name, Stopwatch.GetElapsedTime(startTimestamp), RunInBackground);
        return result;
    }

    private async Task<ReturnSuccessOrError<TValue>> RunAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH (no contexto da chamada; I/O-bound)
        var fetchResult = await FetchAsync(parameters, cancellationToken).ConfigureAwait(false);

        // FASE 2 — CURTO-CIRCUITO: switch exaustivo (union). Failure flui entre genéricos;
        //          o Success é desconstruído por pattern matching (cast direto não é permitido em union).
        return fetchResult switch
        {
            Failure failure => failure,
            Success<TData> success =>
                await ProcessStageAsync(success.Value, parameters, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<ReturnSuccessOrError<TValue>> ProcessStageAsync(
        TData data, ParametersReturnResult parameters, CancellationToken cancellationToken)
    {
        // FASE 3 — PROCESS direto (CPU-bound na thread chamadora)...
        if (!RunInBackground)
            return Process(data, parameters);

        // ...ou despachado ao thread pool. Só o background converte exceção em Failure.
        return await Task.Run<ReturnSuccessOrError<TValue>>(() =>
        {
            try { return Process(data, parameters); }
            catch (Exception ex)
            {
                // AppError -> Failure (conversão implícita); Task.Run<...> anotado fixa o tipo.
                return parameters.Error.WithMessage(
                    $"{parameters.Error.Message} - Cod. {ErrorCodes.BackgroundCatch} --- Catch: {ex}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReturnSuccessOrError<TData>> FetchAsync(
        ParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            // TData -> Success (conversão implícita)
            return await _dataSource.CallAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // AppError -> Failure (conversão implícita)
            return parameters.Error.WithMessage(
                $"{parameters.Error.Message} - Cod. {ErrorCodes.DataSourceCatch} --- Catch: {ex}");
        }
    }

    private static void LogTime(string name, TimeSpan elapsed, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {elapsed.TotalMilliseconds:F2}ms");
}
```

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
| `Unit` | Operação sem valor de retorno (efeito colateral bem-sucedido) | `ReturnSuccessOrError<Unit>` |
| `Nil` | `null` como resultado válido e esperado | `ReturnSuccessOrError<Nil>` |

### 5.9 `IFeatureService`

Interface marcadora para a camada de serviços de uma feature (Service Layer). Implementações funcionam como a "Facade de Domínio" que expõe os casos de uso públicos da feature, orquestrando-os de forma centralizada e ocultando os detalhes de implementação das camadas superiores (Controllers, Handlers).

```csharp
namespace ReturnSuccessOrError;

/// <summary>
/// Contrato marcador para serviços de feature (Service Layer).
/// <para>
/// Implementações são o ponto de entrada público de uma feature, encapsulando
/// a orquestração de um ou mais <see cref="UsecaseBase{TValue}"/> /
/// <see cref="UsecaseBaseCallData{TValue, TData}"/>.
/// </para>
/// </summary>
public interface IFeatureService;
```

### 5.10 Metodologia de composição de features (convenção, não tipo embarcado)

> **Decisão de design.** O registro de dependências é responsabilidade da **camada de composição** (`Program.cs`/Main), não do domínio. Para manter o core com **zero dependências de runtime** e **agnóstico de container de DI**, a biblioteca **não embarca** uma interface de módulo nem extensões acopladas a `IServiceCollection`. Em vez disso, descreve um **padrão recomendado** que o consumidor implementa no seu próprio projeto, com o container que ele já usa (Microsoft.Extensions.DI, Autofac, Lamar, Pure DI etc.).

O padrão "Feature Module" como **Composition Root local**: cada feature expõe um ponto único que registra seus `DataSources`, `UseCases` e o `Service`. Para quem usa `Microsoft.Extensions.DependencyInjection`, basta o consumidor definir — **no app dele** — uma interface trivial e (opcionalmente) extensões fluentes:

```csharp
// Definido NO PROJETO DO CONSUMIDOR (não vem da biblioteca).
// O consumidor escolhe a abstração de DI que usa — aqui, a do .NET.
using Microsoft.Extensions.DependencyInjection;

public interface IFeatureModule
{
    IServiceCollection RegisterServices(IServiceCollection services);
}

public static class FeatureModuleExtensions
{
    public static IServiceCollection AddFeature<TModule>(this IServiceCollection services)
        where TModule : IFeatureModule, new()
        => new TModule().RegisterServices(services);

    public static IServiceCollection AddFeatures(
        this IServiceCollection services, params IFeatureModule[] modules)
    {
        foreach (var module in modules)
            module.RegisterServices(services);
        return services;
    }
}
```

Quem faz **Pure DI** (composição manual) ou usa outro container simplesmente registra os tipos no estilo nativo desse container — sem precisar deste padrão. O único tipo que a biblioteca fornece para essa camada é o marcador `IFeatureService` (seção 5.9), que não depende de nada.

> **Roadmap.** Se houver demanda, um **pacote satélite opcional** `ReturnSuccessOrError.DependencyInjection` poderá fornecer `IFeatureModule`/`AddFeature` prontos para `Microsoft.Extensions.DependencyInjection` — isolando a dependência fora do core, que permanece zero-dep.

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

### 6.5 Erro default via construtor
`NoParams` herda de `ParametersReturnResult` com um construtor que aplica o fallback (`: base(error ?? new ErrorGeneric(...))`), fornecendo um `AppError` não-nulo quando nenhum é informado — sem expor um `Error` nullable.

### 6.6 Imutabilidade com `record` + `with`
Erros e parâmetros são imutáveis. O enriquecimento de mensagem usa `with { Message = ... }`, preservando o tipo concreto — substitui o `copyWith` manual de outras linguagens.

### 6.7 Injeção de Dependência via Construtor
A fonte de dados é injetada no construtor, compatível com `Microsoft.Extensions.DependencyInjection`. Facilita substituição por fakes/substitutes em teste.

### 6.8 Async/await + `CancellationToken`
Toda operação é assíncrona e cooperativamente cancelável, alinhada às convenções da BCL e do ASP.NET Core.

---

## 7. Fluxo de Erros e Códigos Internos

| Origem | Código | Como ocorre |
|---|---|---|
| Erro de negócio deliberado | — | O próprio `Process` devolve `Failure(parameters.Error.WithMessage(...))` |
| Exceção na busca de dados | `Cod. DataSourceCatch` | `IDataSource.CallAsync` lança; capturado em `FetchAsync` e enriquecido |
| Exceção no processamento em background | `Cod. BackgroundCatch` | `Process` lança dentro de `Task.Run`; capturado e enriquecido |

Em todos os casos de exceção, o **tipo concreto** do `AppError` é preservado via `WithMessage`, e a mensagem é enriquecida com o código de rastreio e o conteúdo da exceção (`--- Catch: {ex}`).

---

## 8. Ciclo de Vida de uma Operação

```
Chamador
  │  await usecase.CallAsync(parameters, ct)
  ▼
[UsecaseBase]                          [UsecaseBaseCallData]
  Process(parameters)                    FASE 1: await dataSource.CallAsync(...)
   └─ direto ou Task.Run                   ├─ sucesso → Success<TData>(dado bruto)
   └─ exceção em bg → BackgroundCatch       └─ exceção → Cod. DataSourceCatch → Failure<TData>
                                          FASE 2: is Failure? → propaga Failure<TValue>
                                          FASE 3: Process(dado, parameters)
                                            └─ direto ou Task.Run
                                            └─ exceção em bg → Cod. BackgroundCatch
  │
  ▼
ReturnSuccessOrError<TValue>
  │
  ▼
result.Match(onSuccess: ..., onError: ...)   // tratamento obrigatório
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

### 9.7 Por que o erro mora dentro de `ParametersReturnResult`?
Garante que o erro a ser retornado em caso de falha seja decidido **pelo chamador**, de forma tipada, antes da execução. Tanto a fonte de dados quanto o `Process` têm acesso a `parameters.Error` sem precisar fabricar erros em camadas internas.

### 9.8 Por que `RunInBackground` é `init` e não parâmetro de método?
É uma característica da configuração do caso de uso (decidida na composição/DI), não da chamada individual. `init` permite definir na inicialização do objeto e mantém imutável depois.

### 9.9 Por que `Unit` e `Nil` separados?
`void` não é um tipo de primeira classe em C# (não pode ser argumento genérico). `Unit` resolve isso para operações sem valor. `Nil` distingue "null é o resultado correto e esperado" de "ausência de valor por erro", evitando ambiguidade com `null`.

### 9.10 Por que `Debug.WriteLine` para o monitoramento?
Removido automaticamente em builds Release (a menos que `DEBUG` esteja definido), garantindo custo zero em produção — análogo a um recurso opt-in. Para observabilidade estruturada em produção, a versão futura preverá injeção opcional de `ILogger<T>`.

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

Pode coexistir com `MediatR`: um handler do MediatR pode delegar a um caso de uso desta biblioteca e devolver `ReturnSuccessOrError<T>`.

---

## 11. Estratégia de Testes (resumo)

Framework: **xUnit v3** + **NSubstitute** (mocking de `IDataSource<T>`) + **Shouldly** (asserts) + **coverlet** (cobertura).

| Área | Cenários |
|---|---|
| `ReturnSuccessOrError<T>` | acesso a `Success.Value`/`Failure.Error`, igualdade por valor, `Match`, `switch` nativo, exaustividade |
| `UsecaseBase<T>` | execução direta, em background, `MonitorExecutionTime`, exceção → `Cod. BackgroundCatch`, resultado `Unit`/`Nil` |
| `UsecaseBaseCallData<T,D>` | sucesso fetch+process, curto-circuito (process não chamado em falha de fetch), `Cod. DataSourceCatch`, paridade direto↔background, `CancellationToken` propagado |
| `AppError`/`ErrorGeneric` | comparação por valor, `WithMessage` preserva tipo concreto |
| `NoParams` | erro default vs customizado |
| `IDataSource<T>` | sucesso e exceção |

Detalhamento completo no documento `DEVELOPMENT_PLAN.md`.

---

## 12. Dependências

| Tipo | Pacote | Uso |
|---|---|---|
| Runtime | **(nenhuma)** | O core depende apenas da BCL (`System.*`) — zero pacotes de runtime, AOT puro |
| Dev/Test | `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio` | Testes (xUnit v3) |
| Dev/Test | `NSubstitute` | Mocking de `IDataSource<T>` |
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

1. Modela o desfecho de operações como uma **união discriminada selada** (`Success`/`Failure`), tornando o tratamento de erro obrigatório e verificável.
2. Fornece **bases de caso de uso** que orquestram busca de dados, curto-circuito de erros e processamento — o desenvolvedor escreve apenas `Process`.
3. Separa **I/O (busca)** de **CPU-bound (processamento)**, permitindo despachar o processamento pesado ao thread pool com uma flag.
4. Preserva o **tipo concreto dos erros** ao enriquecê-los, com rastreabilidade via constantes `ErrorCodes.DataSourceCatch` e `ErrorCodes.BackgroundCatch`.
5. É **idiomática em .NET**: async/await, `CancellationToken`, records, pattern matching, DI por construtor, zero dependências de runtime, AOT-friendly.
