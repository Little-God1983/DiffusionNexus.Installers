using System;
using System.IO;

namespace Installer.Core.Logging;

public sealed class FileLogSink : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;

    public FileLogSink(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public string FilePath => (_writer.BaseStream as FileStream)?.Name ?? string.Empty;

    public void Log(LogLevel level, string message)
    {
        var formatted = LogMessage.Create(level, message).ToString();
        _writer.WriteLine(formatted);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
