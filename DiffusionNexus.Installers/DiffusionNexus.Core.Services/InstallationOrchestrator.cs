using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Implementation of the installation orchestrator.
/// </summary>
public class InstallationOrchestrator : IInstallationOrchestrator
{
    private readonly IGitService _gitService;
    private readonly IPythonService _pythonService;

    public InstallationOrchestrator(IGitService gitService, IPythonService pythonService)
    {
        ArgumentNullException.ThrowIfNull(gitService);
        ArgumentNullException.ThrowIfNull(pythonService);

        _gitService = gitService;
        _pythonService = pythonService;
    }

    /// <inheritdoc />
    public async Task<InstallationResult> InstallAsync(
        InstallationConfiguration configuration,
        string targetDirectory,
        IProgress<InstallLogEntry>? logProgress = null,
        IProgress<InstallationProgress>? stepProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        List<InstallationStep> steps;

        if (true)
        {
            // Model-only mode: skip repository cloning and environment setup
            steps = new()
            {
                InstallationStep.Update,
                InstallationStep.DownloadModels
            };
        }
        else
        {
            // Full installation mode
            steps = new()
            {
                InstallationStep.GitSetup,
                InstallationStep.PythonCheck,
                InstallationStep.CloneMainRepository,
                
            };

            if (configuration.Python.CreateVirtualEnvironment)
            {
                steps.Add(InstallationStep.CreateVirtualEnvironment);
            }  
        }

        if (configuration.GitRepositories?.Count > 0)
        {
            steps.Add(InstallationStep.CloneAdditionalRepositories);
        }
        if (configuration.ModelDownloads?.Count > 0)
        {
            steps.Add(InstallationStep.DownloadModels);
        }

        string? repositoryPath = null;
        string? venvPath = null;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            cancellationToken.ThrowIfCancellationRequested();

            stepProgress?.Report(new InstallationProgress
            {
                CurrentStep = step,
                StepIndex = i,
                TotalSteps = steps.Count,
                Message = GetStepDescription(step)
            });

            var result = step switch
            {
                InstallationStep.GitSetup => await EnsureGitInstalledAsync(logProgress, cancellationToken),
                InstallationStep.PythonCheck => await CheckPythonVersionAsync(
                    configuration.Python.PythonVersion,
                    configuration.Python.InterpreterPathOverride,
                    logProgress,
                    cancellationToken),
                InstallationStep.CloneMainRepository => await CloneMainRepositoryAsync(
                    configuration,
                    targetDirectory,
                    logProgress,
                    cancellationToken),
                InstallationStep.CreateVirtualEnvironment => await CreateVirtualEnvironmentAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.CloneAdditionalRepositories => await CloneAdditionalRepositoriesAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.InstallRequirements => InstallationStepResult.Skipped(step, "Step not implemented"),
                InstallationStep.InstallTorch => InstallationStepResult.Skipped(step, "Step not implemented"),
                InstallationStep.DownloadModels => await DownloadModelsAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    venvPath,
                    logProgress,
                    cancellationToken),
                InstallationStep.PostInstall => InstallationStepResult.Skipped(step, "Step not implemented"),
                InstallationStep.Update => await ValidateExistingInstallationAsync(
                    targetDirectory,
                    logProgress,
                    cancellationToken),
                _ => InstallationStepResult.Skipped(step, "Step not implemented")
            };

            // Track paths from successful steps
            if (result.IsSuccess)
            {
                if (step == InstallationStep.CloneMainRepository && result.Message.Contains("to "))
                {
                    repositoryPath = ExtractPathFromMessage(result.Message, targetDirectory, configuration);
                }
                else if (step == InstallationStep.CreateVirtualEnvironment && result.Message.Contains("at "))
                {
                    venvPath = ExtractVenvPathFromMessage(result.Message);
                }
            }

