using DiffusionNexus.Installers.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Represents a full installer configuration used by both the UI authoring tool
    /// and the headless installation engine.
    /// </summary>
    public class InstallationConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Friendly name to help users distinguish between saved configurations.
        /// </summary>
        public string Name { get; set; } = "New Configuration";

        /// <summary>
        /// Description displayed in the UI when loading an existing configuration.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public MainRepositorySettings Repository { get; set; } = new();

        public PythonEnvironmentSettings Python { get; set; } = new();

        /// <summary>
        /// Torch and CUDA selections are authored separately from Python because
        /// they may change independently of interpreter selection.
        /// </summary>
        public TorchSettings Torch { get; set; } = new();

        /// <summary>
        /// Ordered list of repositories that must be cloned after the primary
        /// repository. Priority values are derived from the current ordering.
        /// </summary>
        public List<GitRepository> GitRepositories { get; set; } = new();

        /// <summary>
        /// Models that should be downloaded after repositories are prepared.
        /// </summary>
        public List<ModelDownload> ModelDownloads { get; set; } = new();

        /// <summary>
        /// Paths that influence where repositories, models and logs are written.
        /// </summary>
        public PathSettings Paths { get; set; } = new();
        public VramSettings Vram { get; set; }
    }

    /// <summary>
    /// Primary software selector (ComfyUI, Automatic1111, Forge, etc.).
    /// </summary>
    public class MainRepositorySettings
    {
        [Required]
        public RepositoryType Type { get; set; } = RepositoryType.ComfyUI;

        [Required]
        public string RepositoryUrl { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string CommitHash { get; set; } = string.Empty;
    }

    public enum RepositoryType
    {
        ComfyUI,
        A1111,
        Forge
    }

    /// <summary>
    /// Python environment configuration.
    /// </summary>
    public class PythonEnvironmentSettings
    {
        /// <summary>
        /// Semantic version (e.g. 3.10) represented as a string to preserve the
        /// exact value chosen in the UI.
        /// </summary>
        [Required]
        public string PythonVersion { get; set; } = "3.12";

        /// <summary>
        /// Optional override to an existing interpreter on disk. Empty string
        /// indicates the installer should use the default interpreter for the
        /// selected version.
        /// </summary>
        public string InterpreterPathOverride { get; set; } = string.Empty;

        /// <summary>
        /// Controls whether the installer will create and manage a virtual
        /// environment.
        /// </summary>
        public bool CreateVirtualEnvironment { get; set; } = true;
        public bool CreateVramSettings { get; set; } = true;

        /// <summary>
        /// When <see cref="CreateVirtualEnvironment"/> is true this allows the
        /// user to override the folder name that will be created.
        /// </summary>
        public string VirtualEnvironmentName { get; set; } = "venv";
    }

    /// <summary>
    /// Torch/CUDA settings used to install the appropriate binaries.
    /// </summary>
    public class TorchSettings
    {
        /// <summary>
        /// User supplied Torch version. Empty string means "latest".
        /// </summary>
        public string TorchVersion { get; set; } = string.Empty;

        /// <summary>
        /// CUDA version string (e.g. "12.8").
        /// </summary>
        public string CudaVersion { get; set; } = "12.8";

        /// <summary>
        /// Optional custom index URL. If null or empty the engine derives an
        /// appropriate value from <see cref="CudaVersion"/>.
        /// </summary>
        public string? IndexUrl { get; set; }
 = string.Empty;
    }

    /// <summary>
    /// Download targets may use VRAM profiles to select destination folders.
    /// </summary>
    public enum VramProfile
    {
        VRAM_8GB,
        VRAM_12GB,
        VRAM_16GB,
        VRAM_24GB,
        Custom
    }

    /// <summary>
    /// General file system settings for the installation.
    /// </summary>
    public class PathSettings
    {
        [Required]
        public string RootDirectory { get; set; } = string.Empty;

        public string? DefaultModelDownloadDirectory { get; set; }
            = string.Empty;

        public string LogFileName { get; set; } = "install.log";
    }
}
