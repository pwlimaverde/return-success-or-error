# PRD — ReturnSuccessOrError (.NET)

**Produto:** Biblioteca NuGet `ReturnSuccessOrError`
**Plataforma-alvo:** .NET 9 / C# 13 (com suporte a `netstandard2.1` em avaliação)
**Tipo:** Biblioteca de domínio (class library), sem dependências de runtime de terceiros
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
| Resultado de erro perde o tipo concreto ao subir pelas camadas | `IAppError.WithMessage` preserva o tipo concreto do erro ao enriquecer a mensagem |
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
8. **Zero dependências de runtime** — usa apenas a BCL (`System.*`); não acopla o consumidor a nenhum framework.
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
- Contrato de erro (`IAppError`) e implementação concreta (`ErrorGeneric`).
- Contrato de parâmetros (`IParametersReturnResult`) e implementação concreta (`NoParams`).
- Contrato de fonte de dados (`IDataSource<TData>`).
- Classes base de caso de uso: lógica pura e lógica com fonte de dados.
- Orquestração fetch → curto-circuito → process.
- Execução opcional do processamento em thread pool (`Task.Run`).
- Tipos auxiliares `Unit` (ausência de valor) e `Nil` (null semântico).
- Medição opcional de tempo de execução.

### 4.2 Fora do Escopo

- Geração de código / source generators (avaliado para versão futura).
- Conversões implícitas avançadas (avaliado, opt-in futuro).
- Integração com bibliotecas de validação (`FluentValidation`) — pode ser usada em conjunto, mas não é incluída.

---

## 5. Arquitetura e Tipos

### 5.1 Hierarquia de Tipos

```
ReturnSuccessOrError<TValue>            (abstract record, selado por construtor privado)
├── Success(TValue Value)              (sealed record aninhado)
└── Failure(IAppError Error)           (sealed record aninhado)

IAppError                              (interface)
└── ErrorGeneric                       (sealed record)

IParametersReturnResult                (interface)
└── NoParams                           (sealed record)

IDataSource<TData>                     (interface)

UsecaseBase<TValue>                                (abstract class)
UsecaseBaseCallData<TValue, TData>                 (abstract class)

Unit                                   (sealed class — singleton)
Nil                                    (sealed class — singleton)
```

### 5.2 `ReturnSuccessOrError<TValue>` — Tipo Central

União discriminada implementada como `abstract record` com construtor privado, o que **fecha a hierarquia**: nenhum subtipo pode ser declarado fora da biblioteca. Os dois únicos casos são `record`s aninhados, permitindo desconstrução posicional e padrões de propriedade no `switch`.

```csharp
namespace ReturnSuccessOrError;

using System.Diagnostics;

public abstract record ReturnSuccessOrError<TValue>
{
    // Construtor privado: impede subtipos externos — a união é fechada.
    private ReturnSuccessOrError() { }

    /// <summary>Resultado bem-sucedido, carregando o valor de tipo <typeparamref name="TValue"/>.</summary>
    public sealed record Success(TValue Value) : ReturnSuccessOrError<TValue>;

    /// <summary>Resultado com falha, carregando um <see cref="IAppError"/>.</summary>
    public sealed record Failure(IAppError Error) : ReturnSuccessOrError<TValue>;

    // Fábricas estáticas — leitura fluente e evitam repetir o genérico.
    public static ReturnSuccessOrError<TValue> Ok(TValue value) => new Success(value);
    public static ReturnSuccessOrError<TValue> Err(IAppError error) => new Failure(error);

    /// <summary>Consumo exaustivo: obriga tratar sucesso e erro; nunca cai no caso default.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<IAppError, TResult> onError) => this switch
    {
        Success success => onSuccess(success.Value),
        Failure failure => onError(failure.Error),
        _ => throw new UnreachableException(),
    };

    /// <summary>Variante sem retorno, para efeitos colaterais (logging, side effects).</summary>
    public void Switch(Action<TValue> onSuccess, Action<IAppError> onError)
    {
        switch (this)
        {
            case Success success: onSuccess(success.Value); break;
            case Failure failure: onError(failure.Error); break;
            default: throw new UnreachableException();
        }
    }
}
```

**Padrões de consumo:**

