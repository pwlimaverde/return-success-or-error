# ReturnSuccessOrError

Uma biblioteca .NET mínima, opinativa e extremamente eficiente que fornece abstrações para a camada de domínio (casos de uso e fontes de dados) seguindo os princípios de **Clean Architecture** e **SOLID**.

O elemento central é o tipo de resultado discriminado e selado `ReturnSuccessOrError<TValue>`, que representa o desfecho de qualquer operação como **Sucesso** ou **Falha**, forçando o consumidor a tratar explicitamente ambos os cenários em tempo de compilação.

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

- **União Discriminada Selada (`ReturnSuccessOrError<TValue>`)**: Força o tratamento exaustivo de sucesso ou falha através de `Match`/`Switch` ou `switch` expression em C#. O compilador avisa caso você esqueça de lidar com o erro.
- **Orquestração em Fases (Fetch ➔ Curto-Circuito ➔ Process)**: A classe base orquestra a busca de dados na infraestrutura (I/O assíncrona) e isola o processamento puro no domínio. Se a busca falhar, ocorre um curto-circuito imediato: a fase de processamento de negócio sequer é executada.
- **Separação de Threads Inteligente**: A busca de dados (I/O) sempre ocorre de forma assíncrona tradicional. Contudo, o processamento de regras de negócio (CPU-bound) pode ser opcionalmente delegado ao pool de threads do .NET em segundo plano (`Task.Run`) com uma simples flag (`RunInBackground = true`), mantendo a infraestrutura de I/O intacta na thread original.
- **Preservação de Tipos (`IAppError.WithMessage`)**: Os erros de domínio são imutáveis. Ao enriquecer mensagens de erro durante a subida de camadas, o tipo concreto do erro é preservado, permitindo logs e tratamentos específicos.
- **Rastreabilidade Integrada**: Exceções capturadas na infraestrutura são automaticamente enriquecidas com códigos de rastreio descritivos e centralizados em constantes (`ErrorCodes.DataSourceCatch` para falhas na busca de dados, `ErrorCodes.BackgroundCatch` para exceções no processamento em segundo plano), facilitando a depuração em ambientes de produção.
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

## 🛠️ Conceitos Centrais

A biblioteca é estruturada em torno de tipos simples com responsabilidades únicas:

