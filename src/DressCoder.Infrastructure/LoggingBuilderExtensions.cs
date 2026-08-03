using DressCoder.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace DressCoder.Infrastructure;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Adds the portable rolling-file logger (see <see cref="FileLoggerProvider"/>) writing to
    /// {logDirectory}/dresscoder-{yyyy-MM-dd}.log. Intended for a no-installer app where writing
    /// next to the executable (e.g. a "logs" folder) is preferred over %AppData%.
    /// </summary>
    public static ILoggingBuilder AddFileLogging(
        this ILoggingBuilder builder, string logDirectory, LogLevel minimumLevel = LogLevel.Information)
    {
        builder.AddProvider(new FileLoggerProvider(logDirectory, minimumLevel));
        return builder;
    }
}
