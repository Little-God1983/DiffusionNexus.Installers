using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models;
using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Implementation of Python operations service.
/// </summary>
public partial class PythonService : IPythonService
{
    private readonly IProcessRunner _processRunner;

    public PythonService(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PythonInstallation>> GetInstalledPythonVersionsAsync(CancellationToken cancellationToken = default)
    {
        var installations = new List<PythonInstallation>();

        // Try py launcher on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await AddPyLauncherVersionsAsync(installations, cancellationToken);
        }

        // Try common python executables
        await AddPythonExecutableVersionsAsync(installations, cancellationToken);

        // Try common installation directories on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await AddWindowsInstallationVersionsAsync(installations, cancellationToken);
        }

        // Remove duplicates based on executable path
        return installations
            .GroupBy(p => p.ExecutablePath.ToLowerInvariant())
            .Select(g => g.First())
            .OrderByDescending(p => p.Version)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PythonInstallation?> FindPythonVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var normalizedVersion = NormalizePythonVersion(version);
        var installations = await GetInstalledPythonVersionsAsync(cancellationToken);

        return installations.FirstOrDefault(p => 
            p.MajorMinorVersion.Equals(normalizedVersion, StringComparison.OrdinalIgnoreCase) ||
            p.Version.StartsWith(normalizedVersion, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<string?> GetPythonExecutableAsync(
        string version,
        string? interpreterOverride = null,
        CancellationToken cancellationToken = default)
    {
        // If an override is specified and exists, use it
        if (!string.IsNullOrWhiteSpace(interpreterOverride) && File.Exists(interpreterOverride))
        {
            return interpreterOverride;
        }

        var installation = await FindPythonVersionAsync(version, cancellationToken);
        return installation?.ExecutablePath;
    }

    /// <inheritdoc />
    public async Task<PythonOperationResult> CreateVirtualEnvironmentAsync(
        VirtualEnvironmentOptions options,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var venvPath = Path.Combine(options.BaseDirectory, options.Name);

        // Check if venv already exists
        if (Directory.Exists(venvPath))
        {
            var pythonExe = GetVenvPythonExecutable(venvPath);
            if (File.Exists(pythonExe))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Info,
                    Message = $"Virtual environment already exists at {venvPath}"
                });
                return PythonOperationResult.Success(
                    $"Virtual environment already exists at {venvPath}",
                    venvPath,
                    pythonExe);
            }
        }