| Tipo / Contrato | Responsabilidade |
| :--- | :--- |
| `ReturnSuccessOrError<TValue>` | União discriminada abstrata e selada contendo os tipos aninhados `Success` e `Failure`. |
| `Success(TValue Value)` | Subtipo selado que encapsula o valor de sucesso. |
| `Failure(IAppError Error)` | Subtipo selado que encapsula o erro de domínio. |
| `Match<TResult>` | Método que força o consumo exaustivo com retorno tipado (duas funções: `onSuccess` + `onError`). |
| `Switch` | Variante de `Match` sem retorno, para efeitos colaterais como logging. |
| `IAppError` | Contrato para erros de domínio imutáveis. Expõe `Message` e `WithMessage` para enriquecimento preservando o tipo concreto. |
| `ErrorGeneric` | Implementação padrão de `IAppError` (`sealed record`). Pronta para uso quando não há necessidade de campos adicionais. |
| `IParametersReturnResult` | Contrato de entrada do Caso de Uso. Garante que todo parâmetro traga consigo o `IAppError` a ser usado em caso de falha. |
| `NoParams` | Implementação padrão de `IParametersReturnResult` para chamadas sem parâmetros extras, com erro default via implementação explícita de interface. |
| `IDataSource<TData>` | Abstração da fonte de dados externa (Porta de saída na arquitetura). Retorna o dado bruto ou lança a falha. |
| `UsecaseBase<TValue>` | Classe base para casos de uso com lógica pura de negócio, sem consultas externas. |
| `UsecaseBaseCallData<TValue, TData>` | Classe base que orquestra busca de dados via `IDataSource<TData>`, curto-circuito e processamento. |
| `IFeatureService` | Contrato marcador para serviços de feature (Service Layer). Ponto de entrada de uma feature que orquestra múltiplos casos de uso. Interface vazia, **zero dependência** — único tipo de feature embarcado. |
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
│   │   │   ├── ILoginDataSource.cs                 <-- Interface (Porta de saída)
│   │   │   ├── IRefreshDataSource.cs
│   │   │   ├── LoginGrpcDataSource.cs              <-- Implementação (Adaptador)
│   │   │   └── RefreshGrpcDataSource.cs
│   │   ├── Domain/
│   │   │   ├── Errors/
│   │   │   │   └── AuthErrors.cs                   <-- Erros tipados da feature
│   │   │   ├── Models/
│   │   │   │   └── Session.cs                      <-- Modelos de domínio imutáveis
│   │   │   ├── Parameters/
│   │   │   │   ├── LoginParameters.cs              <-- Parâmetros de entrada
│   │   │   │   └── RefreshParameters.cs
│   │   │   ├── Services/
│   │   │   │   └── IAuthService.cs                 <-- Contrato do Serviço (IFeatureService)
│   │   │   └── UseCases/
│   │   │       ├── LoginUseCase.cs                 <-- Caso de uso 1
│   │   │       └── RefreshTokenUseCase.cs          <-- Caso de uso 2
│   │   ├── Services/
│   │   │   └── AuthService.cs                      <-- Implementação (Orquestrador)
│   │   └── AuthModule.cs                           <-- Composição (padrão Feature Module, definido no seu app)
│   │
│   └── RelatorioVendas/                            <-- Outra feature do sistema
│       ├── DataSources/
│       ├── Domain/
│       ├── Services/
│       └── RelatorioVendasModule.cs
│
└── Program.cs
```

### Regras de Ouro da Estrutura

| Subdiretório | Responsabilidade | Dependências Permitidas |
| :--- | :--- | :--- |
| `Domain/Errors/` | Define records `sealed` implementando `IAppError`. | Apenas a biblioteca principal. |
| `Domain/Models/` | Entidades e modelos de domínio imutáveis. | Sem dependências externas. |
| `Domain/Parameters/` | Parâmetros de entrada dos casos de uso (`IParametersReturnResult`). | Apenas a biblioteca principal. |
| `Domain/Services/` | Interface do serviço da feature (`IXxxService : IFeatureService`). | Apenas a biblioteca principal. |
| `Domain/UseCases/` | Casos de uso herdando de `UsecaseBase` ou `UsecaseBaseCallData`. | Apenas a biblioteca principal e modelos da feature. |
| `DataSources/` | Interfaces de `IDataSource<T>` e suas implementações concretas (infraestrutura). | Depende de frameworks externos (EF, gRPC, HTTP). |
| `Services/` | Implementação concreta do serviço da feature. Orquestra os Casos de Uso. | Injeta usecases e abstrações de datasources. |
| `XxxModule.cs` | Classe de composição da feature (Composition Root), seguindo o padrão "Feature Module" — `IFeatureModule` é um contrato **definido no seu app** (ver seção de composição), não vem da biblioteca. | Depende do seu container de DI (ex.: `Microsoft.Extensions.DependencyInjection`) e acopla os tipos da feature. |

---

## 🚀 Como Funciona o Fluxo de Execução

Um caso de uso que consome dados externos opera como um pipeline ("trilho") estruturado pela classe base:

```text
Chamador (Controller/Handler)
  │   await usecase.CallAsync(parameters, cancellationToken)
  ▼
[UsecaseBaseCallData]
  │
  ├── FASE 1 (Fetch): Busca de dados brutos assíncrona (I/O) no contexto do chamador.
  │     ├── Sucesso ➔ Retorna os dados brutos obtidos.
  │     └── Exceção ➔ Captura automática, mapeia para IAppError (Cod. DataSourceCatch).
  │
  ├── FASE 2 (Curto-Circuito): Se a busca falhou, o fluxo é interrompido.
  │     └── O erro mapeado retorna diretamente para o chamador (Processamento é ignorado).
  │
  └── FASE 3 (Process): Executa a regra de negócio (CPU-bound) com o dado bruto carregado.
        ├── Direto (padrão) ou no Thread Pool (Task.Run) via RunInBackground = true.
        └── Exceção em background ➔ Captura automática (Cod. BackgroundCatch).
  │
  ▼
