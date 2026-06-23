using Microsoft.Extensions.DependencyInjection;

namespace ReturnSuccessOrError.Samples.Composition;

/// <summary>
/// Composition Root local de uma feature. <b>Definido no projeto consumidor</b> (não vem da
/// biblioteca) — demonstra a metodologia da PRD §5.10, mantendo o core agnóstico de DI.
/// </summary>
public interface IFeatureModule
{
    /// <summary>Registra DataSources, UseCases e o Service da feature.</summary>
    IServiceCollection RegisterServices(IServiceCollection services);
}

/// <summary>Extensões fluentes para compor features no container de DI.</summary>
public static class FeatureModuleExtensions
{
    /// <summary>Registra uma feature pelo seu módulo.</summary>
    public static IServiceCollection AddFeature<TModule>(this IServiceCollection services)
        where TModule : IFeatureModule, new()
        => new TModule().RegisterServices(services);

    /// <summary>Registra várias features de uma vez.</summary>
    public static IServiceCollection AddFeatures(
        this IServiceCollection services, params IFeatureModule[] modules)
    {
        foreach (var module in modules)
            module.RegisterServices(services);
        return services;
    }
}
