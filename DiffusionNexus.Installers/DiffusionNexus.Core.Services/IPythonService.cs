using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Services;

/// <summary>
/// Information about an installed Python interpreter.
/// </summary>
public record PythonInstallation
{
    /// <summary>
    /// Full path to the Python executable.
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Python version (e.g., "3.12.0").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Major.Minor version (e.g., "3.12").
    /// </summary>
    public string MajorMinorVersion => Version.Length >= 4 ? Version[..4].TrimEnd('.') : Version;

    /// <summary>
    /// Whether this is a virtual environment.
    /// </summary>
    public bool IsVirtualEnvironment { get; init; }
}

/// <summary>
/// Result of a Python operation.
/// </summary>
public record PythonOperationResult
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
    /// Path to the created virtual environment, if applicable.
    /// </summary>
    public string? VirtualEnvironmentPath { get; init; }

    /// <summary>
    /// Path to the Python executable, if applicable.
    /// </summary>
    public string? PythonExecutablePath { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PythonOperationResult Success(string message, string? venvPath = null, string? pythonPath = null) =>
        new() { IsSuccess = true, Message = message, VirtualEnvironmentPath = venvPath, PythonExecutablePath = pythonPath };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PythonOperationResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}

/// <summary>
/// Options for creating a virtual environment.
/// </summary>
public record VirtualEnvironmentOptions
{
    /// <summary>
    /// Base directory where the virtual environment will be created.
    /// </summary>
    public required string BaseDirectory { get; init; }

    /// <summary>
    /// Name of the virtual environment folder.
    /// </summary>
    public string Name { get; init; } = "venv";

    /// <summary>
    /// Required Python version (e.g., "3.12").
    /// </summary>
    public required string RequiredPythonVersion { get; init; }

    /// <summary>
    /// Optional path to a specific Python interpreter to use.
    /// </summary>
    public string? InterpreterPath { get; init; }

    /// <summary>
    /// Whether to upgrade pip after creating the environment.
    /// </summary>
    public bool UpgradePip { get; init; } = true;
}

/// <summary>
/// Service for Python operations.
/// </summary>
public interface IPythonService
{
    /// <summary>
    /// Gets all Python installations found on the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of Python installations.</returns>
    Task<IReadOnlyList<PythonInstallation>> GetInstalledPythonVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific Python version is installed.
    /// </summary>
    /// <param name="version">Major.Minor version to check (e.g., "3.12").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Python installation if found, null otherwise.</returns>
    Task<PythonInstallation?> FindPythonVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Python executable path for a specific version.
    /// </summary>
    /// <param name="version">Major.Minor version (e.g., "3.12").</param>
    /// <param name="interpreterOverride">Optional interpreter path override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the Python executable, or null if not found.</returns>
    Task<string?> GetPythonExecutableAsync(
        string version,
        string? interpreterOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a virtual environment.
    /// </summary>
    /// <param name="options">Options for the virtual environment.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<PythonOperationResult> CreateVirtualEnvironmentAsync(
        VirtualEnvironmentOptions options,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the Python executable within a virtual environment.
    /// </summary>
    /// <param name="venvPath">Path to the virtual environment.</param>
    /// <returns>Path to the Python executable.</returns>
    string GetVenvPythonExecutable(string venvPath);

    /// <summary>
    /// Gets the path to the pip executable within a virtual environment.
    /// </summary>
    /// <param name="venvPath">Path to the virtual environment.</param>
    /// <returns>Path to the pip executable.</returns>
    string GetVenvPipExecutable(string venvPath);

    /// <summary>
    /// Installs packages using pip in the specified environment.
    /// </summary>
    /// <param name="pipExecutable">Path to the pip executable.</param>
    /// <param name="packages">Packages to install.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<PythonOperationResult> InstallPackagesAsync(
        string pipExecutable,
        string[] packages,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs packages from a requirements file.
    /// </summary>
    /// <param name="pipExecutable">Path to the pip executable.</param>
    /// <param name="requirementsPath">Path to the requirements file.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<PythonOperationResult> InstallRequirementsAsync(
        string pipExecutable,
        string requirementsPath,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs packages using pip with a custom index URL (for PyTorch CUDA wheels).
    /// </summary>
    /// <param name="pipExecutable">Path to the pip executable.</param>
    /// <param name="packages">Packages to install.</param>
    /// <param name="indexUrl">Optional custom index URL (e.g., PyTorch CUDA wheels).</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<PythonOperationResult> InstallPackagesWithIndexAsync(
        string pipExecutable,
        string[] packages,
        string? indexUrl = null,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a Python script inline and returns the result.
    /// </summary>
    /// <param name="pythonExecutable">Path to the Python executable.</param>
    /// <param name="script">Python script to execute.</param>
    /// <param name="progress">Progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<PythonOperationResult> RunPythonScriptAsync(
        string pythonExecutable,
        string script,
        IProgress<InstallLogEntry>? progress = null,
        CancellationToken cancellationToken = default);
}