ReturnSuccessOrError<TValue>
  │
  ▼
result.Match(onSuccess: ..., onError: ...)   // Tratamento obrigatório
```

---

## 💻 Exemplos Práticos de Uso

### 1. Definição do Erro de Domínio (`IAppError`)

Crie erros imutáveis utilizando `record` para garantir igualdade por valor. O método `WithMessage` preserva o tipo concreto do erro:

```csharp
public sealed record RelatorioErro(string Message, int CodigoInterno) : IAppError
{
    public IAppError WithMessage(string message) => this with { Message = message };
}
```

> Para casos simples, a biblioteca já fornece o `ErrorGeneric(string Message)` pronto para uso.

### 2. Definição dos Parâmetros (`IParametersReturnResult`)

Os parâmetros envelopam todas as entradas necessárias para a execução do caso de uso e definem o erro que será propagado em caso de falhas de I/O:

```csharp
public sealed record GerarRelatorioParameters(
    int Mes,
    int Ano,
    IAppError Error) : IParametersReturnResult;
```

> Para chamadas sem parâmetros extras, a biblioteca fornece o `NoParams`:
> ```csharp
> var parametros = new NoParams(Error: new ErrorGeneric("Erro inesperado"));
> ```

### 3. Definição da Fonte de Dados (`IDataSource`)

A fonte de dados é implementada na camada de infraestrutura. Em caso de falha, ela lança uma exceção que será automaticamente encapsulada pela classe base com o código de rastreio `Cod. DataSourceCatch`:

```csharp
public interface IObterDadosVendasDataSource : IDataSource<List<VendaCrua>>;

// Implementação na camada de Infraestrutura
public class ObterDadosVendasDataSource : IObterDadosVendasDataSource
{
    private readonly AppDbContext _db;

    public ObterDadosVendasDataSource(AppDbContext db) => _db = db;

    public async Task<List<VendaCrua>> CallAsync(
        IParametersReturnResult parameters,
        CancellationToken cancellationToken = default)
    {
        return await _db.Vendas
            .Where(x => x.Ativo)
            .ToListAsync(cancellationToken);
    }
}
```

### 4. Caso de Uso com Fonte de Dados (`UsecaseBaseCallData`)

O caso de uso recebe o dado bruto já buscado pelo datasource. Ele sobrescreve apenas o método `Process` com a regra de negócio pura:

```csharp
public class GerarRelatorioUseCase : UsecaseBaseCallData<RelatorioVendasResult, List<VendaCrua>>
{
    public GerarRelatorioUseCase(IObterDadosVendasDataSource dataSource) : base(dataSource)
    {
        RunInBackground = true;       // O processamento pesado roda no Thread Pool!
        MonitorExecutionTime = true;  // Habilita medição automática de tempo no Debug
    }

    protected override ReturnSuccessOrError<RelatorioVendasResult> Process(
        List<VendaCrua> data,
        IParametersReturnResult parameters)
    {
        if (data.Count == 0)
        {
            return ReturnSuccessOrError<RelatorioVendasResult>.Err(
                parameters.Error.WithMessage("Nenhuma venda registrada no período selecionado."));
        }

        // Regra de negócio: Consolidação pesada de dados (CPU-bound)
        var totalFaturado = data.Sum(v => v.Quantidade * v.PrecoUnitario);
        var totalItens = data.Sum(v => v.Quantidade);

        var relatorio = new RelatorioVendasResult(totalFaturado, totalItens);
        return ReturnSuccessOrError<RelatorioVendasResult>.Ok(relatorio);
    }
}
```

### 5. Caso de Uso de Lógica Pura (`UsecaseBase`)

Para regras de negócio que não dependem de fontes de dados externas:

```csharp
public class CalcularComissaoUseCase : UsecaseBase<decimal>
{
    protected override ReturnSuccessOrError<decimal> Process(
        IParametersReturnResult parameters)
    {
        var parametros = (ComissaoParameters)parameters;

        if (parametros.ValorVenda <= 0)
        {
            return ReturnSuccessOrError<decimal>.Err(
                parameters.Error.WithMessage("O valor da venda deve ser positivo."));
        }

        var comissao = parametros.ValorVenda * 0.05m; // 5% de comissão
        return ReturnSuccessOrError<decimal>.Ok(comissao);
    }
}
```

### 6. Consumo e Tratamento Exaustivo

Quem invoca o Caso de Uso é obrigado pelo compilador a tratar o sucesso e a falha de forma limpa, eliminando a dependência de blocos `try/catch` na camada superior:

```csharp
var parametros = new GerarRelatorioParameters(
    Mes: 6,
    Ano: 2026,
    Error: new RelatorioErro("Falha ao processar o relatório financeiro.", 500));

