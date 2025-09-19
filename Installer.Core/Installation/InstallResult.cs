namespace AIKnowledge2Go.Installers.Core.Installation;

public sealed record InstallResult(bool Success, string? ErrorMessage = null, Exception? Exception = null)
{
    public static InstallResult Completed() => new(true);

    public static InstallResult Failed(string message, Exception? exception = null) => new(false, message, exception);
}
