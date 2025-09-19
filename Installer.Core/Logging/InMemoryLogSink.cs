using System.Collections.Concurrent;
using System.Text;

namespace AIKnowledge2Go.Installers.Core.Logging;

public sealed class InMemoryLogSink : ILogSink
{
    private readonly ConcurrentQueue<LogMessage> _messages = new();

    public event EventHandler<LogMessage>? MessageLogged;

    public void Write(LogMessage message)
    {
        _messages.Enqueue(message);
        MessageLogged?.Invoke(this, message);
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<LogMessage> Snapshot()
    {
        return _messages.ToArray();
    }

    public string BuildText()
    {
        var builder = new StringBuilder();
        foreach (var message in _messages)
        {
            builder.AppendLine(message.ToString());
        }

        return builder.ToString();
    }
}
