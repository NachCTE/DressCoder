using DressCoder.Core.Converter;
using DressCoder.Core.Parser;
using DressCoder.Core.Validator;
using DressCoder.Infrastructure.Assembly;
using DressCoder.Infrastructure.ExternalTools;
using Microsoft.Extensions.DependencyInjection;

namespace DressCoder.Infrastructure;

/// <summary>
/// Registers Infrastructure services (external tool wrappers) into the DI container.
/// Consumed from the composition root (see the `di-setup` step, in DressCoder.UI).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDressCoderInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ExternalToolLocator>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<OodleLibraryResolver>();
        services.AddSingleton<RepakLegacyPakBuilder>();

        services.AddTransient<IPakReader, RetocPakReader>();
        services.AddTransient<IContainerBuilder, RetocContainerBuilder>();
        services.AddTransient<IPluginAssembler, PluginAssembler>();
        services.AddTransient<IModValidator, ModValidator>();

        return services;
    }
}
