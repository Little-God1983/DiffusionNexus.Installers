using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Implementation of process runner for executing external commands.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessRunOptions options, CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var result = await RunWithOutputAsync(
            options,
            line => stdout.AppendLine(line),
            line => stderr.AppendLine(line),
            cancellationToken);

        return result with
        {
            StandardOutput = stdout.ToString().TrimEnd(),
            StandardError = stderr.ToString().TrimEnd()
        };
    }

    /// <inheritdoc />
    public async Task<ProcessResult> RunWithOutputAsync(
        ProcessRunOptions options,
        Action<string>? onOutput = null,
        Action<string>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startInfo = new ProcessStartInfo
        {
            FileName = options.FileName,
            Arguments = options.Arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var outputComplete = new TaskCompletionSource<bool>();
        var errorComplete = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                outputComplete.TrySetResult(true);
                return;
            }

            stdout.AppendLine(e.Data);
            onOutput?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                errorComplete.TrySetResult(true);
                return;
            }

            stderr.AppendLine(e.Data);
            onError?.Invoke(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var linkedCts = options.Timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (linkedCts is not null && options.Timeout.HasValue)
            {
                linkedCts.CancelAfter(options.Timeout.Value);
            }

            var effectiveToken = linkedCts?.Token ?? cancellationToken;

            await process.WaitForExitAsync(effectiveToken);
            await Task.WhenAll(outputComplete.Task, errorComplete.Task);

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout.ToString().TrimEnd(),
                StandardError = stderr.ToString().TrimEnd()
            };
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
    }

    /// <inheritdoc />
    public bool IsExecutableInPath(string executableName)
    {
        return GetExecutablePath(executableName) is not null;
    }

    /// <inheritdoc />
    public string? GetExecutablePath(string executableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        // On Windows, check common extensions
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(directory, executableName + ext);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        // Also check if the executable name itself is a full path
        if (File.Exists(executableName))
        {
            return Path.GetFullPath(executableName);
        }

        return null;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore errors when killing process
        }
    }
}