        // Find the Python executable
        var pythonPath = await GetPythonExecutableAsync(
            options.RequiredPythonVersion,
            options.InterpreterPath,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Python {options.RequiredPythonVersion} not found on the system."
            });
            return PythonOperationResult.Failure($"Python {options.RequiredPythonVersion} not found.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Creating virtual environment using {pythonPath}..."
        });

        // Ensure base directory exists
        Directory.CreateDirectory(options.BaseDirectory);

        // Create virtual environment
        var result = await _processRunner.RunWithOutputAsync(
            new ProcessRunOptions
            {
                FileName = pythonPath,
                Arguments = $"-m venv \"{venvPath}\"",
                WorkingDirectory = options.BaseDirectory,
                Timeout = TimeSpan.FromMinutes(5)
            },
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Info, Message = line }),
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = line }),
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to create virtual environment: {result.StandardError}"
            });
            return PythonOperationResult.Failure($"Failed to create virtual environment: {result.StandardError}");
        }

        var venvPython = GetVenvPythonExecutable(venvPath);

        if (!File.Exists(venvPython))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = "Virtual environment created but Python executable not found."
            });
            return PythonOperationResult.Failure("Virtual environment created but Python executable not found.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = $"Virtual environment created at {venvPath}"
        });

        // Upgrade pip if requested
        if (options.UpgradePip)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "Upgrading pip..."
            });

            var pipUpgrade = await _processRunner.RunWithOutputAsync(
                new ProcessRunOptions
                {
                    FileName = venvPython,
                    Arguments = "-m pip install --upgrade pip",
                    WorkingDirectory = venvPath,
                    Timeout = TimeSpan.FromMinutes(5)
                },
                line => progress?.Report(new InstallLogEntry { Level = LogLevel.Info, Message = line }),
                line => progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = line }),
                cancellationToken);

            if (!pipUpgrade.IsSuccess)
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Warning,
                    Message = "Failed to upgrade pip, continuing anyway..."
                });
            }
            else
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Success,
                    Message = "Pip upgraded successfully"
                });
            }
        }

        return PythonOperationResult.Success(
            $"Virtual environment created at {venvPath}",
            venvPath,
            venvPython);
    }

    /// <inheritdoc />
    public string GetVenvPythonExecutable(string venvPath)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python");
    }

    /// <inheritdoc />
    public string GetVenvPipExecutable(string venvPath)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(venvPath, "Scripts", "pip.exe")
            : Path.Combine(venvPath, "bin", "pip");
    }

    /// <inheritdoc />
    public async Task<PythonOperationResult> InstallPackagesAsync(
        string pipExecutable,
        string[] packages,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipExecutable);
        ArgumentNullException.ThrowIfNull(packages);

        if (packages.Length == 0)
        {
            return PythonOperationResult.Success("No packages to install.");
        }

        var packagesArg = string.Join(" ", packages.Select(p => $"\"{p}\""));

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Installing packages: {string.Join(", ", packages)}"
        });

        var result = await _processRunner.RunWithOutputAsync(
            new ProcessRunOptions
            {
                FileName = pipExecutable,
                Arguments = $"install {packagesArg}",
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
                Message = $"Failed to install packages: {result.StandardError}"
            });
            return PythonOperationResult.Failure($"Failed to install packages: {result.StandardError}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Packages installed successfully"
        });

        return PythonOperationResult.Success("Packages installed successfully.");
    }

    /// <inheritdoc />
    public async Task<PythonOperationResult> InstallRequirementsAsync(
        string pipExecutable,
        string requirementsPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(requirementsPath);

        if (!File.Exists(requirementsPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Requirements file not found: {requirementsPath}"
            });
            return PythonOperationResult.Failure($"Requirements file not found: {requirementsPath}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Installing requirements from {requirementsPath}..."
        });

        var result = await _processRunner.RunWithOutputAsync(
            new ProcessRunOptions
            {
                FileName = pipExecutable,
                Arguments = $"install -r \"{requirementsPath}\"",
                Timeout = TimeSpan.FromMinutes(60)
            },
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Info, Message = line }),
            line => progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = line }),
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to install requirements: {result.StandardError}"
            });
            return PythonOperationResult.Failure($"Failed to install requirements: {result.StandardError}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Requirements installed successfully"
        });

        return PythonOperationResult.Success("Requirements installed successfully.");
    }

    #region Private Helper Methods

    private async Task AddPyLauncherVersionsAsync(List<PythonInstallation> installations, CancellationToken cancellationToken)
    {
        if (!_processRunner.IsExecutableInPath("py"))
        {
            return;
        }

        try
        {
            var result = await _processRunner.RunAsync(
                new ProcessRunOptions { FileName = "py", Arguments = "-0p" },
                cancellationToken);

            if (!result.IsSuccess)
            {
                return;
            }

            // Parse py launcher output
            // Format: "-V:X.Y    Path\to\python.exe"
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = PyLauncherOutputRegex().Match(line.Trim());
                if (match.Success)
                {
                    var version = match.Groups[1].Value;
                    var path = match.Groups[2].Value.Trim();

                    if (File.Exists(path))
                    {
                        var fullVersion = await GetPythonVersionFromExecutableAsync(path, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(fullVersion))
                        {
                            installations.Add(new PythonInstallation
                            {
                                ExecutablePath = path,
                                Version = fullVersion,
                                IsVirtualEnvironment = false
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors when checking py launcher
        }
    }

    private async Task AddPythonExecutableVersionsAsync(List<PythonInstallation> installations, CancellationToken cancellationToken)
    {
        var executables = new[] { "python3", "python" };

        foreach (var exe in executables)
        {
            var path = _processRunner.GetExecutablePath(exe);
            if (path is null)
            {
                continue;
            }

            try
            {
                var version = await GetPythonVersionFromExecutableAsync(path, cancellationToken);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    installations.Add(new PythonInstallation
                    {
                        ExecutablePath = path,
                        Version = version,
                        IsVirtualEnvironment = false
                    });
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    private async Task AddWindowsInstallationVersionsAsync(List<PythonInstallation> installations, CancellationToken cancellationToken)
    {
        var searchPaths = new List<string>();

        // Add common Python installation paths
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        searchPaths.Add(Path.Combine(localAppData, "Programs", "Python"));
        searchPaths.Add(Path.Combine(programFiles, "Python"));
        searchPaths.Add(programFilesX86);
        searchPaths.Add(userProfile);

        foreach (var basePath in searchPaths.Where(Directory.Exists))
        {
            try
            {
                var pythonDirs = Directory.GetDirectories(basePath, "Python*", SearchOption.TopDirectoryOnly);
                foreach (var dir in pythonDirs)
                {
                    var pythonExe = Path.Combine(dir, "python.exe");
                    if (File.Exists(pythonExe))
                    {
                        var version = await GetPythonVersionFromExecutableAsync(pythonExe, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(version))
                        {
                            installations.Add(new PythonInstallation
                            {
                                ExecutablePath = pythonExe,
                                Version = version,
                                IsVirtualEnvironment = false
                            });
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when scanning directories
            }
        }
    }

    private async Task<string?> GetPythonVersionFromExecutableAsync(string pythonPath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                new ProcessRunOptions
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    Timeout = TimeSpan.FromSeconds(10)
                },
                cancellationToken);

            if (!result.IsSuccess)
            {
                return null;
            }

            // Parse "Python X.Y.Z" output
            var output = result.StandardOutput.Trim();
            if (output.StartsWith("Python ", StringComparison.OrdinalIgnoreCase))
            {
                return output[7..].Trim();
            }

            return output;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePythonVersion(string version)
    {
        // Normalize version like "3.12" or "3.12.0" to just "3.12"
        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    [GeneratedRegex(@"^\s*-V:(\d+\.\d+)\S*\s+(.+)$")]
    private static partial Regex PyLauncherOutputRegex();

    #endregion
}
