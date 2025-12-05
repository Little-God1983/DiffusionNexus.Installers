using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Result of a Git operation.
/// </summary>
public record GitOperationResult
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Path to the cloned repository, if applicable.
    /// </summary>
    public string? RepositoryPath { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static GitOperationResult Success(string message, string? repositoryPath = null) =>
        new() { IsSuccess = true, Message = message, RepositoryPath = repositoryPath };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static GitOperationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}

/// <summary>
/// Options for cloning a Git repository.
/// </summary>
public record GitCloneOptions
{
    /// <summary>
    /// The URL of the repository to clone.
    /// </summary>
    public required string RepositoryUrl { get; init; }

    /// <summary>
    /// The target directory where the repository will be cloned.
    /// </summary>
    public required string TargetDirectory { get; init; }

    /// <summary>
    /// Optional branch to checkout after cloning.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Optional specific commit hash to checkout.
    /// </summary>
    public string? CommitHash { get; init; }

    /// <summary>
    /// Whether to perform a shallow clone (depth=1).
    /// </summary>
    public bool ShallowClone { get; init; } = false;

    /// <summary>
    /// Custom folder name for the cloned repository. If null, uses the repository name.
    /// </summary>
    public string? FolderName { get; init; }
}

/// <summary>
/// Service for Git operations.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks if Git is installed and available on the system.
    /// </summary>
    /// <returns>True if Git is available, false otherwise.</returns>
    bool IsGitInstalled();

    /// <summary>
    /// Gets the installed Git version.
    /// </summary>
    /// <returns>The Git version string, or null if Git is not installed.</returns>
    Task<string?> GetGitVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to install Git on the system.
    /// </summary>
    /// <param name="progress">Progress callback for installation status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the installation attempt.</returns>
    Task<GitOperationResult> InstallGitAsync(
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones a Git repository.
    /// </summary>
    /// <param name="options">Options for the clone operation.</param>
    /// <param name="progress">Progress callback for clone status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the clone operation.</returns>
    Task<GitOperationResult> CloneRepositoryAsync(
        GitCloneOptions options,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a directory is a Git repository.
    /// </summary>
    /// <param name="directory">Directory to check.</param>
    /// <returns>True if the directory is a Git repository.</returns>
    bool IsGitRepository(string directory);

    /// <summary>
    /// Pulls latest changes from the remote repository.
    /// </summary>
    /// <param name="repositoryPath">Path to the repository.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the pull operation.</returns>
    Task<GitOperationResult> PullAsync(
        string repositoryPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);
}
