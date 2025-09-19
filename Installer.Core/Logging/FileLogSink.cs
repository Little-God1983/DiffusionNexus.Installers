namespace AIKnowledge2Go.Installers.Core.Logging;

public sealed class FileLogSink : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;
    private bool _isDisposed;

    public FileLogSink(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
            NewLine = Environment.NewLine,
        };
    }

    public void Write(LogMessage message)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(FileLogSink));
        }

        _writer.WriteLine(message.ToString());
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _writer.Dispose();
    }
}
