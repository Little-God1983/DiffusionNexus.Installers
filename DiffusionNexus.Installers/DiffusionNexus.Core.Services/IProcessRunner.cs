using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Result of a process execution.
/// </summary>
public record ProcessResult
{
    /// <summary>
    /// The exit code of the process.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// The standard output of the process.
    /// </summary>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// The standard error output of the process.
    /// </summary>
    public required string StandardError { get; init; }

    /// <summary>
    /// Indicates whether the process completed successfully (exit code 0).
    /// </summary>
    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Options for running a process.
/// </summary>
public record ProcessRunOptions
{
    /// <summary>
    /// The executable or command to run.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Arguments to pass to the executable.
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// Working directory for the process. If null, uses current directory.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Timeout for the process execution. Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Abstraction for running external processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process and returns the result.
    /// </summary>
    /// <param name="options">Options for running the process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the process execution.</returns>
    Task<ProcessResult> RunAsync(ProcessRunOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a process with real-time output streaming.
    /// </summary>
    /// <param name="options">Options for running the process.</param>
    /// <param name="onOutput">Callback for standard output lines.</param>
    /// <param name="onError">Callback for standard error lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the process execution.</returns>
    Task<ProcessResult> RunWithOutputAsync(
        ProcessRunOptions options,
        Action<string>? onOutput = null,
        Action<string>? onError = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an executable exists in the system PATH.
    /// </summary>
    /// <param name="executableName">Name of the executable to find.</param>
    /// <returns>True if the executable is found, false otherwise.</returns>
    bool IsExecutableInPath(string executableName);

    /// <summary>
    /// Gets the full path to an executable in the system PATH.
    /// </summary>
    /// <param name="executableName">Name of the executable to find.</param>
    /// <returns>Full path to the executable, or null if not found.</returns>
    string? GetExecutablePath(string executableName);
}