```csharp
// 1) Via Match (recomendado — exaustivo por construção)
string message = result.Match(
    onSuccess: value => $"OK: {value}",
    onError:   error => $"Erro: {error.Message}");

// 2) Via switch expression (compilador avisa CS8524 se faltar caso)
string message = result switch
{
    ReturnSuccessOrError<string>.Success(var value) => $"OK: {value}",
    ReturnSuccessOrError<string>.Failure(var error) => $"Erro: {error.Message}",
};
```

> **Igualdade por valor:** por serem `record`s, `Success` e `Failure` recebem `Equals`/`GetHashCode`/`ToString` por valor automaticamente — equivalente ao comportamento esperado de uma união discriminada.

### 5.3 `IAppError` — Contrato de Erro

```csharp
namespace ReturnSuccessOrError;

public interface IAppError
{
    /// <summary>Descrição legível do erro.</summary>
    string Message { get; }

    /// <summary>
    /// Devolve uma nova instância com a mensagem substituída, preservando o
    /// tipo concreto. Implementações baseadas em <c>record</c> usam <c>with</c>.
    /// </summary>
    IAppError WithMessage(string message);
}
```

**Implementação concreta `ErrorGeneric`:**

```csharp
namespace ReturnSuccessOrError;

public sealed record ErrorGeneric(string Message) : IAppError
{
    public IAppError WithMessage(string message) => this with { Message = message };

    public override string ToString() => $"{nameof(ErrorGeneric)} - {Message}";
}
```

**Erro de domínio customizado (exemplo de uso pelo consumidor):**

```csharp
public sealed record ApiError(string Message, int StatusCode) : IAppError
{
    // O tipo concreto é preservado: WithMessage devolve ApiError, não IAppError genérico.
    public IAppError WithMessage(string message) => this with { Message = message };
}
```

### 5.4 `IParametersReturnResult` — Contrato de Parâmetros

```csharp
namespace ReturnSuccessOrError;

public interface IParametersReturnResult
{
    /// <summary>Erro a ser usado caso a operação falhe.</summary>
    IAppError Error { get; }
}
```

**Implementação concreta `NoParams`:**

```csharp
namespace ReturnSuccessOrError;

public sealed record NoParams(IAppError? Error = null) : IParametersReturnResult
{
    // Implementação explícita de interface: garante Error não-nulo sem
    // alterar a semântica nullable do parâmetro posicional do record.
    IAppError IParametersReturnResult.Error =>
        Error ?? new ErrorGeneric("NoParams: unspecified generic error");
}
```

### 5.5 `IDataSource<TData>` — Contrato de Fonte de Dados