            if (!result.IsSuccess && !result.ShouldContinue)
            {
                logProgress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Error,
                    Message = $"Installation failed at step {step}: {result.Message}"
                });

                return InstallationResult.Failure($"Installation failed at step {step}: {result.Message}");
            }
        }

        // Report final progress
        stepProgress?.Report(new InstallationProgress
        {
            CurrentStep = InstallationStep.PostInstall,
            StepIndex = steps.Count,
            TotalSteps = steps.Count,
            Message = "Installation completed"
        });

        logProgress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Installation completed successfully!"
        });

        return InstallationResult.Success(
            "Installation completed successfully!",
            repositoryPath,
            venvPath);
    }

    private async Task<InstallationStepResult> DownloadModelsAsync(InstallationConfiguration configuration, string v, string? venvPath, IProgress<InstallLogEntry>? logProgress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task<InstallationStepResult> CloneAdditionalRepositoriesAsync(InstallationConfiguration configuration, string v, IProgress<InstallLogEntry>? logProgress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates that an existing installation exists at the target directory for model-only mode.
    /// </summary>
    private Task<InstallationStepResult> ValidateExistingInstallationAsync(
        string targetDirectory,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Validating existing installation at {targetDirectory}..."
        });

        if (!Directory.Exists(targetDirectory))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Target directory does not exist: {targetDirectory}"
            });

            return Task.FromResult(InstallationStepResult.Failure(
                InstallationStep.Update,
                $"Target directory does not exist: {targetDirectory}. Model-only mode requires an existing installation."));
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = $"Existing installation validated at {targetDirectory}"
        });

        return Task.FromResult(InstallationStepResult.Success(
            InstallationStep.Update,
            $"Existing installation validated at {targetDirectory}"));
    }

    /// <inheritdoc />
    public async Task<InstallationStepResult> EnsureGitInstalledAsync(
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Checking Git installation..."
        });

        if (_gitService.IsGitInstalled())
        {
            var version = await _gitService.GetGitVersionAsync(cancellationToken);
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = $"Git is installed: {version}"
            });
            return InstallationStepResult.Success(InstallationStep.GitSetup, $"Git is installed: {version}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Warning,
            Message = "Git not found. Attempting to install..."
        });

        var installResult = await _gitService.InstallGitAsync(progress, cancellationToken);

        if (!installResult.IsSuccess)
        {
            return InstallationStepResult.Failure(
                InstallationStep.GitSetup,
                $"Failed to install Git: {installResult.Message}");
        }

        return InstallationStepResult.Success(InstallationStep.GitSetup, installResult.Message);
    }

    /// <inheritdoc />
    public async Task<InstallationStepResult> CheckPythonVersionAsync(
        string requiredVersion,
        string? interpreterOverride = null,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Checking for Python {requiredVersion}..."
        });

        // Check interpreter override first
        if (!string.IsNullOrWhiteSpace(interpreterOverride))
        {
            if (File.Exists(interpreterOverride))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Success,
                    Message = $"Using specified interpreter: {interpreterOverride}"
                });
                return InstallationStepResult.Success(
                    InstallationStep.PythonCheck,
                    $"Using specified interpreter: {interpreterOverride}");
            }

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Specified interpreter not found: {interpreterOverride}. Searching for Python {requiredVersion}..."
            });
        }

        var installation = await _pythonService.FindPythonVersionAsync(requiredVersion, cancellationToken);

        if (installation is null)
        {
            // List available versions for better error message
            var availableVersions = await _pythonService.GetInstalledPythonVersionsAsync(cancellationToken);
            var versionList = availableVersions.Count > 0
                ? string.Join(", ", availableVersions)
                : "none found";

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Python {requiredVersion} not found. Available versions: {versionList}"
            });

            return InstallationStepResult.Failure(
                InstallationStep.PythonCheck,
                $"Python {requiredVersion} not found. Please install Python {requiredVersion} and try again. Available versions: {versionList}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = $"Found Python {installation.Version} at {installation.ExecutablePath}"
        });

        return InstallationStepResult.Success(
            InstallationStep.PythonCheck,
            $"Found Python {installation.Version} at {installation.ExecutablePath}");
    }

    /// <inheritdoc />
    public async Task<InstallationStepResult> CloneMainRepositoryAsync(
        InstallationConfiguration configuration,
        string targetDirectory,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var repoUrl = configuration.Repository.RepositoryUrl;
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            return InstallationStepResult.Failure(
                InstallationStep.CloneMainRepository,
                "Repository URL is not configured.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Cloning {repoUrl}..."
        });

        var cloneOptions = new GitCloneOptions
        {
            RepositoryUrl = repoUrl,
            TargetDirectory = targetDirectory,
            Branch = configuration.Repository.Branch,
            CommitHash = configuration.Repository.CommitHash
        };

        var result = await _gitService.CloneRepositoryAsync(cloneOptions, progress, cancellationToken);

        if (!result.IsSuccess)
        {
            return InstallationStepResult.Failure(
                InstallationStep.CloneMainRepository,
                $"Failed to clone repository: {result.Message}");
        }

        return InstallationStepResult.Success(
            InstallationStep.CloneMainRepository,
            $"Successfully cloned to {result.RepositoryPath}");
    }

    /// <inheritdoc />
    public async Task<InstallationStepResult> CreateVirtualEnvironmentAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.Python.CreateVirtualEnvironment)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "Virtual environment creation is disabled. Skipping..."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.CreateVirtualEnvironment,
                "Virtual environment creation is disabled.");
        }

        var venvOptions = new VirtualEnvironmentOptions
        {
            BaseDirectory = repositoryPath,
            Name = configuration.Python.VirtualEnvironmentName,
            RequiredPythonVersion = configuration.Python.PythonVersion,
            InterpreterPath = configuration.Python.InterpreterPathOverride,
            UpgradePip = true
        };

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Creating virtual environment '{venvOptions.Name}'..."
        });

        var result = await _pythonService.CreateVirtualEnvironmentAsync(venvOptions, progress, cancellationToken);

        if (!result.IsSuccess)
        {
            return InstallationStepResult.Failure(
                InstallationStep.CreateVirtualEnvironment,
                $"Failed to create virtual environment: {result.Message}");
        }

        return InstallationStepResult.Success(
            InstallationStep.CreateVirtualEnvironment,
            $"Virtual environment created at {result.VirtualEnvironmentPath}");
    }

    #region Private Helpers

    private static string GetStepDescription(InstallationStep step)
    {
        return step switch
        {
            InstallationStep.GitSetup => "Setting up Git...",
            InstallationStep.PythonCheck => "Checking Python installation...",
            InstallationStep.CloneMainRepository => "Cloning main repository...",
            InstallationStep.CreateVirtualEnvironment => "Creating virtual environment...",
            InstallationStep.CloneAdditionalRepositories => "Cloning additional repositories...",
            InstallationStep.InstallRequirements => "Installing requirements...",
            InstallationStep.InstallTorch => "Installing PyTorch...",
            InstallationStep.DownloadModels => "Downloading models...",
            InstallationStep.PostInstall => "Running post-installation tasks...",
            InstallationStep.Update => "Validating existing installation...",
            _ => "Processing..."
        };
    }

    private static string GetRepositoryName(string url)
    {
        var uri = url.TrimEnd('/');
        if (uri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri[..^4];
        }

        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 ? uri[(lastSlash + 1)..] : uri;
    }

    private static string? ExtractPathFromMessage(string message, string targetDirectory, InstallationConfiguration config)
    {
        // Try to extract path from "Successfully cloned to <path>" message
        var prefix = "Successfully cloned to ";
        if (message.StartsWith(prefix))
        {
            return message[prefix.Length..].Trim();
        }

        // Fall back to constructing the path
        return Path.Combine(targetDirectory, GetRepositoryName(config.Repository.RepositoryUrl));
    }

    private static string? ExtractVenvPathFromMessage(string message)
    {
        // Try to extract path from "Virtual environment created at <path>" message
        var prefix = "Virtual environment created at ";
        if (message.StartsWith(prefix))
        {
            return message[prefix.Length..].Trim();
        }

        var altPrefix = "at ";
        var atIndex = message.LastIndexOf(altPrefix, StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            return message[(atIndex + altPrefix.Length)..].Trim();
        }

        return null;
    }

    #endregion
}
