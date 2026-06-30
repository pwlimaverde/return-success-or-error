# ReturnSuccessOrError

[![NuGet](https://img.shields.io/nuget/v/ReturnSuccessOrError.svg)](https://www.nuget.org/packages/ReturnSuccessOrError/)
[![Downloads](https://img.shields.io/nuget/dt/ReturnSuccessOrError.svg)](https://www.nuget.org/packages/ReturnSuccessOrError/)
[![CI](https://github.com/pwlimaverde/return-success-or-error/actions/workflows/ci.yml/badge.svg)](https://github.com/pwlimaverde/return-success-or-error/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Uma biblioteca .NET mínima, opinativa e extremamente eficiente que fornece abstrações para a camada de domínio (casos de uso, repositórios e fontes de dados) seguindo os princípios de **Clean Architecture** e **SOLID**, estruturadas em três camadas: **`DataSource → Repository → UseCase`**.

O elemento central é o tipo de resultado discriminado e selado `ReturnSuccessOrError<TValue, TError>`, que representa o desfecho de qualquer operação como **Sucesso** ou **Falha**. Com o **erro parametrizado** (`TError` é um `union` fechado, definido por feature), o compilador **obriga** o consumidor a tratar **todos** os erros possíveis daquela feature — sem braço `_`.

---

## 🧠 O Problema e a Filosofia da Biblioteca

Em sistemas desenvolvidos com **Clean Architecture**, o fluxo de execução tradicional segue o caminho:  
`Apresentação (Controllers/Handlers) ➔ Domínio (Casos de Uso) ➔ Infraestrutura (Fontes de Dados/APIs)`.

Dessa arquitetura surgem três dores recorrentes:

1. **Vazamento de Exceções de I/O:** Exceções originadas na infraestrutura (banco de dados, requisições HTTP) vazam para as camadas superiores de forma imprevisível, forçando o uso de blocos `try/catch` redundantes por toda a aplicação.
2. **Processamento Pesado (CPU-bound) Bloqueando a Thread Principal:** Casos de uso que realizam processamento computacional intensivo (parsing de arquivos, agregações complexas) rodam na mesma thread da requisição (ex: thread do pool de requisições HTTP), podendo impactar a escalabilidade ou congelar threads críticas.
3. **Erros Perdem sua Identidade:** Ao subir a pilha de chamadas, os erros costumam ser convertidos em strings simples ou exceções genéricas, perdendo o tipo concreto do erro e dificultando a depuração e o enriquecimento de mensagens.

### 🛡️ A Solução por Design

A `ReturnSuccessOrError` resolve esses problemas estruturando o fluxo de execução através de contratos bem delimitados:

- **União Discriminada com Erro Fechado (`ReturnSuccessOrError<TValue, TError>`)**: o erro de cada feature é um `union` fechado (`TError`); o `Match`/`switch` é **exaustivo, sem `_`** — o compilador não deixa você esquecer nenhum dos erros previstos.
- **Três camadas com fronteira clara (`DataSource → Repository → UseCase`)**: o **DataSource** é burro (devolve dado bruto ou lança exceção técnica); o **Repository** é a *anti-corruption layer* que traduz a exceção em erro de domínio (`MapError`); o **UseCase** recebe o dado já tratado e aplica a regra. Casos de uso dependem de `IRepository` (abstração), o que os torna **portáveis**: troca-se o datasource sem tocar na regra de negócio (DIP).
- **Orquestração em Fases (Fetch ➔ Curto-Circuito ➔ Process)**: a base do caso de uso orquestra a busca via repositório e isola o processamento puro no domínio. Se a busca falhar, ocorre um curto-circuito imediato: a fase de processamento de negócio sequer é executada.
- **Separação de Threads Inteligente**: A busca de dados (I/O) sempre ocorre de forma assíncrona tradicional. Contudo, o processamento de regras de negócio (CPU-bound) pode ser opcionalmente delegado ao pool de threads do .NET em segundo plano (`Task.Run`) com uma simples flag (`RunInBackground = true`), mantendo a infraestrutura de I/O intacta na thread original.
- **Erro fechado por feature (`union`)**: na construção da feature você define o conjunto de erros que ela pode produzir (um `union`). `MapError` (no Repository) traduz exceções técnicas nesses casos; `OnUnexpected` mapeia o bug inesperado; o `Process` devolve erros de negócio. `AppError` é uma base opcional dos records de erro (dá `Message`/`WithMessage`).
- **Nada propaga como exceção**: uma exceção inesperada no `Process` (direto ou background) é convertida via `OnUnexpected` num caso do `union` — o resultado é sempre um erro tratável, nunca um `throw` que escapa.
- **Cancelamento Cooperativo (`CancellationToken`)**: Todo o fluxo — busca de dados e processamento — propaga `CancellationToken` de ponta a ponta, integrando-se nativamente com o modelo de cancelamento do ASP.NET Core e da BCL.

---

## 📦 Instalação

```shell
dotnet add package ReturnSuccessOrError
```

Ou via `PackageReference` no `.csproj`:

```xml
<PackageReference Include="ReturnSuccessOrError" Version="1.*" />
```

> A biblioteca depende **apenas** das bibliotecas padrão do .NET (`System.*`) — **zero dependências de runtime de terceiros**, totalmente AOT-friendly. A composição de features com injeção de dependência é uma **metodologia documentada** (veja a seção de composição), então você usa o container de DI que preferir, sem que o pacote o imponha.

---

## ⚡ Início Rápido (em 4 passos)

Um caso de uso completo: busca um número na fonte de dados e responde se ele é **maior que 10**. As peças são **Erros (union) → Parameters → DataSource → Repository → UseCase**. Você escreve **a definição dos erros, a tradução (`MapError`/`OnUnexpected`) e a regra (`Process`)** — sem `try/catch` no domínio, sem `Ok(...)`/`Err(...)`.

```csharp
using ReturnSuccessOrError;

// 1) Conjunto FECHADO de erros da feature (records + union). ErrorGeneric (da lib) p/ o inesperado.
public sealed record NaoEhMaior(string Message) : AppError(Message);
public sealed record FonteIndisponivel(string Message) : AppError(Message);
public readonly union NumeroError(NaoEhMaior, FonteIndisponivel, ErrorGeneric);

// 2) Parâmetros — só dados.
public sealed record NumeroParams : Parameters;

// 3) Fonte de dados — burra: devolve o dado cru ou LANÇA exceção técnica.
public sealed class NumeroDataSource : IDataSource<int, NumeroParams>
{
    public Task<int> CallAsync(NumeroParams parameters, CancellationToken ct = default)
        => Task.FromResult(42); // simula o dado buscado
}

// 4) Repository — fronteira: MapError é ABSTRATO; traduz toda exceção num caso do union.
public sealed class NumeroRepository(IDataSource<int, NumeroParams> ds)
    : RepositoryBase<int, NumeroParams, NumeroError>(ds)
{
    protected override NumeroError MapError(Exception ex, NumeroParams p) => ex switch
    {
        HttpRequestException => new FonteIndisponivel("fonte fora do ar"),
        _                    => new ErrorGeneric($"inesperado: {ex.Message}"),
    };
}

// 5) Caso de uso — depende de IRepository (portável); recebe o número já buscado.
public sealed class MaiorQue10Usecase(IRepository<int, NumeroParams, NumeroError> repo)
    : UsecaseBaseCallData<bool, int, NumeroParams, NumeroError>(repo)
{
    protected override ReturnSuccessOrError<bool, NumeroError> Process(int data, NumeroParams p)
        => data > 10; // ← o bool vira Success<bool> sozinho (conversão implícita)

    protected override NumeroError OnUnexpected(Exception ex)
        => new ErrorGeneric($"bug: {ex.Message}"); // exceção inesperada no Process → caso do union
}
```

Executando e consumindo o resultado com `Match` (exaustivo — cobre os 3 erros, sem `_`):

```csharp
var usecase = new MaiorQue10Usecase(new NumeroRepository(new NumeroDataSource()));

ReturnSuccessOrError<bool, NumeroError> resultado = await usecase.CallAsync(new NumeroParams());

string texto = resultado.Match(
    onSuccess: valor => valor ? "maior que 10" : "não é maior",
    onError:   e => e switch
    {
        NaoEhMaior n          => n.Message,
        FonteIndisponivel f   => f.Message,
        ErrorGeneric g        => g.Message,
    });
```

**O que aconteceu por baixo:** a fonte devolveu `42` → o repositório empacotou em `Success<int>(42)` → o `Process` recebeu `42` → `42 > 10` → `Success<bool>(true)`. Se a fonte lançasse uma exceção, o `Repository.MapError` a traduziria num caso do `union`, o `Process` **nem seria chamado**, e o `Match` cairia em `onError`.

> Quer que "não ser maior que 10" seja um **erro de negócio**? Troque o corpo do `Process` por (note o cast ao union — duplo salto):
> ```csharp
> if (data <= 10)
>     return (NumeroError)new NaoEhMaior("número não é maior que 10"); // -> Failure
> return true;                                                         // -> Success
> ```

---

## 🛠️ Conceitos Centrais

A biblioteca é estruturada em torno de tipos simples com responsabilidades únicas:

| Tipo / Contrato | Responsabilidade |
| :--- | :--- |
| `ReturnSuccessOrError<TValue, TError>` | União discriminada (`union` do C# 15) sobre `Success<TValue>` e `Failure<TError>`. **Erro parametrizado** → consumo exaustivo sem `_`. |
| `Success<TValue>(TValue Value)` | Subtipo selado que encapsula o valor de sucesso. |
| `Failure<TError>(TError Error)` | Subtipo selado que encapsula o erro (tipicamente um `union` fechado da feature). |
| `Match<TResult>` | Método que força o consumo exaustivo com retorno tipado (`onSuccess` + `onError`). No `onError`, faça `switch` sobre o `union` do erro. |
| `AppError` / `ErrorGeneric` | **Base opcional** dos records de erro: dá `Message`, igualdade por valor e `WithMessage` (preserva o tipo concreto). `ErrorGeneric` é um caso pronto para o "inesperado". |
| `union FeatureError(...)` | (no consumidor) conjunto **fechado** dos erros da feature, usado como `TError`. Dá a exaustividade. |
| `Parameters` | `abstract record` de entrada do Caso de Uso — carrega **só dados**. |
| `NoParams` | Implementação padrão de `Parameters` para chamadas sem parâmetros extras (singleton `NoParams.Value`). |
| `IDataSource<TData, TParams>` | Fonte de dados **burra** (Porta de saída). Retorna o dado bruto **ou lança** exceção técnica — sem conhecimento de domínio. |
| `IRepository<TData, TParams, TError>` / `RepositoryBase` | **Fronteira** (anti-corruption layer): chama o datasource e traduz a exceção técnica num caso do `union` via `MapError` (**abstrato**). Devolve `ReturnSuccessOrError<TData, TError>`. |
| `UsecaseBase<TValue, TParams, TError>` | Classe base para casos de uso com lógica pura de negócio, sem consultas externas. |
| `UsecaseBaseCallData<TValue, TData, TParams, TError>` | Classe base que orquestra busca via `IRepository<TData, TParams, TError>`, curto-circuito e processamento. **Depende da abstração → usecase portável.** |
| `OnUnexpected(Exception)` | Método abstrato dos casos de uso: mapeia uma exceção inesperada do `Process` (bug) num caso do `union`. O `Process` nunca propaga. |
| `Unit` | Singleton que substitui o `void` como argumento genérico em operações sem valor de retorno. |
| `Nil` | Singleton que representa semanticamente um retorno nulo/vazio válido e bem-sucedido. |

---

## 📁 Estrutura de Código Recomendada (Convenção)

A padronização das pastas e dos arquivos de cada funcionalidade (**feature**) reduz a carga cognitiva do time e garante a consistência do ecossistema. Cada feature é isolada em seu próprio diretório, organizando-se de forma coesa e independente.

Para evitar que módulos complexos fiquem desorganizados quando múltiplos casos de uso coexistirem, este guia **recomenda** uma estrutura de pastas padronizada para cada feature. É uma convenção (não imposta pela biblioteca); adote-a por inteiro ou adapte ao seu contexto:

```text
MinhaAplicacao/
├── Features/
│   ├── Auth/                                       <-- Feature: Autenticação
│   │   ├── DataSources/
│   │   │   ├── ILoginDataSource.cs                 <-- Interface (Porta de saída, burra)
│   │   │   ├── IRefreshDataSource.cs
│   │   │   ├── LoginGrpcDataSource.cs              <-- Implementação (Adaptador)
│   │   │   └── RefreshGrpcDataSource.cs
│   │   ├── Repositories/
│   │   │   ├── LoginRepository.cs                  <-- Fronteira (MapError: exceção → caso do union)
│   │   │   └── RefreshRepository.cs
│   │   ├── Domain/
│   │   │   ├── Errors/
│   │   │   │   └── AuthErrors.cs                   <-- Erros tipados da feature
│   │   │   ├── Models/
│   │   │   │   └── Session.cs                      <-- Modelos de domínio imutáveis
│   │   │   ├── Parameters/
│   │   │   │   ├── LoginParameters.cs              <-- Parâmetros de entrada
│   │   │   │   └── RefreshParameters.cs
│   │   │   ├── Services/
│   │   │   │   └── IAuthService.cs                 <-- Contrato do Serviço (definido no seu app)
│   │   │   └── UseCases/
│   │   │       ├── LoginUseCase.cs                 <-- Caso de uso 1
│   │   │       └── RefreshTokenUseCase.cs          <-- Caso de uso 2
│   │   ├── Services/
│   │   │   └── AuthService.cs                      <-- Implementação (Orquestrador)
│   │   └── AuthServiceCollectionExtensions.cs      <-- Registro de DI: AddAuthFeature() (no seu app)
│   │
│   └── RelatorioVendas/                            <-- Outra feature do sistema
│       ├── DataSources/
│       ├── Domain/
│       ├── Services/
│       └── RelatorioVendasServiceCollectionExtensions.cs   <-- AddRelatorioVendasFeature()
│
├── Composition/
│   └── FeatureRegistration.cs                      <-- Agregador: AddFeatures() (encadeia as extensões)
└── Program.cs
```

### Regras de Ouro da Estrutura

| Subdiretório | Responsabilidade | Dependências Permitidas |
| :--- | :--- | :--- |
| `Domain/Errors/` | Define os records de erro (`: AppError`) **e o `union` fechado** da feature (usado como `TError`). | Apenas a biblioteca principal. |
| `Domain/Models/` | Entidades e modelos de domínio imutáveis. | Sem dependências externas. |
| `Domain/Parameters/` | Parâmetros de entrada dos casos de uso (`Parameters`, só dados). | Apenas a biblioteca principal. |
| `Domain/Services/` | Interface do serviço da feature (`IXxxService`), definida no seu app. | Apenas a biblioteca principal. |
| `Domain/UseCases/` | Casos de uso herdando de `UsecaseBase` ou `UsecaseBaseCallData` (dependem de `IRepository`). | Apenas a biblioteca principal e modelos da feature. |
| `DataSources/` | Interfaces de `IDataSource<T,P>` e suas implementações concretas burras (infraestrutura). | Depende de frameworks externos (EF, gRPC, HTTP). |
| `Repositories/` | Fronteira: `RepositoryBase<T,P,E>` com `MapError` (abstrato) traduzindo exceção técnica num caso do `union`. | Apenas a biblioteca principal e os datasources/erros da feature. |
| `Services/` | Implementação concreta do serviço da feature. Orquestra os Casos de Uso. | Injeta usecases e abstrações de datasources. |
| `XxxServiceCollectionExtensions.cs` | Registro de DI da feature num **método de extensão** idiomático (`AddXxxFeature()`), **definido no seu app** (ver seção de composição), não vem da biblioteca. | Depende do seu container de DI (ex.: `Microsoft.Extensions.DependencyInjection`) e acopla os tipos da feature. |

---

## 🚀 Como Funciona o Fluxo de Execução

Um caso de uso que consome dados externos opera como um pipeline ("trilho") estruturado pela classe base:

```text
Chamador (Controller/Handler)
  │   await usecase.CallAsync(parameters, cancellationToken)
  ▼
[UsecaseBaseCallData]
  │
  ├── FASE 1 (Fetch): await repository.CallAsync(...) — já tratado.
  │     │  [RepositoryBase]  try dataSource.CallAsync(...)  (I/O, fonte burra)
  │     │     ├── Sucesso ➔ Success<TData>(dado bruto).
  │     │     └── Exceção técnica ➔ MapError (abstrato) ➔ Failure (caso do union).
  │     └── Resultado: Success|Failure (o domínio nunca vê exceção de infra).
  │
  ├── FASE 2 (Curto-Circuito): Se a busca falhou, o fluxo é interrompido.
  │     └── O erro retorna diretamente para o chamador (Process é ignorado).
  │
  └── FASE 3 (Process): Executa a regra de negócio (CPU-bound) com o dado bruto carregado.
        ├── Direto (padrão) ou no Thread Pool (Task.Run) via RunInBackground = true.
        └── Exceção inesperada (direto OU background) ➔ OnUnexpected ➔ caso do union (nada propaga).
  │
  ▼
ReturnSuccessOrError<TValue, TError>
  │
  ▼
result.Match(onSuccess: ..., onError: e => e switch { ... })   // exaustivo, sem _
```

---

## 💻 Exemplos Práticos de Uso

### 1. Definição do Conjunto Fechado de Erros da Feature (`union`)

Na construção da feature, declare os erros que ela pode produzir (records, herdando de `AppError` por conveniência) e agrupe-os num `union` — é ele que vira o `TError`, garantindo consumo exaustivo:

```csharp
public sealed record SemVendas(string Message) : AppError(Message);
public sealed record BancoIndisponivel(string Message, int Codigo) : AppError(Message);
public sealed record TempoEsgotado(string Message) : AppError(Message);

// O conjunto FECHADO da feature (ErrorGeneric, da lib, como caso "inesperado"):
public readonly union RelatorioError(SemVendas, BancoIndisponivel, TempoEsgotado, ErrorGeneric);
```

> `ErrorGeneric(string Message)` (da lib) é um caso pronto para incluir no `union` como "inesperado" (alvo típico de `OnUnexpected`).

### 2. Definição dos Parâmetros (`Parameters`)

Os parâmetros envelopam todas as entradas necessárias para a execução do caso de uso. Carregam **só dados** (o erro não fica mais nos parâmetros):

```csharp
public sealed record GerarRelatorioParameters(int Mes, int Ano) : Parameters;
```

> Para chamadas sem parâmetros extras, a biblioteca fornece o singleton `NoParams.Value`.

### 3. Definição da Fonte de Dados (`IDataSource`) — camada burra

A fonte de dados é implementada na camada de infraestrutura e é **burra**: devolve o dado bruto ou lança uma exceção técnica. A tradução fica no Repository:

```csharp
public interface IObterDadosVendasDataSource : IDataSource<List<VendaCrua>, GerarRelatorioParameters>;

// Implementação na camada de Infraestrutura
public class ObterDadosVendasDataSource : IObterDadosVendasDataSource
{
    private readonly AppDbContext _db;

    public ObterDadosVendasDataSource(AppDbContext db) => _db = db;

    public async Task<List<VendaCrua>> CallAsync(
        GerarRelatorioParameters parameters,
        CancellationToken cancellationToken = default)
    {
        return await _db.Vendas
            .Where(x => x.Ativo)
            .ToListAsync(cancellationToken);
    }
}
```

### 4. Definição do Repository (`RepositoryBase`) — fronteira / tradução de erro

O repositório chama a fonte e traduz a exceção técnica num caso do `union` via `MapError` (**abstrato** — obrigatório):

```csharp
public sealed class RelatorioVendasRepository(IObterDadosVendasDataSource ds)
    : RepositoryBase<List<VendaCrua>, GerarRelatorioParameters, RelatorioError>(ds)
{
    protected override RelatorioError MapError(Exception ex, GerarRelatorioParameters p) => ex switch
    {
        DbException        => new BancoIndisponivel("Falha ao consultar o banco de vendas.", 500),
        TimeoutException   => new TempoEsgotado("Tempo esgotado ao consultar vendas."),
        _                  => new ErrorGeneric($"Falha inesperada: {ex.Message}"), // braço _ → "inesperado"
    };
}
```

### 5. Caso de Uso com Repositório (`UsecaseBaseCallData`)

O caso de uso depende de `IRepository` (portável) e recebe o dado bruto já buscado. Implementa `Process` (regra) e `OnUnexpected` (mapeia o bug inesperado num caso do `union`):

```csharp
public class GerarRelatorioUseCase
    : UsecaseBaseCallData<RelatorioVendasResult, List<VendaCrua>, GerarRelatorioParameters, RelatorioError>
{
    public GerarRelatorioUseCase(
        IRepository<List<VendaCrua>, GerarRelatorioParameters, RelatorioError> repository) : base(repository)
    {
        RunInBackground = true;       // O processamento pesado roda no Thread Pool!
        MonitorExecutionTime = true;  // Habilita medição automática de tempo no Debug
    }

    protected override ReturnSuccessOrError<RelatorioVendasResult, RelatorioError> Process(
        List<VendaCrua> data,
        GerarRelatorioParameters parameters)
    {
        if (data.Count == 0)
        {
            // Erro de NEGÓCIO -> Failure (cast ao union — duplo salto)
            return (RelatorioError)new SemVendas("Nenhuma venda registrada no período selecionado.");
        }

        // Regra de negócio: Consolidação pesada de dados (CPU-bound)
        var totalFaturado = data.Sum(v => v.Quantidade * v.PrecoUnitario);
        var totalItens = data.Sum(v => v.Quantidade);

        // RelatorioVendasResult -> Success (conversão implícita)
        return new RelatorioVendasResult(totalFaturado, totalItens);
    }

    protected override RelatorioError OnUnexpected(Exception ex)
        => new ErrorGeneric($"Bug ao gerar relatório: {ex.Message}");
}
```

### 6. Caso de Uso de Lógica Pura (`UsecaseBase`)

Para regras de negócio que não dependem de fontes de dados externas (sem datasource/repository):

```csharp
// ComissaoError = union(ValorInvalido, ErrorGeneric)
public class CalcularComissaoUseCase : UsecaseBase<decimal, ComissaoParameters, ComissaoError>
{
    protected override ReturnSuccessOrError<decimal, ComissaoError> Process(ComissaoParameters parameters)
    {
        if (parameters.ValorVenda <= 0)
            return (ComissaoError)new ValorInvalido("O valor da venda deve ser positivo."); // -> Failure

        var comissao = parameters.ValorVenda * 0.05m; // 5% de comissão
        return comissao;                              // decimal -> Success (conversão implícita)
    }

    protected override ComissaoError OnUnexpected(Exception ex)
        => new ErrorGeneric($"Bug no cálculo: {ex.Message}");
}
```

### 7. Consumo e Tratamento Exaustivo

Quem invoca o Caso de Uso é obrigado pelo compilador a tratar o sucesso e a falha de forma limpa, eliminando a dependência de blocos `try/catch` na camada superior:

```csharp
var parametros = new GerarRelatorioParameters(Mes: 6, Ano: 2026);

var resultado = await _useCase.CallAsync(parametros);

// Match — o onError faz switch sobre o union: EXAUSTIVO, sem _
string resposta = resultado.Match(
    onSuccess: relatorio => $"Faturamento total: {relatorio.FaturamentoTotal:C}",
    onError:   e => e switch
    {
        SemVendas s          => s.Message,
        BancoIndisponivel b  => $"Banco ({b.Codigo}): {b.Message}",
        TempoEsgotado t      => t.Message,
        ErrorGeneric g       => $"Inesperado: {g.Message}",
    });

// Via switch nativo sobre o resultado (Success | Failure) e depois sobre o union do erro:
switch (resultado)
{
    case Success<RelatorioVendasResult>(var relatorio):
        Console.WriteLine($"Sucesso: {relatorio.FaturamentoTotal:C}");
        break;
    case Failure<RelatorioError>(var erro):
        Console.WriteLine($"Falha: {erro switch { SemVendas s => s.Message, BancoIndisponivel b => b.Message, TempoEsgotado t => t.Message, ErrorGeneric g => g.Message }}");
        break;
}
```

> **Nada de `_`.** Se amanhã a feature ganhar um novo erro no `union`, **todos** os `switch` de consumo param de compilar até você tratá-lo — é a rede de segurança que o erro fechado oferece.

### 8. Composição e Orquestração de Features Complexas (Service Layer & DI idiomática)

Em cenários reais, módulos ou features frequentemente contêm múltiplos casos de uso, datasources e modelos. Para manter o acoplamento baixo e encapsular a complexidade de DI e orquestração, recomenda-se o padrão de **Service Layer** (um service por feature) e o **registro idiomático do .NET**: um **método de extensão por feature** (`AddXxxFeature()`) encadeado por um **agregador fino**. **Nada disso vem da biblioteca** — é sugestão de implementação, escrita no seu app.

> **Por que composição/DI não vem na biblioteca?** Registrar serviços é responsabilidade da camada de composição da sua aplicação, não do domínio. Para que o pacote tenha **zero dependências de runtime** e seja **agnóstico do seu container de DI**, ele não embarca tipos acoplados a `IServiceCollection` — nem mesmo um marcador de serviço. Em vez disso, cada feature expõe — **no seu projeto** — um método de extensão idiomático (como `AddControllers`/`AddDbContext` do próprio .NET), e um **agregador** os encadeia. Quem usa `Microsoft.Extensions.DependencyInjection`:
>
> ```csharp
> // Definido no SEU projeto (camada de composição). Não vem do pacote.
> using Microsoft.Extensions.DependencyInjection;
>
> // Agregador: ponto ÚNICO de DI (o "controle geral" das features).
> public static class FeatureRegistration
> {
>     public static IServiceCollection AddFeatures(this IServiceCollection services)
>         => services
>             .AddAuthFeature()
>             .AddRelatorioVendasFeature();
>             // adicionar feature = 1 linha
> }
> ```
>
> Cada `AddXxxFeature()` mora no arquivo da própria feature (ver 8.6). Sem interface custom, sem `new XxxModule()`, **sem reflexão** (AOT-friendly). Usa Autofac, Lamar, ou faz composição manual (Pure DI)? Basta registrar os tipos no estilo nativo desse container — o padrão abaixo é só uma sugestão de organização.

Abaixo está o exemplo completo da feature **Auth** de ponta a ponta, usando o padrão acima:

#### 8.1 Domínio (Modelos, Parâmetros e Erros)

```csharp
// Features/Auth/Domain/Errors/AuthErrors.cs — records + union FECHADO da feature
public sealed record InvalidCredentials(string Message) : AppError(Message);
public sealed record AccountLocked(string Message) : AppError(Message);
public sealed record SessionExpired(string Message) : AppError(Message);

public readonly union AuthError(InvalidCredentials, AccountLocked, SessionExpired, ErrorGeneric);

// Features/Auth/Domain/Models/Session.cs
public sealed record Session(
    string AccessToken,
    string RefreshToken,
    string TenantId,
    DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

// Features/Auth/Domain/Parameters/AuthParameters.cs — só dados (sem AppError)
public sealed record LoginParameters(string Email, string Password) : Parameters;
public sealed record RefreshParameters(string RefreshToken) : Parameters;
public sealed record LogoutParameters(string? RefreshToken) : Parameters;
```

#### 8.2 Interfaces dos DataSources (Portas, burras)

```csharp
// Features/Auth/DataSources/
public interface ILoginDataSource : IDataSource<Session, LoginParameters>;
public interface IRefreshDataSource : IDataSource<Session, RefreshParameters>;
public interface ILogoutDataSource : IDataSource<Unit, LogoutParameters>;
```

#### 8.3 Repositories (Fronteira — tradução de erro)

```csharp
// Features/Auth/Repositories/ — MapError abstrato (as três features usam o union AuthError)
public sealed class LoginRepository(ILoginDataSource ds)
    : RepositoryBase<Session, LoginParameters, AuthError>(ds)
{
    protected override AuthError MapError(Exception ex, LoginParameters p) => ex switch
    {
        HttpRequestException { StatusCode: 401 } => new InvalidCredentials("Credenciais inválidas."),
        HttpRequestException { StatusCode: 423 } => new AccountLocked("Conta bloqueada."),
        _                                         => new ErrorGeneric($"Falha inesperada: {ex.Message}"),
    };
}

public sealed class RefreshRepository(IRefreshDataSource ds)
    : RepositoryBase<Session, RefreshParameters, AuthError>(ds)
{
    protected override AuthError MapError(Exception ex, RefreshParameters p)
        => new SessionExpired($"Sessão expirada: {ex.Message}");
}

public sealed class LogoutRepository(ILogoutDataSource ds)
    : RepositoryBase<Unit, LogoutParameters, AuthError>(ds)
{
    protected override AuthError MapError(Exception ex, LogoutParameters p)
        => new ErrorGeneric($"Falha ao encerrar sessão: {ex.Message}");
}
```

#### 8.4 Casos de Uso (Lógica de Negócio — dependem de IRepository)

```csharp
// Features/Auth/Domain/UseCases/LoginUseCase.cs
public class LoginUseCase : UsecaseBaseCallData<Session, Session, LoginParameters, AuthError>
{
    public LoginUseCase(IRepository<Session, LoginParameters, AuthError> repository) : base(repository) { }

    protected override ReturnSuccessOrError<Session, AuthError> Process(Session data, LoginParameters p)
        => data;  // Session -> Success (conversão implícita)

    protected override AuthError OnUnexpected(Exception ex) => new ErrorGeneric(ex.Message);
}

// RefreshTokenUseCase e LogoutUseCase seguem o mesmo molde (TError = AuthError + OnUnexpected).
public class RefreshTokenUseCase : UsecaseBaseCallData<Session, Session, RefreshParameters, AuthError>
{
    public RefreshTokenUseCase(IRepository<Session, RefreshParameters, AuthError> repository) : base(repository) { }
    protected override ReturnSuccessOrError<Session, AuthError> Process(Session data, RefreshParameters p) => data;
    protected override AuthError OnUnexpected(Exception ex) => new ErrorGeneric(ex.Message);
}

public class LogoutUseCase : UsecaseBaseCallData<Unit, Unit, LogoutParameters, AuthError>
{
    public LogoutUseCase(IRepository<Unit, LogoutParameters, AuthError> repository) : base(repository) { }
    protected override ReturnSuccessOrError<Unit, AuthError> Process(Unit data, LogoutParameters p) => Unit.Value;
    protected override AuthError OnUnexpected(Exception ex) => new ErrorGeneric(ex.Message);
}
```

#### 8.5 Service Layer — Contrato e Implementação (Orquestrador)

O Serviço encapsula a injeção e execução dos casos de uso, expondo um contrato simplificado para a camada externa (Controllers/Handlers):

```csharp
// Features/Auth/Domain/Services/IAuthService.cs — contrato definido no SEU app
public interface IAuthService
{
    Task<ReturnSuccessOrError<Session, AuthError>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<ReturnSuccessOrError<Session, AuthError>> RefreshAsync(CancellationToken ct = default);
    Task<ReturnSuccessOrError<Unit, AuthError>> LogoutAsync(CancellationToken ct = default);
}

// Features/Auth/Services/AuthService.cs
public class AuthService : IAuthService
{
    private readonly LoginUseCase _loginUseCase;
    private readonly RefreshTokenUseCase _refreshUseCase;
    private readonly LogoutUseCase _logoutUseCase;

    public AuthService(
        LoginUseCase loginUseCase,
        RefreshTokenUseCase refreshUseCase,
        LogoutUseCase logoutUseCase)
    {
        _loginUseCase = loginUseCase;
        _refreshUseCase = refreshUseCase;
        _logoutUseCase = logoutUseCase;
    }

    public async Task<ReturnSuccessOrError<Session, AuthError>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        var parameters = new LoginParameters(Email: email, Password: password);
        return await _loginUseCase.CallAsync(parameters, ct);
    }

    public async Task<ReturnSuccessOrError<Session, AuthError>> RefreshAsync(CancellationToken ct = default)
    {
        var parameters = new RefreshParameters(RefreshToken: "stored-token");
        return await _refreshUseCase.CallAsync(parameters, ct);
    }

    public async Task<ReturnSuccessOrError<Unit, AuthError>> LogoutAsync(CancellationToken ct = default)
    {
        var parameters = new LogoutParameters(RefreshToken: null);
        return await _logoutUseCase.CallAsync(parameters, ct);
    }
}
```

#### 8.6 Registro da Feature (método de extensão idiomático)

A feature declara todas as suas dependências locais de forma isolada, num **método de extensão** `AddAuthFeature()` (o "módulo" no idioma do .NET). Note o registro das **três camadas** (DataSource → Repository → UseCase) mais o Service:

```csharp
// Features/Auth/AuthServiceCollectionExtensions.cs
public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        // DataSources (Infraestrutura, burros)
        services.AddScoped<ILoginDataSource, LoginGrpcDataSource>();
        services.AddScoped<IRefreshDataSource, RefreshGrpcDataSource>();
        services.AddScoped<ILogoutDataSource, LogoutGrpcDataSource>();

        // Repositories (Fronteira / tradução de erro) — registrados pela ABSTRAÇÃO (DIP)
        services.AddScoped<IRepository<Session, LoginParameters, AuthError>, LoginRepository>();
        services.AddScoped<IRepository<Session, RefreshParameters, AuthError>, RefreshRepository>();
        services.AddScoped<IRepository<Unit, LogoutParameters, AuthError>, LogoutRepository>();

        // UseCases (Domínio — dependem de IRepository)
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();

        // Service (Orquestrador)
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
```

#### 8.7 Registro no `Program.cs` e Consumo no Controller

Com o agregador `FeatureRegistration` definido no seu projeto (mostrado no início desta seção), o `Program.cs` fica em **uma linha** — ponto único de composição:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Registra TODAS as features (a lista vive no agregador).
builder.Services.AddFeatures();
```

```csharp
// Features/Auth/Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var resultado = await _authService.LoginAsync(request.Email, request.Password, ct);

        // O switch sobre o union AuthError é exaustivo: cada erro vira o status HTTP adequado.
        return resultado.Match(
            onSuccess: session => Ok(new { session.AccessToken, session.ExpiresAt }),
            onError:   e => e switch
            {
                InvalidCredentials => Unauthorized(),
                AccountLocked      => StatusCode(423),
                SessionExpired     => Unauthorized(),
                ErrorGeneric g     => Problem(g.Message),
            });
    }
}
```

---

## 🔁 Portabilidade de Casos de Uso

Como o caso de uso depende de `IRepository` (abstração), levar uma regra de negócio de um projeto a outro — ou trocar a origem dos dados — significa **apenas trocar o datasource**, sem tocar no `Process`. O mesmo `GerarRelatorioUseCase` roda com uma fonte HTTP em produção, um CSV num batch e um fake nos testes:

```csharp
// Mesmo usecase, fontes diferentes — só o datasource muda.
var deApi = new GerarRelatorioUseCase(
    new RelatorioVendasRepository(new ObterDadosVendasApiDataSource(http)));

var deCsv = new GerarRelatorioUseCase(
    new RelatorioVendasRepository(new ObterDadosVendasCsvDataSource(arquivo)));

var deTeste = new GerarRelatorioUseCase(
    new RelatorioVendasRepository(new FakeVendasDataSource()));

// Os três produzem o mesmo resultado para os mesmos dados.
var r = await deApi.CallAsync(new GerarRelatorioParameters(6, 2026));
```

É o **Dependency Inversion Principle** na prática: a regra de negócio não conhece a infraestrutura concreta. Veja o sample `SalesReport` (em `samples/`) demonstrando o mesmo usecase com `InMemoryDataSource` e `CsvDataSource`.

---

## 🔍 Modelo de Erro (de onde vem cada falha)

Todas as origens de falha caem num **caso do `union` da feature** — por isso o consumo é exaustivo e contempla todas:

| Origem da Falha | Quem traduz | Como ocorre |
| :--- | :--- | :--- |
| Regra de **negócio** deliberada | o próprio `Process` | `return (FeatureError)new AlgumCaso(...)` (cast ao union — duplo salto) |
| Falha de **I/O** (banco, HTTP, timeout) | `RepositoryBase.MapError` (abstrato) | `IDataSource.CallAsync` lança exceção técnica; `MapError` a mapeia num caso do `union` |
| Exceção **inesperada** (bug) no `Process` | `OnUnexpected(Exception)` (abstrato) | em direto **ou** background, a base captura e mapeia num caso do `union` — nada propaga |

Não há códigos de rastreio embutidos: o consumidor escolhe o caso de erro em cada ponto. O `union` fechado garante que, se uma nova origem precisar de um novo caso, o compilador force a atualização de todos os `switch` de consumo.

---

## 🌟 Principais Vantagens

- **Garantias em Tempo de Compilação:** o erro fechado (`union`) força o tratamento **exaustivo** — adicionar um novo erro quebra a compilação de todo consumo que ainda não o trata.
- **Legibilidade e Manutenibilidade:** O fluxo principal do sistema não fica oculto sob camadas de tratamento de exceção (`try/catch`).
- **Respeito ao Single Responsibility Principle (SRP):** O Caso de Uso gerencia apenas a lógica de negócio (`Process`). O Repository concentra a tradução de erro (`MapError`). O DataSource gerencia apenas a chamada física à fonte (`CallAsync`). A classe base orquestra o fluxo de controle, threads e curto-circuito.
- **Open/Closed Principle (OCP):** A estrutura do Template Method permite estender o comportamento (novos casos de uso) sem modificar o código das classes base.
- **Dependency Inversion Principle (DIP):** Casos de uso dependem apenas de abstrações (`IRepository<T,P,E>`), nunca de implementações concretas de infraestrutura — o que os torna **portáveis** (troca-se o datasource sem tocar na regra).
- **Composição e Modularização:** O método de extensão por feature (`AddXxxFeature()`) + agregador fino (que você implementa na sua camada de composição, fora do core) faz cada funcionalidade encapsular suas dependências internas (usecases, datasources) e expor uma interface de serviço limpa. Registro desacoplado, modular e extensível, no idioma do .NET — no container de DI que você já usa.
- **Zero Dependências de Runtime:** O core não carrega nenhum pacote de runtime (apenas a BCL `System.*`), permanecendo agnóstico do seu gerenciador de DI e sem impor frameworks de terceiros. Máxima interoperabilidade e zero conflito de versões.
- **AOT-Friendly & Leve:** Construído sobre recursos modernos de C# (`record`, `pattern matching`, generic constraints) sem reflexão em tempo de execução. Totalmente compatível com Native AOT e trimming.
- **Cancelamento Cooperativo:** `CancellationToken` propagado de ponta a ponta, integrando-se nativamente com ASP.NET Core e a BCL.
- **Observabilidade Opt-in:** Medição de tempo de execução habilitável por instância (`MonitorExecutionTime`), com custo zero quando desligada.

---

## 🌍 Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE) para obter mais informações.
