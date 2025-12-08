using System.Diagnostics;
using DiffusionNexus.Core.Models.Installation;
using Serilog;

namespace DiffusionNexus.Core.Models.Strategies;

/// <summary>
/// Base class with common functionality for installation strategies.
/// </summary>
public abstract class BaseInstallStrategy : IInstallStrategy
{
    public abstract string ApplicationName { get; }
    public abstract string Version { get; }

    public abstract Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress);

    public virtual async Task<bool> ValidatePrerequisitesAsync()
    {
        return await CheckPythonInstalled() && await CheckGitInstalled();
    }

    public virtual async Task<bool> UninstallAsync(string installPath)
    {
        try
        {
            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath, true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to uninstall {App}", ApplicationName);
            return false;
        }
    }

    protected async Task<bool> CheckPythonInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    protected async Task<bool> CheckGitInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    protected async Task<bool> RunCommandAsync(string fileName, string arguments, string? workingDirectory = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    protected void ReportProgress(IProgress<InstallProgress>? progress, int percent, string step, string? details = null)
    {
        progress?.Report(new InstallProgress
        {
            PercentComplete = percent,
            CurrentStep = step,
            Details = details
        });
    }
}
