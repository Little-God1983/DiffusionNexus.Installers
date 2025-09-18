namespace Installer.Core.Logging;

public interface ILogSink
{
    void Log(LogLevel level, string message);
}
