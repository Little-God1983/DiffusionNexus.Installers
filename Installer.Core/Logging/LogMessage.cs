namespace AIKnowledge2Go.Installers.Core.Logging;

public sealed record LogMessage(LogLevel Level, string Message, DateTimeOffset Timestamp, Exception? Exception = null)
{
    public override string ToString()
    {
        var exceptionSuffix = Exception is null ? string.Empty : $"{Environment.NewLine}{Exception}";
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level.ToString().ToUpperInvariant(),-11} {Message}{exceptionSuffix}";
    }
}
