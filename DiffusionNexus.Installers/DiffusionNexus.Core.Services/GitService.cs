using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Implementation of Git operations service.
/// </summary>
public class GitService : IGitService
{
    private const string GitForWindowsDownloadUrl = "https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.2/Git-2.47.1.2-64-bit.exe";
    private const string GitExecutable = "git";

    private readonly IProcessRunner _processRunner;

    public GitService(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public bool IsGitInstalled()
    {
        return _processRunner.IsExecutableInPath(GitExecutable);
    }

    /// <inheritdoc />
    public async Task<string?> GetGitVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsGitInstalled())
        {
            return null;
        }

        try
        {
            var result = await _processRunner.RunAsync(
                new ProcessRunOptions { FileName = GitExecutable, Arguments = "--version" },
                cancellationToken);

            return result.IsSuccess ? result.StandardOutput.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GitOperationResult> InstallGitAsync(
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GitOperationResult.Failure(
                "Automatic Git installation is only supported on Windows. " +
                "Please install Git manually using your package manager.");
        }

        if (IsGitInstalled())
        {
            var version = await GetGitVersionAsync(cancellationToken);
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"Git is already installed: {version}"
            });
            return GitOperationResult.Success($"Git is already installed: {version}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Git not found. Downloading Git for Windows..."
        });

        var tempDir = Path.Combine(Path.GetTempPath(), "DiffusionNexusInstaller");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, "GitInstaller.exe");

        try
        {
            // Download Git installer
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "Downloading Git installer..."
            });

            var response = await httpClient.GetAsync(GitForWindowsDownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var fs = File.Create(installerPath))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "Download complete. Running installer..."
            });

            // Run silent installation
            var installResult = await _processRunner.RunAsync(
                new ProcessRunOptions
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /NORESTART /NOCANCEL /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /COMPONENTS=\"icons,ext\\reg\\shellhere,assoc,assoc_sh\"",
                    Timeout = TimeSpan.FromMinutes(10)
                },
                cancellationToken);

            if (!installResult.IsSuccess)
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Error,
                    Message = $"Git installation failed with exit code {installResult.ExitCode}"
                });
                return GitOperationResult.Failure($"Git installation failed: {installResult.StandardError}");
            }

            // Refresh PATH to find newly installed Git
            RefreshEnvironmentPath();

            // Verify installation
            if (!IsGitInstalled())
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Warning,
                    Message = "Git installed but not found in PATH. You may need to restart the application."
                });
                return GitOperationResult.Failure("Git installed but not found in PATH. Please restart the application.");
            }

            var installedVersion = await GetGitVersionAsync(cancellationToken);
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = $"Git installed successfully: {installedVersion}"
            });

            return GitOperationResult.Success($"Git installed successfully: {installedVersion}");
        }
        catch (HttpRequestException ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to download Git: {ex.Message}"
            });
            return GitOperationResult.Failure($"Failed to download Git: {ex.Message}");
        }
        catch (Exception ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Git installation error: {ex.Message}"
            });
            return GitOperationResult.Failure($"Git installation error: {ex.Message}");
        }
        finally
        {
            // Cleanup installer
            try
            {
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc />
    public async Task<GitOperationResult> CloneRepositoryAsync(
        GitCloneOptions options,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsGitInstalled())
        {
            return GitOperationResult.Failure("Git is not installed. Please install Git first.");
        }

        // Determine the target folder name
        var folderName = options.FolderName ?? GetRepositoryNameFromUrl(options.RepositoryUrl);
        var targetPath = Path.Combine(options.TargetDirectory, folderName);

        // Check if directory already exists
        if (Directory.Exists(targetPath))
        {
            if (IsGitRepository(targetPath))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Info,
                    Message = $"Repository already exists at {targetPath}. Skipping clone."
                });
                return GitOperationResult.Success($"Repository already exists at {targetPath}", targetPath);
            }

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Directory {targetPath} exists but is not a Git repository."
            });
            return GitOperationResult.Failure($"Directory {targetPath} exists but is not a Git repository.");
        }

        // Ensure parent directory exists
        Directory.CreateDirectory(options.TargetDirectory);

        // Build clone command
        var args = BuildCloneArguments(options, folderName);

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Cloning {options.RepositoryUrl} to {targetPath}..."
        });

        var result = await _processRunner.RunWithOutputAsync(
            new ProcessRunOptions
            {
                FileName = GitExecutable,
                Arguments = args,
                WorkingDirectory = options.TargetDirectory,
                Timeout = TimeSpan.FromMinutes(30)
            },
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Info, Message = line }),
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = line }),
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Clone failed with exit code {result.ExitCode}"
            });
            return GitOperationResult.Failure($"Clone failed: {result.StandardError}");
        }

        // Checkout specific commit if specified
        if (!string.IsNullOrWhiteSpace(options.CommitHash))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"Checking out commit {options.CommitHash}..."
            });

            var checkoutResult = await _processRunner.RunAsync(
                new ProcessRunOptions
                {
                    FileName = GitExecutable,
                    Arguments = $"checkout {options.CommitHash}",
                    WorkingDirectory = targetPath
                },
                cancellationToken);

            if (!checkoutResult.IsSuccess)
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Warning,
                    Message = $"Failed to checkout commit {options.CommitHash}: {checkoutResult.StandardError}"
                });
            }
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = $"Successfully cloned repository to {targetPath}"
        });

        return GitOperationResult.Success($"Successfully cloned to {targetPath}", targetPath);
    }

    /// <inheritdoc />
    public bool IsGitRepository(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(directory, ".git"));
    }

    /// <inheritdoc />
    public async Task<GitOperationResult> PullAsync(
        string repositoryPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsGitRepository(repositoryPath))
        {
            return GitOperationResult.Failure($"{repositoryPath} is not a Git repository.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Pulling latest changes in {repositoryPath}..."
        });

        var result = await _processRunner.RunWithOutputAsync(
            new ProcessRunOptions
            {
                FileName = GitExecutable,
                Arguments = "pull",
                WorkingDirectory = repositoryPath,
                Timeout = TimeSpan.FromMinutes(10)
            },
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Info, Message = line }),
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = line }),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return GitOperationResult.Failure($"Pull failed: {result.StandardError}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Pull completed successfully"
        });

        return GitOperationResult.Success("Pull completed successfully", repositoryPath);
    }

    private static string BuildCloneArguments(GitCloneOptions options, string folderName)
    {
        var args = "clone";

        if (options.ShallowClone)
        {
            args += " --depth 1";
        }

        if (!string.IsNullOrWhiteSpace(options.Branch))
        {
            args += $" --branch {options.Branch}";
        }

        args += $" \"{options.RepositoryUrl}\" \"{folderName}\"";

        return args;
    }

    private static string GetRepositoryNameFromUrl(string url)
    {
        // Extract repository name from URL
        // e.g., https://github.com/user/repo.git -> repo
        var uri = url.TrimEnd('/');
        
        if (uri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri[..^4];
        }

        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 ? uri[(lastSlash + 1)..] : uri;
    }

    private static void RefreshEnvironmentPath()
    {
        // Try to refresh the PATH environment variable from the registry
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            var newPath = $"{machinePath};{userPath}";
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
        }
        catch
        {
            // Ignore errors when refreshing PATH
        }
    }
}
