using DressCoder.Application.Services;
using DressCoder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DressCoder.Application;

/// <summary>
/// Composition entry point for the Application layer: wires Infrastructure plus any
/// Application-level services (orchestrators, DTO mappers) as they're implemented in later
/// stages (Etapa 5/6). Consumed from the UI's composition root (DressCoder.UI/App.xaml.cs).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDressCoderApplication(this IServiceCollection services)
    {
        services.AddDressCoderInfrastructure();

        services.AddSingleton<IStartupDiagnosticsService, StartupDiagnosticsService>();
        services.AddSingleton<IConversionSessionState, ConversionSessionState>();

        return services;
    }

    /// <summary>
    /// Adds the portable rolling-file logger. Exposed from Application (not Infrastructure
    /// directly) so the UI project only needs to reference Application, keeping the module
    /// boundaries from docs/02-documento-tecnico.md intact.
    /// </summary>
    public static ILoggingBuilder AddDressCoderFileLogging(this ILoggingBuilder builder, string logDirectory)
    {
        builder.AddFileLogging(logDirectory);
        return builder;
    }
}
