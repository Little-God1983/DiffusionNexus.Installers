using System;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models.Configuration;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Runtime options for installation selected by the user in the installer UI.
/// These are not persisted with the configuration.
/// </summary>
public record InstallationOptions
{
    /// <summary>
    /// When true, skips repository cloning and environment setup,
    /// and only downloads models to an existing installation.
    /// </summary>
    public bool OnlyModelDownload { get; init; }

    /// <summary>
    /// The VRAM profile selected by the user (in GB).
    /// Used to filter which models to download.
    /// </summary>
    public int SelectedVramProfile { get; init; }

    /// <summary>
    /// When true, logs detailed command information including exact Python/Git commands being executed.
    /// </summary>
    public bool VerboseLogging { get; init; }

    /// <summary>
    /// Creates default options for a full installation.
    /// </summary>
    public static InstallationOptions Default => new() { OnlyModelDownload = false, SelectedVramProfile = 0, VerboseLogging = false };
}

/// <summary>
/// Result of an installation step.
/// </summary>
public record InstallationStepResult
{
    /// <summary>
    /// Indicates whether the step was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The step that was executed.
    /// </summary>
    public required InstallationStep Step { get; init; }

    /// <summary>
    /// Whether the installation should continue after this step.
    /// </summary>
    public bool ShouldContinue { get; init; } = true;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static InstallationStepResult Success(InstallationStep step, string message) =>
        new() { IsSuccess = true, Message = message, Step = step };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static InstallationStepResult Failure(InstallationStep step, string message, bool shouldContinue = false) =>
        new() { IsSuccess = false, Message = message, Step = step, ShouldContinue = shouldContinue };

    /// <summary>
    /// Creates a skipped result.
    /// </summary>
    public static InstallationStepResult Skipped(InstallationStep step, string message) =>
        new() { IsSuccess = true, Message = message, Step = step };
}

/// <summary>
/// Represents the installation steps.
/// </summary>
public enum InstallationStep
{
    /// <summary>
    /// Check and install Git if needed.
    /// </summary>
    GitSetup,

    /// <summary>
    /// Verify Python version is available.
    /// </summary>
    PythonCheck,

    /// <summary>
    /// Clone the main repository.
    /// </summary>
    CloneMainRepository,

    /// <summary>
    /// Create virtual environment.
    /// </summary>
    CreateVirtualEnvironment,

    /// <summary>
    /// Clone additional Git repositories (custom nodes).
    /// </summary>
    CloneAdditionalRepositories,

    /// <summary>
    /// Install Python requirements.
    /// </summary>
    InstallRequirements,

    /// <summary>
    /// Install Torch and CUDA dependencies.
    /// </summary>
    InstallTorch,

    /// <summary>
    /// Download models.
    /// </summary>
    DownloadModels,

    /// <summary>
    /// Run post-installation hooks.
    /// </summary>
    PostInstall,

    /// <summary>
    /// Update an existing installation (model-only mode).
    /// </summary>
    Update
}

/// <summary>
/// Progress information for installation.
/// </summary>
public record InstallationProgress
{
    /// <summary>
    /// Current step being executed.
    /// </summary>
    public required InstallationStep CurrentStep { get; init; }

    /// <summary>
    /// Zero-based index of the current step.
    /// </summary>
    public required int StepIndex { get; init; }

    /// <summary>
    /// Total number of steps.
    /// </summary>
    public required int TotalSteps { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercentage => TotalSteps > 0 ? (double)StepIndex / TotalSteps * 100 : 0;

    /// <summary>
    /// Status message.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Result of the full installation.
/// </summary>
public record InstallationResult
{
    /// <summary>
    /// Indicates whether the installation was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Summary message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Path to the installed repository.
    /// </summary>
    public string? RepositoryPath { get; init; }

    /// <summary>
    /// Path to the virtual environment.
    /// </summary>
    public string? VirtualEnvironmentPath { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static InstallationResult Success(string message, string? repoPath = null, string? venvPath = null) =>
        new() { IsSuccess = true, Message = message, RepositoryPath = repoPath, VirtualEnvironmentPath = venvPath };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static InstallationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}

/// <summary>
/// Orchestrates the installation process.
/// </summary>
public interface IInstallationOrchestrator
{
    /// <summary>
    /// Runs the full installation process.
    /// </summary>
    /// <param name="configuration">Installation configuration.</param>
    /// <param name="targetDirectory">Target directory for installation.</param>
    /// <param name="options">Runtime installation options selected by the user.</param>
    /// <param name="logProgress">Progress callback for log entries.</param>
    /// <param name="stepProgress">Progress callback for step progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the installation.</returns>
    Task<InstallationResult> InstallAsync(
        InstallationConfiguration configuration,
        string targetDirectory,
        InstallationOptions options,
        IProgress<InstallLogEntry>? logProgress = null,
        IProgress<InstallationProgress>? stepProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs only the Git setup step.
    /// </summary>
    Task<InstallationStepResult> EnsureGitInstalledAsync(
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the required Python version is available.
    /// </summary>
    Task<InstallationStepResult> CheckPythonVersionAsync(
        string requiredVersion,
        string? interpreterOverride = null,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones the main repository.
    /// </summary>
    Task<InstallationStepResult> CloneMainRepositoryAsync(
        InstallationConfiguration configuration,
        string targetDirectory,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a virtual environment.
    /// </summary>
    Task<InstallationStepResult> CreateVirtualEnvironmentAsync(
        InstallationConfiguration configuration,
        string repositoryPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);
}
