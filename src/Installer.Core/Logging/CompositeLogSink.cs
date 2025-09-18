using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Installer.Core.Logging;

public sealed class CompositeLogSink : ILogSink
{
    private ImmutableArray<ILogSink> _sinks;

    public CompositeLogSink(IEnumerable<ILogSink> sinks)
    {
        _sinks = ImmutableArray.CreateRange(sinks);
    }

    public CompositeLogSink(params ILogSink[] sinks)
    {
        _sinks = ImmutableArray.CreateRange(sinks);
    }

    public void Log(LogLevel level, string message)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Log(level, message);
            }
            catch (Exception)
            {
                // Ignore logging failures to avoid crashing the installer.
            }
        }
    }

    public CompositeLogSink With(ILogSink sink)
    {
        _sinks = _sinks.Add(sink);
        return this;
    }
}
