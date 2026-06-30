using Microsoft.Extensions.DependencyInjection;
using ReturnSuccessOrError.Samples.Features.CheckConnection;
using ReturnSuccessOrError.Samples.Features.Fibonacci;
using ReturnSuccessOrError.Samples.Features.SalesReport;

namespace ReturnSuccessOrError.Samples.Composition;

/// <summary>
/// Agregador de composição da aplicação. <b>Definido no projeto consumidor</b> (não vem da
/// biblioteca) — encadeia os métodos de extensão idiomáticos de cada feature (<c>AddXxxFeature()</c>)
/// num ponto único de DI, mantendo o core agnóstico de DI (ver PRD §5.9–5.10).
/// </summary>
public static class FeatureRegistration
{
    /// <summary>Registra TODAS as features. Adicionar uma feature = uma linha aqui.</summary>
    public static IServiceCollection AddFeatures(this IServiceCollection services)
        => services
            .AddCheckConnectionFeature()
            .AddFibonacciFeature()
            .AddSalesReportFeature();
}
