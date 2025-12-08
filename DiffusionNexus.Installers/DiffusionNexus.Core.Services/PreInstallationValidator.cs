using System;
using System.IO;
using System.Linq;
using DiffusionNexus.Core.Models.Configuration;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Result of pre-installation validation.
/// </summary>
public record PreInstallationValidationResult
{
    /// <summary>
    /// Whether the installation can proceed.
    /// </summary>
    public required bool CanProceed { get; init; }

    /// <summary>
    /// Whether the target directory exists.
    /// </summary>
    public required bool TargetDirectoryExists { get; init; }

    /// <summary>
    /// Whether the target directory is empty.
    /// </summary>
    public required bool TargetDirectoryIsEmpty { get; init; }

    /// <summary>
    /// Whether the configuration has models to install.
    /// </summary>
    public required bool HasModels { get; init; }

    /// <summary>
    /// Whether the configuration has custom nodes (additional git repositories) to install.
    /// </summary>
    public required bool HasCustomNodes { get; init; }

    /// <summary>
    /// Whether the user should be prompted to switch to Models/Nodes only mode.
    /// </summary>
    public required bool ShouldSuggestModelsNodesOnly { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The full target path for the main repository.
    /// </summary>
    public string? FullTargetPath { get; init; }

    /// <summary>
    /// Creates a successful result indicating installation can proceed.
    /// </summary>
    public static PreInstallationValidationResult Success(string fullTargetPath, bool hasModels, bool hasCustomNodes) =>
        new()
        {
            CanProceed = true,
            TargetDirectoryExists = false,
            TargetDirectoryIsEmpty = true,
            HasModels = hasModels,
            HasCustomNodes = hasCustomNodes,
            ShouldSuggestModelsNodesOnly = false,
            FullTargetPath = fullTargetPath
        };

    /// <summary>
    /// Creates a result suggesting Models/Nodes only installation.
    /// </summary>
    public static PreInstallationValidationResult SuggestModelsNodesOnly(string fullTargetPath, bool hasModels, bool hasCustomNodes) =>
        new()
        {
            CanProceed = false,
            TargetDirectoryExists = true,
            TargetDirectoryIsEmpty = false,
            HasModels = hasModels,
            HasCustomNodes = hasCustomNodes,
            ShouldSuggestModelsNodesOnly = true,
            FullTargetPath = fullTargetPath
        };

    /// <summary>
    /// Creates a failure result when target is not empty and no models/nodes to install.
    /// </summary>
    public static PreInstallationValidationResult TargetNotEmpty(string fullTargetPath) =>
        new()
        {
            CanProceed = false,
            TargetDirectoryExists = true,
            TargetDirectoryIsEmpty = false,
            HasModels = false,
            HasCustomNodes = false,
            ShouldSuggestModelsNodesOnly = false,
            ErrorMessage = $"The target folder '{fullTargetPath}' is not empty and no models or custom nodes are configured for installation.",
            FullTargetPath = fullTargetPath
        };
}

/// <summary>
/// Validates pre-installation conditions.
/// </summary>
public interface IPreInstallationValidator
{
    /// <summary>
    /// Validates the installation configuration and target directory.
    /// </summary>
    /// <param name="configuration">The installation configuration.</param>
    /// <param name="targetDirectory">The base target directory.</param>
    /// <param name="isFullInstall">Whether this is a full install or models/nodes only.</param>
    /// <returns>Validation result.</returns>
    PreInstallationValidationResult Validate(
        InstallationConfiguration configuration,
        string targetDirectory,
        bool isFullInstall);
}

/// <summary>
/// Implementation of pre-installation validator.
/// </summary>
public class PreInstallationValidator : IPreInstallationValidator
{
    /// <inheritdoc />
    public PreInstallationValidationResult Validate(
        InstallationConfiguration configuration,
        string targetDirectory,
        bool isFullInstall)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        // Determine the full target path (base directory + repository folder name)
        var repoName = GetRepositoryNameFromUrl(configuration.Repository.RepositoryUrl);
        var fullTargetPath = Path.Combine(targetDirectory, repoName);

        // Check if configuration has models or custom nodes
        var hasModels = configuration.ModelDownloads?.Any(m => m.Enabled) == true;
        var hasCustomNodes = configuration.GitRepositories?.Count > 0;

        // For Models/Nodes only installation, we don't need to check target directory
        if (!isFullInstall)
        {
            return PreInstallationValidationResult.Success(fullTargetPath, hasModels, hasCustomNodes);
        }

        // For Full Install, check if target directory exists and is empty
        if (!Directory.Exists(fullTargetPath))
        {
            // Directory doesn't exist, installation can proceed
            return PreInstallationValidationResult.Success(fullTargetPath, hasModels, hasCustomNodes);
        }

        // Directory exists, check if it's empty
        var isEmpty = IsDirectoryEmpty(fullTargetPath);

        if (isEmpty)
        {
            // Directory exists but is empty, installation can proceed
            return PreInstallationValidationResult.Success(fullTargetPath, hasModels, hasCustomNodes);
        }

        // Directory is not empty
        if (hasModels || hasCustomNodes)
        {
            // Suggest switching to Models/Nodes only mode
            return PreInstallationValidationResult.SuggestModelsNodesOnly(fullTargetPath, hasModels, hasCustomNodes);
        }

        // No models or custom nodes, cannot proceed
        return PreInstallationValidationResult.TargetNotEmpty(fullTargetPath);
    }

    /// <summary>
    /// Validates that an existing ComfyUI installation is present at the target directory.
    /// Required for Models/Nodes only installation mode.
    /// </summary>
    /// <param name="targetDirectory">The target directory to check.</param>
    /// <returns>True if a valid ComfyUI installation is found, false otherwise.</returns>
    public bool IsValidComfyUIInstallation(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return false;
        }

        // Check if the directory exists
        if (!Directory.Exists(targetDirectory))
        {
            return false;
        }

        // Check if the folder is named "ComfyUI"
        var directoryName = Path.GetFileName(targetDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(directoryName, "ComfyUI", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if it contains a "venv" folder
        var venvPath = Path.Combine(targetDirectory, "venv");
        if (Directory.Exists(venvPath))
        {
            return true;
        }

        // Check if it contains a "python_embeded" folder
        var pythonEmbededPath = Path.Combine(targetDirectory, "python_embeded");
        if (Directory.Exists(pythonEmbededPath))
        {
            return true;
        }

        // Check if it contains a "python_embedded" folder (alternate spelling)
        var pythonEmbeddedPath = Path.Combine(targetDirectory, "python_embedded");
        if (Directory.Exists(pythonEmbeddedPath))
        {
            return true;
        }

        return false;
    }

    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (UnauthorizedAccessException)
        {
            // If we can't access the directory, assume it's not empty
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            // Directory doesn't exist, consider it empty
            return true;
        }
    }

    private static string GetRepositoryNameFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "repository";
        }

        var uri = url.TrimEnd('/');

        if (uri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri[..^4];
        }

        var lastSlash = uri.LastIndexOf('/');
        return lastSlash >= 0 ? uri[(lastSlash + 1)..] : uri;
    }
}
