using System.Globalization;

namespace Installer.Core.Logging;

public static class LogSinkExtensions
{
    public static void Info(this ILogSink sink, string message) => sink.Log(LogLevel.Info, message);

    public static void Warn(this ILogSink sink, string message) => sink.Log(LogLevel.Warning, message);

    public static void Error(this ILogSink sink, string message) => sink.Log(LogLevel.Error, message);

    public static void Verbose(this ILogSink sink, string message) => sink.Log(LogLevel.Verbose, message);

    public static void Info(this ILogSink sink, string messageFormat, params object[] args)
        => sink.Log(LogLevel.Info, string.Format(CultureInfo.InvariantCulture, messageFormat, args));

    public static void Warn(this ILogSink sink, string messageFormat, params object[] args)
        => sink.Log(LogLevel.Warning, string.Format(CultureInfo.InvariantCulture, messageFormat, args));

    public static void Error(this ILogSink sink, string messageFormat, params object[] args)
        => sink.Log(LogLevel.Error, string.Format(CultureInfo.InvariantCulture, messageFormat, args));

    public static void Verbose(this ILogSink sink, string messageFormat, params object[] args)
        => sink.Log(LogLevel.Verbose, string.Format(CultureInfo.InvariantCulture, messageFormat, args));
}
