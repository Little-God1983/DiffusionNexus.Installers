using System;

namespace Installer.Core.Logging;

public readonly record struct LogMessage(DateTimeOffset Timestamp, LogLevel Level, string Message)
{
    public static LogMessage Create(LogLevel level, string message)
        => new(DateTimeOffset.Now, level, message);

    public override string ToString()
        => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level.ToString().ToUpperInvariant()}: {Message}";
}
