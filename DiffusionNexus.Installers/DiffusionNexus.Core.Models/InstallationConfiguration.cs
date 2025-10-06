using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Main configuration class that holds all installation settings
    /// </summary>
    public class InstallationConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Configuration";
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        // Core Settings
        public MainRepositorySettings MainRepository { get; set; } = new();
        public PythonEnvironmentSettings PythonEnvironment { get; set; } = new();
        public HardwareSettings Hardware { get; set; } = new();
        public PathSettings Paths { get; set; } = new();

        // Components to Install
        public List<GitRepository> CustomNodes { get; set; } = new();
        public List<ModelDownload> Models { get; set; } = new();
        public List<CustomCommand> PostInstallCommands { get; set; } = new();

        // Installation Options
        public InstallationOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Main repository settings (ComfyUI, Forge, A1111, etc.)
    /// </summary>
    public class MainRepositorySettings
    {
        [Required]
        public RepositoryType Type { get; set; } = RepositoryType.ComfyUI;

        [Required]
        public string RepositoryUrl { get; set; } = "https://github.com/comfyanonymous/ComfyUI";

        public string Branch { get; set; } = string.Empty; // Empty means latest release or main
        public string CommitHash { get; set; } = string.Empty; // Specific commit if needed
        public bool UseLatestRelease { get; set; } = true;
        public bool ShallowClone { get; set; } = true;
        public int CloneDepth { get; set; } = 1;
    }

    public enum RepositoryType
    {
        ComfyUI,
        StableDiffusionWebUI, // A1111
        StableDiffusionWebUIForge,
        Custom
    }

    /// <summary>
    /// Python environment configuration
    /// </summary>
    public class PythonEnvironmentSettings
    {
        [Required]
        [Range(3.8, 3.13)]
        public double PythonVersion { get; set; } = 3.12;

        public string PythonPath { get; set; } = string.Empty; // Empty means use system python
        public bool CreateVirtualEnvironment { get; set; } = true;
        public string VenvName { get; set; } = "venv";
        public bool UpgradePip { get; set; } = true;

        // PyTorch Settings
        public TorchSettings Torch { get; set; } = new();

        // Additional packages
        public List<PipPackage> AdditionalPackages { get; set; } = new();
    }

    /// <summary>
    /// PyTorch/CUDA configuration
    /// </summary>
    public class TorchSettings
    {
        public string TorchVersion { get; set; } = "latest";
        public CudaVersion CudaVersion { get; set; } = CudaVersion.Cuda128;
        public string IndexUrl { get; set; } = "https://download.pytorch.org/whl/cu128";
        public bool InstallTorchVision { get; set; } = true;
        public bool InstallTorchAudio { get; set; } = true;
        public bool VerifyCudaVersion { get; set; } = true;
    }

    public enum CudaVersion
    {
        Cpu,
        Cuda118,
        Cuda121,
        Cuda124,
        Cuda128,
        Rocm56,
        Rocm60
    }

    /// <summary>
    /// Hardware/VRAM configuration
    /// </summary>
    public class HardwareSettings
    {
        public VramProfile VramProfile { get; set; } = VramProfile.GB_16;
        public int CustomVramMB { get; set; } = 0; // If custom amount
        public List<string> GgufPreferences { get; set; } = new();
        public ModelPrecision DefaultPrecision { get; set; } = ModelPrecision.FP16;
        public bool UseGpu { get; set; } = true;
        public int GpuIndex { get; set; } = 0;
    }

    public enum VramProfile
    {
        GB_4,
        GB_6,
        GB_8,
        GB_12,
        GB_16,
        GB_24,
        GB_32,
        GB_48,
        Custom
    }

    public enum ModelPrecision
    {
        FP32,
        FP16,
        BF16,
        INT8,
        INT4
    }

    /// <summary>
    /// Installation paths configuration
    /// </summary>
    public class PathSettings
    {
        [Required]
        public string RootDirectory { get; set; } = @"C:\ComfyUI";
        public string LogFile { get; set; } = "install.log";

        // Model directories (relative to RootDirectory if not absolute)
        public Dictionary<string, string> ModelPaths { get; set; } = new()
        {
            ["checkpoints"] = "models/checkpoints",
            ["loras"] = "models/loras",
            ["vae"] = "models/vae",
            ["text_encoders"] = "models/text_encoders",
            ["clip_vision"] = "models/clip_vision",
            ["diffusion_models"] = "models/diffusion_models",
            ["embeddings"] = "models/embeddings",
            ["controlnet"] = "models/controlnet",
            ["upscale_models"] = "models/upscale_models"
        };

        public string CustomNodesPath { get; set; } = "custom_nodes";
        public string OutputPath { get; set; } = "output";
        public string InputPath { get; set; } = "input";
        public string TempPath { get; set; } = "temp";
    }

    public enum ModelSource
    {
        HuggingFace,
        CivitAI,
        DirectUrl,
        LocalFile,
        GitLfs
    }

    public enum ModelType
    {
        Checkpoint,
        Lora,
        Vae,
        TextEncoder,
        ClipVision,
        Diffusion,
        Embedding,
        ControlNet,
        Upscaler,
        Other
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }
}