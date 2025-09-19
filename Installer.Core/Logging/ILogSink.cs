namespace AIKnowledge2Go.Installers.Core.Logging;

public interface ILogSink
{
    void Write(LogMessage message);
}
