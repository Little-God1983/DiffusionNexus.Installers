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
            
            // Always install PyTorch after venv creation (or Python check if no venv)
            steps.Add(InstallationStep.InstallTorch);

            // Install Triton if configured OR if SageAttention is configured (SageAttention requires Triton)
            if (configuration.Python.InstallTriton || configuration.Python.InstallSageAttention)
            {
                steps.Add(InstallationStep.InstallTriton);
            }

            // Install SageAttention if configured (requires Triton, which is added above)
            if (configuration.Python.InstallSageAttention)
            {
                steps.Add(InstallationStep.InstallSageAttention);
            }
            
            // Install main repository requirements (e.g., ComfyUI's requirements.txt)
            steps.Add(InstallationStep.InstallRequirements);
            
            if (configuration.GitRepositories?.Count > 0)
            {
                steps.Add(InstallationStep.CloneAdditionalRepositories);
            }
            if (configuration.ModelDownloads?.Count > 0)
            {
                steps.Add(InstallationStep.DownloadModels);
            }

            // Always add PostInstall step to create launcher scripts
            steps.Add(InstallationStep.PostInstall);
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
                InstallationStep.InstallTorch => await InstallTorchAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.InstallTriton => await InstallTritonAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.InstallSageAttention => await InstallSageAttentionAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.InstallRequirements => await InstallMainRequirementsAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.CloneAdditionalRepositories => await CloneAdditionalRepositoriesAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    logProgress,
                    cancellationToken),
                InstallationStep.DownloadModels => await DownloadModelsAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    venvPath,
                    logProgress,
                    cancellationToken),
                InstallationStep.PostInstall => await CreateLauncherScriptsAsync(
                    configuration,
                    repositoryPath ?? Path.Combine(targetDirectory, GetRepositoryName(configuration.Repository.RepositoryUrl)),
                    options,
                    logProgress,
                    cancellationToken),
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

    /// <summary>
    /// Installs PyTorch with the configured CUDA version.
    /// </summary>
    private async Task<InstallationStepResult> InstallTorchAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        var torchSettings = configuration.Torch;
        
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Installing PyTorch..."
        });

        // Find the pip executable in the virtual environment
        var venvName = configuration.Python.VirtualEnvironmentName;
        if (string.IsNullOrWhiteSpace(venvName))
        {
            venvName = "venv";
        }
        
        var venvPath = Path.Combine(repositoryPath, venvName);
        string pipExecutable;
        
        if (Directory.Exists(venvPath))
        {
            pipExecutable = _pythonService.GetVenvPipExecutable(venvPath);
            if (!File.Exists(pipExecutable))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Error,
                    Message = $"Pip executable not found at {pipExecutable}"
                });
                return InstallationStepResult.Failure(
                    InstallationStep.InstallTorch,
                    $"Pip executable not found at {pipExecutable}");
            }
        }
        else
        {
            // No venv, try to use system pip
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = "Virtual environment not found, attempting to use system Python..."
            });
            
            var pythonInstallation = await _pythonService.FindPythonVersionAsync(
                configuration.Python.PythonVersion, 
                cancellationToken);
                
            if (pythonInstallation is null)
            {
                return InstallationStepResult.Failure(
                    InstallationStep.InstallTorch,
                    "Python installation not found for PyTorch installation.");
            }
            
            // Use python -m pip instead
            pipExecutable = pythonInstallation.ExecutablePath;
        }

        // Build the index URL for PyTorch
        var indexUrl = torchSettings.IndexUrl;
        if (string.IsNullOrWhiteSpace(indexUrl) && !string.IsNullOrWhiteSpace(torchSettings.CudaVersion))
        {
            var cudaSuffix = DeriveCudaSuffix(torchSettings.CudaVersion);
            indexUrl = $"https://download.pytorch.org/whl/{cudaSuffix}";
        }

        // Build the package list
        var packages = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(torchSettings.TorchVersion))
        {
            packages.Add($"torch=={torchSettings.TorchVersion}");
            packages.Add($"torchvision");
            packages.Add($"torchaudio");
        }
        else
        {
            packages.Add("torch");
            packages.Add("torchvision");
            packages.Add("torchaudio");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = $"Installing PyTorch packages: {string.Join(", ", packages)}"
        });

        if (!string.IsNullOrWhiteSpace(indexUrl))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"Using index URL: {indexUrl}"
            });
        }

        // Install PyTorch packages
        var result = await _pythonService.InstallPackagesWithIndexAsync(
            pipExecutable,
            packages.ToArray(),
            indexUrl,
            progress,
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to install PyTorch: {result.Message}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallTorch,
                $"Failed to install PyTorch: {result.Message}");
        }

        // Verify PyTorch installation
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Verifying PyTorch installation..."
        });

        var verifyResult = await VerifyTorchInstallationAsync(
            venvPath,
            progress,
            cancellationToken);

        if (!verifyResult.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"PyTorch verification: {verifyResult.Message}"
            });
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "PyTorch installed successfully."
        });

        return InstallationStepResult.Success(
            InstallationStep.InstallTorch,
            "PyTorch installed successfully.");
    }

    /// <summary>
    /// Verifies that PyTorch is installed correctly with CUDA support.
    /// </summary>
    private async Task<PythonOperationResult> VerifyTorchInstallationAsync(
        string venvPath,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        var pythonExecutable = _pythonService.GetVenvPythonExecutable(venvPath);
        if (!File.Exists(pythonExecutable))
        {
            return PythonOperationResult.Failure("Python executable not found in venv.");
        }

        // Run a verification script
        var verifyScript = "import torch; print(f'torch {torch.__version__}'); print(f'cuda {torch.version.cuda}'); print(f'cuda_available {torch.cuda.is_available()}')";
        
        progress?.Report(InstallLogEntry.ForCommand($"{pythonExecutable} -c \"{verifyScript}\"", venvPath));

        var result = await _pythonService.RunPythonScriptAsync(
            pythonExecutable,
            verifyScript,
            progress,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Installs Triton for Windows (GPU kernel optimization).
    /// </summary>
    private async Task<InstallationStepResult> InstallTritonAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        // Install Triton if explicitly enabled OR if SageAttention is enabled (SageAttention requires Triton)
        if (!configuration.Python.InstallTriton && !configuration.Python.InstallSageAttention)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "Triton installation is disabled. Skipping..."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.InstallTriton,
                "Triton installation is disabled.");
        }

        var reason = configuration.Python.InstallTriton 
            ? "Installing Triton for Windows..." 
            : "Installing Triton for Windows (required by SageAttention)...";
        
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = reason
        });

        // Find the pip executable in the virtual environment
        var venvName = configuration.Python.VirtualEnvironmentName;
        if (string.IsNullOrWhiteSpace(venvName))
        {
            venvName = "venv";
        }

        var venvPath = Path.Combine(repositoryPath, venvName);
        string pipExecutable;

        if (Directory.Exists(venvPath))
        {
            pipExecutable = _pythonService.GetVenvPipExecutable(venvPath);
            if (!File.Exists(pipExecutable))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Error,
                    Message = $"Pip executable not found at {pipExecutable}"
                });
                return InstallationStepResult.Failure(
                    InstallationStep.InstallTriton,
                    $"Pip executable not found at {pipExecutable}");
            }
        }
        else
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Virtual environment not found at {venvPath}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallTriton,
                $"Virtual environment not found at {venvPath}. Triton requires a virtual environment.");
        }

        // Step 1: Uninstall any existing triton package
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Uninstalling existing triton package (if any)..."
        });

        await _pythonService.UninstallPackagesAsync(
            pipExecutable,
            ["triton"],
            progress,
            cancellationToken);

        // Step 2: Install triton-windows package
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Installing triton-windows..."
        });

        var result = await _pythonService.InstallPackagesAsync(
            pipExecutable,
            ["triton-windows<3.5"],
            progress,
            cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to install triton-windows: {result.Message}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallTriton,
                $"Failed to install triton-windows: {result.Message}",
                shouldContinue: true); // Continue with installation even if Triton fails
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Triton installed successfully."
        });

        return InstallationStepResult.Success(
            InstallationStep.InstallTriton,
            "Triton installed successfully.");
    }

    /// <summary>
    /// Installs SageAttention for optimized attention computation.
    /// Requires Triton to be installed first.
    /// </summary>
    private async Task<InstallationStepResult> InstallSageAttentionAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        if (!configuration.Python.InstallSageAttention)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "SageAttention installation is disabled. Skipping..."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.InstallSageAttention,
                "SageAttention installation is disabled.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Installing SageAttention..."
        });

        // Find the pip executable in the virtual environment
        var venvName = configuration.Python.VirtualEnvironmentName;
        if (string.IsNullOrWhiteSpace(venvName))
        {
            venvName = "venv";
        }

        var venvPath = Path.Combine(repositoryPath, venvName);
        string pipExecutable;

        if (Directory.Exists(venvPath))
        {
            pipExecutable = _pythonService.GetVenvPipExecutable(venvPath);
            if (!File.Exists(pipExecutable))
            {
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Error,
                    Message = $"Pip executable not found at {pipExecutable}"
                });
                return InstallationStepResult.Failure(
                    InstallationStep.InstallSageAttention,
                    $"Pip executable not found at {pipExecutable}");
            }
        }
        else
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Virtual environment not found at {venvPath}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallSageAttention,
                $"Virtual environment not found at {venvPath}. SageAttention requires a virtual environment.");
        }

        // Step 1: Uninstall any existing sageattention package
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Uninstalling existing sageattention package (if any)..."
        });

        await _pythonService.UninstallPackagesAsync(
            pipExecutable,
            ["sageattention"],
            progress,
            cancellationToken);

        // Step 2: Install SageAttention from pre-built wheel
        // Using the Windows wheel from woct0rdho's releases
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Installing SageAttention 2.2.0 (Windows, cu128, Torch 2.8)..."
        });

        // Install with --no-deps to avoid pulling in incompatible dependencies
        var pythonExecutable = _pythonService.GetVenvPythonExecutable(venvPath);
        var wheelUrl = "https://github.com/woct0rdho/SageAttention/releases/download/v2.2.0-windows.post2/sageattention-2.2.0+cu128torch2.8.0.post2-cp39-abi3-win_amd64.whl";
        
        var installArgs = $"-m pip install --no-deps \"{wheelUrl}\"";
        progress?.Report(InstallLogEntry.ForCommand($"{pythonExecutable} {installArgs}", venvPath));

        var result = await _pythonService.RunPythonScriptAsync(
            pythonExecutable,
            $"import subprocess; import sys; sys.exit(subprocess.call([sys.executable, '-m', 'pip', 'install', '--no-deps', '{wheelUrl}']))",
            progress,
            cancellationToken);

        // Alternative: Use pip directly with the wheel URL
        var pipResult = await _pythonService.InstallPackagesAsync(
            pipExecutable,
            [$"--no-deps", wheelUrl],
            progress,
            cancellationToken);

        if (!pipResult.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Failed to install SageAttention: {pipResult.Message}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallSageAttention,
                $"Failed to install SageAttention: {pipResult.Message}",
                shouldContinue: true); // Continue with installation even if SageAttention fails
        }

        // Step 3: Verify SageAttention installation
        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Verifying SageAttention import..."
        });

        var verifyScript = "import sageattention; import torch; print(f'sage: {getattr(sageattention, \"__version__\", \"?\")} torch: {torch.__version__} cuda: {getattr(torch.version, \"cuda\", None)}')";
        var verifyResult = await _pythonService.RunPythonScriptAsync(
            pythonExecutable,
            verifyScript,
            progress,
            cancellationToken);

        if (!verifyResult.IsSuccess)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"SageAttention verification failed: {verifyResult.Message}"
            });
            // Don't fail the step, just warn
        }
        else
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = $"SageAttention verified: {verifyResult.Message}"
            });
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "SageAttention installed successfully."
        });

        return InstallationStepResult.Success(
            InstallationStep.InstallSageAttention,
            "SageAttention installed successfully.");
    }

    /// <summary>
    /// Installs the main repository's requirements.txt.
    /// </summary>
    private async Task<InstallationStepResult> InstallMainRequirementsAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        var requirementsPath = Path.Combine(repositoryPath, "requirements.txt");
        
        if (!File.Exists(requirementsPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = "No requirements.txt found in main repository, skipping."
            });
            return InstallationStepResult.Skipped(
                InstallationStep.InstallRequirements,
                "No requirements.txt found.");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Installing main repository requirements..."
        });

        // Find the pip executable in the virtual environment
        var venvName = configuration.Python.VirtualEnvironmentName;
        if (string.IsNullOrWhiteSpace(venvName))
        {
            venvName = "venv";
        }
        
        var venvPath = Path.Combine(repositoryPath, venvName);
        
        if (!Directory.Exists(venvPath))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Virtual environment not found at {venvPath}, skipping requirements installation."
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallRequirements,
                $"Virtual environment not found at {venvPath}");
        }

        var pipExecutable = _pythonService.GetVenvPipExecutable(venvPath);
        if (!File.Exists(pipExecutable))
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = $"Pip executable not found at {pipExecutable}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallRequirements,
                $"Pip executable not found at {pipExecutable}");
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
                Level = LogLevel.Error,
                Message = $"Failed to install requirements: {result.Message}"
            });
            return InstallationStepResult.Failure(
                InstallationStep.InstallRequirements,
                $"Failed to install requirements: {result.Message}");
        }

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Success,
            Message = "Main repository requirements installed successfully."
        });

        return InstallationStepResult.Success(
            InstallationStep.InstallRequirements,
            "Requirements installed successfully.");
    }

    /// <summary>
    /// Derives the CUDA suffix for PyTorch wheel URL (e.g., "cu128" from "12.8").
    /// </summary>
    private static string DeriveCudaSuffix(string cudaVersion)
    {
        if (string.IsNullOrWhiteSpace(cudaVersion))
        {
            return "cpu";
        }

        var sanitized = new string(cudaVersion.Where(char.IsDigit).ToArray());
        if (sanitized.Length < 2)
        {
            return "cpu";
        }

        return $"cu{sanitized}";
    }

    /// <summary>
    /// Creates launcher scripts (batch files) and shortcuts for running the application.
    /// </summary>
    private Task<InstallationStepResult> CreateLauncherScriptsAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        InstallationOptions options,
        IProgress<InstallLogEntry>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new InstallLogEntry
        {
            Level = LogLevel.Info,
            Message = "Creating launcher scripts..."
        });

        try
        {
            // Create run_nvidia.bat for NVIDIA GPU users
            var batFilePath = Path.Combine(repositoryPath, "run_nvidia.bat");
            var batContent = """
                @echo off
                REM Activate the virtual environment
                call venv\Scripts\activate

                REM Run ComfyUI in a new window with custom output
                python main.py --windows-standalone-build
                """;

            File.WriteAllText(batFilePath, batContent);

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Success,
                Message = "Created launcher script: run_nvidia.bat"
            });

            // Create shortcuts if requested
            var shortcutName = "ComfyUI_AIK2go";
            var iconPath = GetIconPath();

            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(
                    GetDesktopPath(),
                    shortcutName,
                    batFilePath,
                    repositoryPath,
                    iconPath,
                    progress);
            }

            if (options.CreateStartMenuShortcut)
            {
                CreateShortcut(
                    GetStartMenuPath(),
                    shortcutName,
                    batFilePath,
                    repositoryPath,
                    iconPath,
                    progress);
            }

            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Info,
                Message = $"You can run the application using: {batFilePath}"
            });

            return Task.FromResult(InstallationStepResult.Success(
                InstallationStep.PostInstall,
                $"Launcher scripts created at {repositoryPath}"));
        }
        catch (Exception ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Failed to create launcher scripts: {ex.Message}"
            });

            // Don't fail the installation if script creation fails
            return Task.FromResult(InstallationStepResult.Success(
                InstallationStep.PostInstall,
                $"Post-install completed with warnings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the path to the application icon.
    /// </summary>
    private static string? GetIconPath()
    {
        // Try to find the icon in the application directory
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var iconPath = Path.Combine(appDir, "Assets", "AIKnowledgeIcon.ico");
        
        if (File.Exists(iconPath))
        {
            return iconPath;
        }

        // Try alternate location
        iconPath = Path.Combine(appDir, "AIKnowledgeIcon.ico");
        if (File.Exists(iconPath))
        {
            return iconPath;
        }

        return null;
    }

    /// <summary>
    /// Gets the path to the user's desktop folder.
    /// </summary>
    private static string GetDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    /// <summary>
    /// Gets the path to the user's Start Menu Programs folder.
    /// </summary>
    private static string GetStartMenuPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
    }

    /// <summary>
    /// Creates a Windows shortcut (.lnk file) using PowerShell.
    /// </summary>
    private static void CreateShortcut(
        string destinationFolder,
        string shortcutName,
        string targetPath,
        string workingDirectory,
        string? iconPath,
        IProgress<InstallLogEntry>? progress)
    {
        try
        {
            var shortcutPath = Path.Combine(destinationFolder, $"{shortcutName}.lnk");

            // Use PowerShell to create the shortcut (works without COM interop)
            var iconArg = !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)
                ? $"$s.IconLocation = '{iconPath}';"
                : "";

            var script = $@"
$WshShell = New-Object -ComObject WScript.Shell;
$s = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}');
$s.TargetPath = '{targetPath.Replace("'", "''")}';
$s.WorkingDirectory = '{workingDirectory.Replace("'", "''")}';
$s.Description = 'Launch ComfyUI with AIK2go configuration';
{iconArg}
$s.Save();
";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit(10000); // 10 second timeout

            if (process?.ExitCode == 0)
            {
                var locationName = destinationFolder.Contains("Start Menu") ? "Start Menu" : "Desktop";
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Success,
                    Message = $"Created {locationName} shortcut: {shortcutName}"
                });
            }
            else
            {
                var error = process?.StandardError.ReadToEnd() ?? "Unknown error";
                progress?.Report(new InstallLogEntry
                {
                    Level = LogLevel.Warning,
                    Message = $"Failed to create shortcut at {destinationFolder}: {error}"
                });
            }
        }
        catch (Exception ex)
        {
            progress?.Report(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = $"Failed to create shortcut: {ex.Message}"
            });
        }
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
            InstallationStep.InstallTriton => "Installing Triton...",
            InstallationStep.InstallSageAttention => "Installing SageAttention...",
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