var resultado = await _useCase.CallAsync(parametros);

// Opção 1: Via método Match (abordagem exaustiva recomendada)
string resposta = resultado.Match(
    onSuccess: relatorio => $"Faturamento total: {relatorio.FaturamentoTotal:C}",
    onError:   erro => $"Erro: {erro.Message}"
);

// Opção 2: Via método Switch (para efeitos colaterais sem retorno)
resultado.Switch(
    onSuccess: relatorio => Console.WriteLine($"Sucesso: {relatorio.FaturamentoTotal:C}"),
    onError:   erro => Console.WriteLine($"Falha: {erro.Message}")
);

// Opção 3: Via C# Pattern Matching (switch expression)
string respostaPattern = resultado switch
{
    ReturnSuccessOrError<RelatorioVendasResult>.Success(var relatorio)
        => $"Sucesso! Total: {relatorio.FaturamentoTotal}",
    ReturnSuccessOrError<RelatorioVendasResult>.Failure(var erro)
        => $"Falha: {erro.Message}",
};
```

### 7. Composição e Orquestração de Features Complexas (Service Layer & Feature Module)

Em cenários reais, módulos ou features frequentemente contêm múltiplos casos de uso, datasources e modelos. Para manter o acoplamento baixo e encapsular a complexidade de DI e orquestração, usamos o padrão de **Service Layer** (`IFeatureService`, que a biblioteca fornece) e **Composition Root local** (o padrão "Feature Module").

> **Por que o "Feature Module" não vem na biblioteca?** Registrar serviços é responsabilidade da camada de composição da sua aplicação, não do domínio. Para que o pacote tenha **zero dependências de runtime** e seja **agnóstico do seu container de DI**, ele não embarca tipos acoplados a `IServiceCollection`. Em vez disso, você define — **uma única vez, no seu projeto** — um contrato trivial de módulo e (opcionalmente) extensões fluentes. Quem usa `Microsoft.Extensions.DependencyInjection`:
>
> ```csharp
> // Definido no SEU projeto (camada de composição). Não vem do pacote.
> using Microsoft.Extensions.DependencyInjection;
>
> public interface IFeatureModule
> {
>     IServiceCollection RegisterServices(IServiceCollection services);
> }
>
> public static class FeatureModuleExtensions
> {
>     public static IServiceCollection AddFeature<TModule>(this IServiceCollection services)
>         where TModule : IFeatureModule, new()
>         => new TModule().RegisterServices(services);
>
>     public static IServiceCollection AddFeatures(
>         this IServiceCollection services, params IFeatureModule[] modules)
>     {
>         foreach (var module in modules)
>             module.RegisterServices(services);
>         return services;
>     }
> }
> ```
>
> Usa Autofac, Lamar, ou faz composição manual (Pure DI)? Basta registrar os tipos no estilo nativo desse container — o padrão abaixo é só uma sugestão de organização.

Abaixo está o exemplo completo da feature **Auth** de ponta a ponta, usando o contrato acima:

#### 7.1 Domínio (Modelos, Parâmetros e Erros)

```csharp
// Features/Auth/Domain/Errors/AuthErrors.cs
public sealed record AuthError(string Message) : IAppError
{
    public IAppError WithMessage(string message) => this with { Message = message };
}

public sealed record UnauthorizedError(string Message) : IAppError
{
    public IAppError WithMessage(string message) => this with { Message = message };
}