```csharp
namespace ReturnSuccessOrError;

public interface IDataSource<TData>
{
    /// <summary>
    /// Executa a chamada externa e devolve o dado bruto, ou lança o
    /// <see cref="IAppError"/> dos parâmetros (como exceção) em caso de falha.
    /// </summary>
    Task<TData> CallAsync(
        IParametersReturnResult parameters,
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
        IParametersReturnResult parameters);

    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunStageAsync(parameters, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var result = await RunStageAsync(parameters, cancellationToken);
        stopwatch.Stop();
        LogTime(GetType().Name, stopwatch.ElapsedMilliseconds, RunInBackground);
        return result;
    }

    private Task<ReturnSuccessOrError<TValue>> RunStageAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        if (!RunInBackground)
            return Task.FromResult(Process(parameters));

        return Task.Run(() =>
        {
            try { return Process(parameters); }
            catch (Exception ex)
            {
                return ReturnSuccessOrError<TValue>.Err(
                    parameters.Error.WithMessage(
                        $"{parameters.Error.Message} - Cod. BackgroundCatch --- Catch: {ex}"));
            }
        }, cancellationToken);
    }

    private static void LogTime(string name, long ms, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {ms}ms");
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
        IParametersReturnResult parameters);

    public async Task<ReturnSuccessOrError<TValue>> CallAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        if (!MonitorExecutionTime)
            return await RunAsync(parameters, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var result = await RunAsync(parameters, cancellationToken);
        stopwatch.Stop();
        LogTime(GetType().Name, stopwatch.ElapsedMilliseconds, RunInBackground);
        return result;
    }

    private async Task<ReturnSuccessOrError<TValue>> RunAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        // FASE 1 — FETCH (no contexto da chamada; I/O-bound)
        var fetchResult = await FetchAsync(parameters, cancellationToken);

        // FASE 2 — CURTO-CIRCUITO no erro
        if (fetchResult is ReturnSuccessOrError<TData>.Failure failure)
            return ReturnSuccessOrError<TValue>.Err(failure.Error);

        var data = ((ReturnSuccessOrError<TData>.Success)fetchResult).Value;

        // FASE 3 — PROCESS (direto ou em background; CPU-bound)
        if (!RunInBackground)
            return Process(data, parameters);

        return await Task.Run(() =>
        {
            try { return Process(data, parameters); }
            catch (Exception ex)
            {
                return ReturnSuccessOrError<TValue>.Err(
                    parameters.Error.WithMessage(
                        $"{parameters.Error.Message} - Cod. BackgroundCatch --- Catch: {ex}"));
            }
        }, cancellationToken);
    }

    private async Task<ReturnSuccessOrError<TData>> FetchAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await _dataSource.CallAsync(parameters, cancellationToken);
            return ReturnSuccessOrError<TData>.Ok(data);
        }
        catch (Exception ex)
        {
            return ReturnSuccessOrError<TData>.Err(
                parameters.Error.WithMessage(
                    $"{parameters.Error.Message} - Cod. 02-1 --- Catch: {ex}"));
        }
    }

    private static void LogTime(string name, long ms, bool background) =>
        Debug.WriteLine($"[ReturnSuccessOrError] Execution Time {name} " +
                        $"({(background ? "Background" : "Direct")}): {ms}ms");
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

---

## 6. Padrões de Design (idiomas C#)

### 6.1 União Discriminada via `record` selado
`abstract record` com construtor privado + subtipos `sealed record` aninhados. É a forma canônica de modelar discriminated unions em C# moderno, com igualdade por valor e desconstrução posicional gratuitas.

### 6.2 Railway-Oriented Programming
O fluxo fetch → process é um "trilho": se a busca falha, o processamento é pulado e o erro segue direto até o chamador. Curto-circuito implementado com `is Failure`.

### 6.3 Template Method
`CallAsync` define o algoritmo (medição, fetch, curto-circuito, despacho para background); a subclasse fornece apenas `Process`.

### 6.4 `Process` como método abstrato (não delegate)
Diferente de abordagens baseadas em delegate/typedef, `Process` é um **método abstrato** sobrescrito pela subclasse — o idioma natural de polimorfismo em C#. Reduz boilerplate e é diretamente testável.

### 6.5 Implementação Explícita de Interface
`NoParams` implementa `IParametersReturnResult.Error` explicitamente para fornecer um default não-nulo sem alterar a natureza nullable do parâmetro do `record`.

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
| Exceção na busca de dados | `Cod. 02-1` | `IDataSource.CallAsync` lança; capturado em `FetchAsync` e enriquecido |
| Exceção no processamento em background | `Cod. BackgroundCatch` | `Process` lança dentro de `Task.Run`; capturado e enriquecido |

Em todos os casos de exceção, o **tipo concreto** do `IAppError` é preservado via `WithMessage`, e a mensagem é enriquecida com o código de rastreio e o conteúdo da exceção (`--- Catch: {ex}`).

---

## 8. Ciclo de Vida de uma Operação

```
Chamador
  │  await usecase.CallAsync(parameters, ct)
  ▼
[UsecaseBase]                          [UsecaseBaseCallData]
  Process(parameters)                    FASE 1: await dataSource.CallAsync(...)
   └─ direto ou Task.Run                   ├─ sucesso → Success<TData>(dado bruto)
   └─ exceção em bg → BackgroundCatch       └─ exceção → Cod. 02-1 → Failure<TData>
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

### 9.1 Por que construtor privado em `ReturnSuccessOrError<TValue>`?
Fecha a hierarquia. Sem ele, qualquer assembly poderia criar um terceiro subtipo, quebrando a exaustividade do `switch`/`Match`. Com o construtor privado, apenas `Success` e `Failure` (aninhados) existem.

### 9.2 Por que `Task.Run` em vez de algo "mais isolado"?
Processamento CPU-bound bloqueia a thread chamadora (de requisição no ASP.NET, ou de UI no MAUI). `Task.Run` despacha para o thread pool. Diferente de modelos de memória isolada, threads .NET compartilham memória — então **não há restrição de serialização** e o `Process` pode ser um método de instância normal. A busca de dados (I/O-bound) **não** vai para o thread pool: I/O assíncrono já libera a thread sem precisar de `Task.Run`, e despachar I/O para o pool é desperdício.

