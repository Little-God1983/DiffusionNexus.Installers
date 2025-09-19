using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Installer.Core.Logging;

public sealed class BufferingLogSink : ILogSink
{
    private readonly ConcurrentQueue<LogMessage> _messages = new();

    public event EventHandler<LogMessage>? MessageLogged;

    public IReadOnlyCollection<LogMessage> Messages => _messages.ToArray();

    public void Log(LogLevel level, string message)
    {
        var logMessage = LogMessage.Create(level, message);
        _messages.Enqueue(logMessage);
        MessageLogged?.Invoke(this, logMessage);
    }

    public string AsText()
    {
        return string.Join(Environment.NewLine, _messages.ToArray());
    }
}
