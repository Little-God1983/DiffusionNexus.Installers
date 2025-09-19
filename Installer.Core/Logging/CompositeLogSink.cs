namespace AIKnowledge2Go.Installers.Core.Logging;

public sealed class CompositeLogSink : ILogSink
{
    private readonly IReadOnlyList<ILogSink> _sinks;

    public CompositeLogSink(IEnumerable<ILogSink> sinks)
    {
        _sinks = sinks?.ToArray() ?? Array.Empty<ILogSink>();
    }

    public void Write(LogMessage message)
    {
        foreach (var sink in _sinks)
        {
            sink.Write(message);
        }
    }
}