### 9.3 Por que `Process` é método abstrato e não um delegate?
Em C#, polimorfismo por método sobrescrito é o caminho idiomático e gera o melhor código. Não há a restrição (presente em modelos de isolamento de memória) de que a função precise ser estática para evitar capturar estado não-serializável. Mantemos a recomendação de **não acessar a fonte de dados dentro de `Process`** como boa prática de separação de responsabilidades, mas não é imposto pelo runtime.

### 9.4 Por que `Failure` e não `Error` como nome do subtipo?
`Error` colide semanticamente com `System.Error`/erros graves de runtime. `Failure` é mais claro e alinhado a bibliotecas funcionais consolidadas no ecossistema (`OneOf`, `ErrorOr`, `LanguageExt`).

### 9.5 Por que `IAppError` e não usar `Exception` diretamente?
Exceções são caras (captura de stack trace) e semanticamente representam fluxo excepcional. Aqui o erro é um **valor de domínio esperado** — modelá-lo como `record` imutável é mais barato e expressa melhor a intenção. A biblioteca só usa exceções na fronteira da fonte de dados, onde elas inevitavelmente ocorrem, convertendo-as imediatamente em valores.

### 9.6 Por que `WithMessage` na interface, se `record` já tem `with`?
`with` exige conhecer o tipo concreto. Quando se tem apenas uma referência `IAppError`, `with` não está disponível. `WithMessage` é o ponto polimórfico que permite enriquecer a mensagem sem conhecer o tipo concreto, mantendo-o preservado.

### 9.7 Por que o erro mora dentro de `IParametersReturnResult`?
Garante que o erro a ser retornado em caso de falha seja decidido **pelo chamador**, de forma tipada, antes da execução. Tanto a fonte de dados quanto o `Process` têm acesso a `parameters.Error` sem precisar fabricar erros em camadas internas.

### 9.8 Por que `RunInBackground` é `init` e não parâmetro de método?
É uma característica da configuração do caso de uso (decidida na composição/DI), não da chamada individual. `init` permite definir na inicialização do objeto e mantém imutável depois.

### 9.9 Por que `Unit` e `Nil` separados?
`void` não é um tipo de primeira classe em C# (não pode ser argumento genérico). `Unit` resolve isso para operações sem valor. `Nil` distingue "null é o resultado correto e esperado" de "ausência de valor por erro", evitando ambiguidade com `null`.

### 9.10 Por que `Debug.WriteLine` para o monitoramento?
Removido automaticamente em builds Release (a menos que `DEBUG` esteja definido), garantindo custo zero em produção — análogo a um recurso opt-in. Para observabilidade estruturada em produção, a versão futura preverá injeção opcional de `ILogger<T>`.

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

Framework: **xUnit** + **NSubstitute** (mocking de `IDataSource<T>`) + **FluentAssertions**.

| Área | Cenários |
|---|---|
| `ReturnSuccessOrError<T>` | acesso a `Success.Value`/`Failure.Error`, igualdade por valor, `Match`, `Switch`, exaustividade |
| `UsecaseBase<T>` | execução direta, em background, `MonitorExecutionTime`, exceção → `Cod. BackgroundCatch`, resultado `Unit`/`Nil` |
| `UsecaseBaseCallData<T,D>` | sucesso fetch+process, curto-circuito (process não chamado em falha de fetch), `Cod. 02-1`, paridade direto↔background, `CancellationToken` propagado |
| `IAppError`/`ErrorGeneric` | comparação por valor, `WithMessage` preserva tipo concreto |
| `NoParams` | erro default vs customizado |
| `IDataSource<T>` | sucesso e exceção |

Detalhamento completo no documento `DEVELOPMENT_PLAN.md`.

---

## 12. Dependências

| Tipo | Pacote | Uso |
|---|---|---|
| Runtime | *(nenhum)* | Apenas BCL `System.*` |
| Dev/Test | `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` | Testes |
| Dev/Test | `NSubstitute` | Mocking de `IDataSource<T>` |
| Dev/Test | `FluentAssertions` | Asserts legíveis |

---

## 13. Requisitos Não-Funcionais

- **Compatibilidade:** `net9.0` (alvo principal). Avaliar multi-targeting com `netstandard2.1` para ampliar alcance, condicionado ao uso de `record`/pattern matching disponíveis.
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
4. Preserva o **tipo concreto dos erros** ao enriquecê-los, com rastreabilidade via `Cod. 02-1` e `Cod. BackgroundCatch`.
5. É **idiomática em .NET**: async/await, `CancellationToken`, records, pattern matching, DI por construtor, zero dependências de runtime, AOT-friendly.