// Features/Auth/Domain/Models/Session.cs
public sealed record Session(
    string AccessToken,
    string RefreshToken,
    string TenantId,
    DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

// Features/Auth/Domain/Parameters/AuthParameters.cs
public sealed record LoginParameters(string Email, string Password, IAppError Error) : IParametersReturnResult;
public sealed record RefreshParameters(string RefreshToken, IAppError Error) : IParametersReturnResult;
public sealed record LogoutParameters(string? RefreshToken, IAppError Error) : IParametersReturnResult;
```

#### 7.2 Interfaces dos DataSources (Portas)

```csharp
// Features/Auth/DataSources/
public interface ILoginDataSource : IDataSource<Session>;
public interface IRefreshDataSource : IDataSource<Session>;
public interface ILogoutDataSource : IDataSource<Unit>;
```

#### 7.3 Casos de Uso (Lógica de Negócio)

```csharp
// Features/Auth/Domain/UseCases/LoginUseCase.cs
public class LoginUseCase : UsecaseBaseCallData<Session, Session>
{
    public LoginUseCase(ILoginDataSource dataSource) : base(dataSource) { }

    protected override ReturnSuccessOrError<Session> Process(Session data, IParametersReturnResult parameters)
        => ReturnSuccessOrError<Session>.Ok(data);
}

// Features/Auth/Domain/UseCases/RefreshTokenUseCase.cs
public class RefreshTokenUseCase : UsecaseBaseCallData<Session, Session>
{
    public RefreshTokenUseCase(IRefreshDataSource dataSource) : base(dataSource) { }

    protected override ReturnSuccessOrError<Session> Process(Session data, IParametersReturnResult parameters)
        => ReturnSuccessOrError<Session>.Ok(data);
}

// Features/Auth/Domain/UseCases/LogoutUseCase.cs
public class LogoutUseCase : UsecaseBaseCallData<Unit, Unit>
{
    public LogoutUseCase(ILogoutDataSource dataSource) : base(dataSource) { }

    protected override ReturnSuccessOrError<Unit> Process(Unit data, IParametersReturnResult parameters)
        => ReturnSuccessOrError<Unit>.Ok(Unit.Value);
}
```

#### 7.4 Service Layer — Contrato e Implementação (Orquestrador)

O Serviço encapsula a injeção e execução dos casos de uso, expondo um contrato simplificado para a camada externa (Controllers/Handlers):

```csharp
// Features/Auth/Domain/Services/IAuthService.cs
public interface IAuthService : IFeatureService
{
    Task<ReturnSuccessOrError<Session>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<ReturnSuccessOrError<Session>> RefreshAsync(CancellationToken ct = default);
    Task<ReturnSuccessOrError<Unit>> LogoutAsync(CancellationToken ct = default);
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

    public async Task<ReturnSuccessOrError<Session>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        var parameters = new LoginParameters(
            Email: email,
            Password: password,
            Error: new AuthError("Falha na autenticação."));

        return await _loginUseCase.CallAsync(parameters, ct);
    }

    public async Task<ReturnSuccessOrError<Session>> RefreshAsync(CancellationToken ct = default)
    {
        var parameters = new RefreshParameters(
            RefreshToken: "stored-token",
            Error: new UnauthorizedError("Sessão expirada."));

        return await _refreshUseCase.CallAsync(parameters, ct);
    }

    public async Task<ReturnSuccessOrError<Unit>> LogoutAsync(CancellationToken ct = default)
    {
        var parameters = new LogoutParameters(
            RefreshToken: null,
            Error: new AuthError("Falha ao encerrar sessão."));

        return await _logoutUseCase.CallAsync(parameters, ct);
    }
}
```

#### 7.5 Feature Module (Composition Root)

O módulo declara todas as dependências locais da feature de maneira isolada, implementando o contrato `IFeatureModule` que você definiu no início desta seção:

```csharp
// Features/Auth/AuthModule.cs
public class AuthModule : IFeatureModule
{
    public IServiceCollection RegisterServices(IServiceCollection services)
    {
        // DataSources (Infraestrutura)
        services.AddScoped<ILoginDataSource, LoginGrpcDataSource>();
        services.AddScoped<IRefreshDataSource, RefreshGrpcDataSource>();
        services.AddScoped<ILogoutDataSource, LogoutGrpcDataSource>();

        // UseCases (Domínio)
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();

        // Service (Orquestrador)
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
```

#### 7.6 Registro no `Program.cs` e Consumo no Controller

Com o `FeatureModuleExtensions` definido no seu projeto (mostrado no início desta seção), o registro torna-se limpo e focado no alto nível:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Registro de cada feature com uma única linha fluente
builder.Services.AddFeature<AuthModule>();
builder.Services.AddFeature<RelatorioVendasModule>();

// Ou registrando múltiplos módulos em lote:
builder.Services.AddFeatures(
    new AuthModule(),
    new RelatorioVendasModule()
);
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

        return resultado.Match(
            onSuccess: session => Ok(new { session.AccessToken, session.ExpiresAt }),
            onError:   erro => Unauthorized(new { erro.Message })
        );
    }
}
```

---

## 🔍 Rastreabilidade de Erros

A biblioteca enriquece automaticamente as mensagens de erro com códigos de rastreio internos, facilitando a identificação da origem de falhas:

| Origem da Falha | Código | Descrição |
| :--- | :--- | :--- |
| Regra de negócio deliberada | — | O `Process` retorna `Failure` diretamente com a mensagem customizada. |
| Exceção na busca de dados | `Cod. DataSourceCatch` | `IDataSource.CallAsync` lança exceção; capturada e enriquecida automaticamente. |
| Exceção no processamento em background | `Cod. BackgroundCatch` | `Process` lança exceção dentro de `Task.Run`; capturada e enriquecida automaticamente. |

Em todos os casos, o tipo concreto do `IAppError` é preservado via `WithMessage`, e a mensagem é enriquecida com o código de rastreio e o conteúdo da exceção.

---

## 🌟 Principais Vantagens

- **Garantias em Tempo de Compilação:** Tipos selados forçam o tratamento de erro. O compilador emite warning se você esquecer de tratar os caminhos de falha.
- **Legibilidade e Manutenibilidade:** O fluxo principal do sistema não fica oculto sob camadas de tratamento de exceção (`try/catch`).
- **Respeito ao Single Responsibility Principle (SRP):** O Caso de Uso gerencia apenas a lógica de negócio (`Process`). O DataSource gerencia apenas a chamada física à fonte de dados (`CallAsync`). A classe base orquestra o fluxo de controle, threads e captura de exceções.
- **Open/Closed Principle (OCP):** A estrutura do Template Method permite estender o comportamento (novos casos de uso) sem modificar o código das classes base.
- **Dependency Inversion Principle (DIP):** Casos de uso dependem apenas de abstrações (`IDataSource<T>`), nunca de implementações concretas de infraestrutura.
- **Composição e Modularização:** O marcador `IFeatureService` mais o padrão "Feature Module" (que você implementa na sua camada de composição) fazem cada funcionalidade encapsular suas dependências internas (usecases, datasources) e expor uma interface de serviço limpa. Registro desacoplado, modular e extensível — no container de DI que você já usa.
- **Zero Dependências de Runtime:** O core não carrega nenhum pacote de runtime (apenas a BCL `System.*`), permanecendo agnóstico do seu gerenciador de DI e sem impor frameworks de terceiros. Máxima interoperabilidade e zero conflito de versões.
- **AOT-Friendly & Leve:** Construído sobre recursos modernos de C# (`record`, `pattern matching`, generic constraints) sem reflexão em tempo de execução. Totalmente compatível com Native AOT e trimming.
- **Cancelamento Cooperativo:** `CancellationToken` propagado de ponta a ponta, integrando-se nativamente com ASP.NET Core e a BCL.
- **Observabilidade Opt-in:** Medição de tempo de execução habilitável por instância (`MonitorExecutionTime`), com custo zero quando desligada.

---

## 🌍 Licença

Este projeto está licenciado sob a licença MIT. Consulte o arquivo [LICENSE](LICENSE) para obter mais informações.
