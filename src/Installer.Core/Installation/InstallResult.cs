using System;

namespace Installer.Core.Installation;

public sealed class InstallResult
{
    private InstallResult(bool success, bool cancelled, TimeSpan duration, Exception? error, string? logFilePath)
    {
        Success = success;
        Cancelled = cancelled;
        Duration = duration;
        Error = error;
        LogFilePath = logFilePath;
    }

    public bool Success { get; }

    public bool Cancelled { get; }

    public TimeSpan Duration { get; }

    public Exception? Error { get; }

    public string? LogFilePath { get; }

    public static InstallResult Successful(TimeSpan duration, string? logFilePath)
        => new(true, false, duration, null, logFilePath);

    public static InstallResult CancelledResult(TimeSpan duration, string? logFilePath)
        => new(false, true, duration, null, logFilePath);

    public static InstallResult Failed(TimeSpan duration, Exception error, string? logFilePath)
        => new(false, false, duration, error, logFilePath);
}
