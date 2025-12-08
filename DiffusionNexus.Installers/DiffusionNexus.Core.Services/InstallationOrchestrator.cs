using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    private readonly HttpClient _httpClient;

    public InstallationOrchestrator(IGitService gitService, IPythonService pythonService)
        : this(gitService, pythonService, new HttpClient())
    {
    }

    public InstallationOrchestrator(IGitService gitService, IPythonService pythonService, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(gitService);
        ArgumentNullException.ThrowIfNull(pythonService);
        ArgumentNullException.ThrowIfNull(httpClient);

        _gitService = gitService;
        _pythonService = pythonService;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<InstallationResult> InstallAsync(
        InstallationConfiguration configuration,
        string targetDirectory,
        InstallationOptions options,
        IProgress<InstallLogEntry>? logProgress = null,
        IProgress<InstallationProgress>? stepProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        List<InstallationStep> steps;

        if (options.OnlyModelDownload)
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
                InstallationStep.CloneMainRepository
            };

            if (configuration.Python.CreateVirtualEnvironment)
            {
                steps.Add(InstallationStep.CreateVirtualEnvironment);
            }
            if (configuration.GitRepositories?.Count > 0)
            {
                steps.Add(InstallationStep.CloneAdditionalRepositories);
            }
            if (configuration.ModelDownloads?.Count > 0)
            {
                steps.Add(InstallationStep.DownloadModels);
            }
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

    private async Task<InstallationStepResult> DownloadModelsAsync(InstallationConfiguration configuration, string repositoryPath, string? venvPath, IProgress<InstallLogEntry>? logProgress, CancellationToken cancellationToken)
    {
        var modelDownloads = configuration.ModelDownloads;
        if (modelDownloads is null || modelDownloads.Count == 0)
        {
            logProgress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "No models configured for download."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.DownloadModels,
                "No models configured.");
        }

        // Filter to only enabled models
        var enabledModels = modelDownloads.Where(m => m.Enabled).ToList();
        if (enabledModels.Count == 0)
        {
            logProgress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "All configured models are disabled."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.DownloadModels,
                "All models are disabled.");
        }

        logProgress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Downloading {enabledModels.Count} models..."
        });

        var successCount = 0;
        var failCount = 0;
        var skippedCount = 0;

        foreach (var model in enabledModels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logProgress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"Processing model: {model.Name}"
            });

            // Process download links for this model
            var enabledLinks = model.DownloadLinks.Where(link => link.Enabled).ToList();

            if (enabledLinks.Count == 0)
            {
                // Fall back to the model's direct URL if no download links are configured
                if (!string.IsNullOrWhiteSpace(model.Url))
                {
                    var result = await DownloadSingleFileAsync(
                        model.Url,
                        ResolveModelDestination(configuration, model, repositoryPath),
                        model.Name,
                        logProgress,
                        cancellationToken);

                    if (result) successCount++;
                    else failCount++;
                }
                else
                {
                    logProgress?.Report(new InstallLogEntry
                    {
                        Level = LogLevel.Warning,
                        Message = $"No download links configured for {model.Name}"
                    });
                    skippedCount++;
                }
                continue;
            }

            // Download each enabled link
            foreach (var link in enabledLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destination = !string.IsNullOrWhiteSpace(link.Destination)
                    ? ResolvePath(link.Destination, repositoryPath, configuration)
                    : ResolveModelDestination(configuration, model, repositoryPath);

                var result = await DownloadSingleFileAsync(
                    link.Url,
                    destination,
                    model.Name,
                    logProgress,
                    cancellationToken);

                if (result) successCount++;
                else failCount++;
            }
        }

        if (failCount > 0 && successCount == 0 && skippedCount == 0)
        {
            return InstallationStepResult.Failure(
                InstallationStep.DownloadModels,
                $"Failed to download all {failCount} models.",
                shouldContinue: true);
        }

        var message = failCount > 0
            ? $"Downloaded {successCount} models, {failCount} failed, {skippedCount} skipped."
            : $"Successfully downloaded {successCount} models.";

        logProgress?.Report(new InstallLogEntry
        {
            Level = failCount > 0 ? LogLevel.Warning : LogLevel.Success,
            Message = message
        });

        return InstallationStepResult.Success(
            InstallationStep.DownloadModels,
            message);
    }

    /// <summary>
    /// Downloads a single file from a URL to a destination path.
    /// </summary>
    private async Task<bool> DownloadSingleFileAsync(
        string url,
        string destinationDirectory,
        string modelName,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract filename from URL
            var fileName = GetFileNameFromUrl(url);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{modelName}_{Guid.NewGuid():N}.bin";
            }

            var destinationPath = Path.Combine(destinationDirectory, fileName);

            // Check if file already exists
            if (File.Exists(destinationPath))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Info,
                    Message = $"File already exists: {fileName}, skipping download."
                });
                return true;
            }

            // Ensure destination directory exists
            Directory.CreateDirectory(destinationDirectory);

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"Downloading {fileName}..."
            });

            // Log the exact command being executed (verbose)
            progress?.Report(InstallLogEntry.ForCommand($"HTTP GET {url} -> {destinationPath}"));

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(destinationPath);

            var buffer = new byte[81920]; // 80KB buffer
            long bytesDownloaded = 0;
            int bytesRead;
            var lastProgressReport = DateTime.UtcNow;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesDownloaded += bytesRead;

                // Report progress every 2 seconds
                if (DateTime.UtcNow - lastProgressReport > TimeSpan.FromSeconds(2))
                {
                    var progressPercent = totalBytes.HasValue && totalBytes > 0
                        ? (double)bytesDownloaded / totalBytes.Value * 100
                        : 0;
                    var downloadedMB = bytesDownloaded / (1024.0 * 1024.0);
                    var totalMB = totalBytes.HasValue ? totalBytes.Value / (1024.0 * 1024.0) : 0;

                    progress?.Report(new InstallLogEntry
                    {
                        Level = LogLevel.Info,
                        Message = totalBytes.HasValue
                            ? $"Downloading {fileName}: {downloadedMB:F1} MB / {totalMB:F1} MB ({progressPercent:F0}%)"
                            : $"Downloading {fileName}: {downloadedMB:F1} MB"
                    });

                    lastProgressReport = DateTime.UtcNow;
                }
            }

            var finalSizeMB = bytesDownloaded / (1024.0 * 1024.0);
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = $"Downloaded {fileName} ({finalSizeMB:F1} MB)"
            });

            return true;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Download failed for {modelName}: {ex.Message}"
            });
            return false;
        }
        catch (IOException ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to save {modelName}: {ex.Message}"
            });
            return false;
        }
    }

    /// <summary>
    /// Resolves the destination path for a model download.
    /// </summary>
    private static string ResolveModelDestination(
        InstallationConfiguration configuration,
        Models.Entities.ModelDownload model,
        string repositoryPath)
    {
        // If model has explicit destination, use it
        if (!string.IsNullOrWhiteSpace(model.Destination))
        {
            return ResolvePath(model.Destination, repositoryPath, configuration);
        }

        // Use default model download directory from configuration
        var baseDirectory = configuration.Paths.DefaultModelDownloadDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            // Default to ComfyUI's models directory
            baseDirectory = Path.Combine(repositoryPath, "models", "checkpoints");
        }
        else if (!Path.IsPathRooted(baseDirectory))
        {
            baseDirectory = Path.Combine(repositoryPath, baseDirectory);
        }

        return baseDirectory;
    }

    /// <summary>
    /// Resolves a path, making it absolute if needed.
    /// </summary>
    private static string ResolvePath(string path, string repositoryPath, InstallationConfiguration configuration)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Try relative to repository first
        return Path.Combine(repositoryPath, path);
    }

    /// <summary>
    /// Extracts the filename from a URL.
    /// </summary>
    private static string GetFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);

            // Handle URLs with query strings that might have the filename
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
            {
                // Try to get filename from query parameter if present
                var query = uri.Query;
                if (query.Contains("filename=", StringComparison.OrdinalIgnoreCase))
                {
                    var start = query.IndexOf("filename=", StringComparison.OrdinalIgnoreCase) + 9;
                    var end = query.IndexOf('&', start);
                    fileName = end > start
                        ? query[start..end]
                        : query[start..];
                    fileName = Uri.UnescapeDataString(fileName);
                }
            }

            return fileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<InstallationStepResult> CloneAdditionalRepositoriesAsync(InstallationConfiguration configuration, string repositoryPath, IProgress<InstallLogEntry>? logProgress, CancellationToken cancellationToken)
    {
        var repositories = configuration.GitRepositories;
        if (repositories is null || repositories.Count == 0)
        {
            logProgress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "No additional repositories to clone."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.CloneAdditionalRepositories,
                "No additional repositories configured.");
        }

        logProgress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Cloning {repositories.Count} additional repositories..."
        });

        // Determine the custom_nodes directory (ComfyUI convention)
        var customNodesPath = Path.Combine(repositoryPath, "custom_nodes");
        Directory.CreateDirectory(customNodesPath);

        var successCount = 0;
        var failCount = 0;

        foreach (var repo in repositories.OrderBy(r => r.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var repoName = !string.IsNullOrWhiteSpace(repo.Name) ? repo.Name : GetRepositoryName(repo.Url);

            logProgress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"[{repo.Priority}] Cloning {repoName}..."
            });

            var cloneOptions = new GitCloneOptions
            {
                RepositoryUrl = repo.Url,
                TargetDirectory = customNodesPath,
                ShallowClone = true
            };

            var cloneResult = await _gitService.CloneRepositoryAsync(cloneOptions, logProgress, cancellationToken);

            if (!cloneResult.IsSuccess)
            {
                logProgress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Warning,
                    Message = $"Failed to clone {repoName}: {cloneResult.Message}"
                });
                failCount++;
                continue;
            }

            successCount++;

            // Install requirements if configured
            if (repo.InstallRequirements && !string.IsNullOrWhiteSpace(cloneResult.RepositoryPath))
            {
                await InstallRepositoryRequirementsAsync(
                    cloneResult.RepositoryPath,
                    repositoryPath,
                    repoName,
                    logProgress,
                    cancellationToken);
            }
        }

        if (failCount > 0 && successCount == 0)
        {
            return InstallationStepResult.Failure(
                InstallationStep.CloneAdditionalRepositories,
                $"Failed to clone all {failCount} repositories.");
        }

        var message = failCount > 0
            ? $"Cloned {successCount} repositories, {failCount} failed."
            : $"Successfully cloned {successCount} repositories.";

        logProgress?.Report(new InstallLogEntry
        {
            Level = failCount > 0 ? LogLevel.Warning : LogLevel.Success,
            Message = message
        });

        return InstallationStepResult.Success(
            InstallationStep.CloneAdditionalRepositories,
            message);
    }

    /// <summary>
    /// Installs Python requirements for a cloned repository.
    /// </summary>
    private async Task InstallRepositoryRequirementsAsync(
        string repoPath,
        string mainRepositoryPath,
        string repoName,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        // Look for requirements.txt in the repository
        var requirementsPath = Path.Combine(repoPath, "requirements.txt");
        if (!File.Exists(requirementsPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"No requirements.txt found for {repoName}, skipping."
            });
            return;
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Installing requirements for {repoName}..."
        });

        // Find the pip executable in the virtual environment
        var venvPath = Path.Combine(mainRepositoryPath, "venv");
        if (!Directory.Exists(venvPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Virtual environment not found at {venvPath}, skipping requirements installation."
            });
            return;
        }

        var pipExecutable = _pythonService.GetVenvPipExecutable(venvPath);
        if (!File.Exists(pipExecutable))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Pip executable not found at {pipExecutable}, skipping requirements installation."
            });
            return;
        }

        var result = await _pythonService.InstallRequirementsAsync(
            pipExecutable,
            requirementsPath,
            progress,
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Failed to install requirements for {repoName}: {result.Message}"
            });
        }
        else
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = $"Requirements installed for {repoName}."
            });
        }
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
