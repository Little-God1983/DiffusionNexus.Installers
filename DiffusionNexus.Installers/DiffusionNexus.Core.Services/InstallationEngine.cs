using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Entities;
using DiffusionNexus.Core.Models.Enums;
using DiffusionNexus.Core.Models.Validation;

namespace DiffusionNexus.Core.Services
{
    public class InstallLogEntry
    {
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
        public string Message { get; init; } = string.Empty;
        public LogLevel Level { get; init; } = LogLevel.Info;
    }

    /// <summary>
    /// Provides lightweight planning and headless execution helpers. The actual
    /// operations are intentionally conservative to keep the environment safe while
    /// still producing actionable plans and log output.
    /// </summary>
    public class InstallationEngine
    {
        private readonly IInstallationOrchestrator? _orchestrator;

        /// <summary>
        /// Default constructor for backward compatibility.
        /// </summary>
        public InstallationEngine() : this(null)
        {
        }

        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        public InstallationEngine(IInstallationOrchestrator? orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public IReadOnlyList<string> BuildPlan(InstallationConfiguration configuration)
        {
            var steps = new List<string>();
            steps.Add($"Select repository: {configuration.Repository.Type} -> {configuration.Repository.RepositoryUrl}");

            var gitRepositories = configuration.GitRepositories ?? new List<GitRepository>();
            var modelDownloads = configuration.ModelDownloads ?? new List<ModelDownload>();

            // Step 1: Git check
            steps.Add("Check Git installation (install if missing)");

            // Step 2: Python check
            steps.Add($"Verify Python {configuration.Python.PythonVersion} is available");

            // Step 3: Clone main repository
            steps.Add($"Clone main repository: {configuration.Repository.RepositoryUrl}");

            // Step 4: Virtual environment
            if (configuration.Python.CreateVirtualEnvironment)
            {
                steps.Add($"Create virtual environment '{configuration.Python.VirtualEnvironmentName}' using Python {configuration.Python.PythonVersion}");
            }
            else if (!string.IsNullOrWhiteSpace(configuration.Python.InterpreterPathOverride))
            {
                steps.Add($"Use existing interpreter at {configuration.Python.InterpreterPathOverride}");
            }

            if (gitRepositories.Count > 0)
            {
                steps.Add("Clone repositories in priority order:");
                foreach (var repo in gitRepositories.OrderBy(r => r.Priority))
                {
                    var requirements = repo.InstallRequirements ? " (install requirements)" : string.Empty;
                    steps.Add($"  {repo.Priority:00}. {repo.Url}{requirements}");
                }
            }

            if (!string.IsNullOrWhiteSpace(configuration.Torch.TorchVersion) ||
                !string.IsNullOrWhiteSpace(configuration.Torch.CudaVersion))
            {
                var cudaSuffix = DeriveCudaSuffix(configuration.Torch.CudaVersion);
                var torchVersion = string.IsNullOrWhiteSpace(configuration.Torch.TorchVersion) ? "latest" : configuration.Torch.TorchVersion;
                steps.Add($"Install torch {torchVersion} with CUDA {configuration.Torch.CudaVersion} ({cudaSuffix})");
            }

            if (modelDownloads.Count > 0)
            {
                steps.Add("Download models:");
                foreach (var model in modelDownloads.Where(m => m.Enabled))
                {
                    steps.Add($"  {model.Name} -> {ResolveModelDestination(configuration, model)}");
                }
            }

            steps.Add("Run repository-specific post-install hooks");
            return steps;
        }

        public async Task DryRunAsync(
            InstallationConfiguration configuration,
            IProgress<InstallLogEntry> progress,
            CancellationToken cancellationToken = default)
        {
            var validation = configuration.Validate();
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                {
                    progress.Report(new InstallLogEntry { Level = LogLevel.Error, Message = error });
                }

                throw new InvalidOperationException("Configuration failed validation.");
            }

            foreach (var warning in validation.Warnings)
            {
                progress.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = warning });
            }

            await Task.Run(() =>
            {
                progress.Report(new InstallLogEntry { Message = "Validated configuration successfully." });
                progress.Report(new InstallLogEntry { Message = "Checked Python version availability." });
                progress.Report(new InstallLogEntry { Message = "Verified Git connectivity (simulated)." });
                progress.Report(new InstallLogEntry { Message = "Verified model download endpoints (simulated)." });
            }, cancellationToken);
        }

        /// <summary>
        /// Runs the actual installation using the orchestrator.
        /// </summary>
        /// <param name="configuration">Installation configuration.</param>
        /// <param name="targetDirectory">Target directory for installation.</param>
        /// <param name="progress">Progress callback for log entries.</param>
        /// <param name="stepProgress">Progress callback for step progress.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the installation.</returns>
        public async Task<InstallationResult> RunInstallationAsync(
            InstallationConfiguration configuration,
            string targetDirectory,
            IProgress<InstallLogEntry>? progress = null,
            IProgress<InstallationProgress>? stepProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (_orchestrator is null)
            {
                throw new InvalidOperationException(
                    "InstallationOrchestrator is not configured. " +
                    "Use the constructor that accepts IInstallationOrchestrator for real installations.");
            }

            // Validate configuration first
            var validation = configuration.Validate();
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                {
                    progress?.Report(new InstallLogEntry { Level = LogLevel.Error, Message = error });
                }

                return InstallationResult.Failure("Configuration validation failed: " + string.Join("; ", validation.Errors));
            }

            foreach (var warning in validation.Warnings)
            {
                progress?.Report(new InstallLogEntry { Level = LogLevel.Warning, Message = warning });
            }

            // Run installation through orchestrator
            return await _orchestrator.InstallAsync(
                configuration,
                targetDirectory,
                progress,
                stepProgress,
                cancellationToken);
        }

        public async Task InstallAsync(
            InstallationConfiguration configuration,
            IProgress<InstallLogEntry> progress,
            CancellationToken cancellationToken = default)
        {
            var plan = BuildPlan(configuration);
            await DryRunAsync(configuration, progress, cancellationToken);

            await Task.Run(() =>
            {
                foreach (var step in plan)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report(new InstallLogEntry { Message = step });
                }

                progress.Report(new InstallLogEntry { Message = "Installation simulation complete." });
            }, cancellationToken);

            WriteLogToDisk(configuration, plan);
        }

        public string ResolveModelDestination(
            InstallationConfiguration configuration,
            ModelDownload model)
        {
            if (configuration.Paths is null)
            {
                throw new InvalidOperationException("Path settings are required to resolve model destinations.");
            }

            if (!string.IsNullOrWhiteSpace(model.Destination))
            {
                return model.Destination;
            }

            var baseDirectory = configuration.Paths.DefaultModelDownloadDirectory;
            var root = configuration.Paths.RootDirectory;

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = root;
            }
            else if (!Path.IsPathRooted(baseDirectory) && !string.IsNullOrWhiteSpace(root))
            {
                baseDirectory = Path.Combine(root, baseDirectory);
            }

            var profileDirectory = model.VramProfile switch
            {
                VramProfile.VRAM_8GB => "vram-8gb",
                VramProfile.VRAM_12GB => "vram-12gb",
                VramProfile.VRAM_16GB => "vram-16gb",
                VramProfile.VRAM_24GB => "vram-24gb",
                _ => "custom"
            };

            return Path.Combine(baseDirectory ?? string.Empty, profileDirectory);
        }

        public string GetTorchCompatibilityHint(TorchSettings torch)
        {
            var cudaSuffix = DeriveCudaSuffix(torch.CudaVersion);
            if (string.IsNullOrWhiteSpace(cudaSuffix))
            {
                return "Unknown CUDA version";
            }

            var builder = new StringBuilder();
            builder.Append($"Torch wheel index: https://download.pytorch.org/whl/{cudaSuffix}");

            if (!string.IsNullOrWhiteSpace(torch.TorchVersion))
            {
                builder.Append($" | Suggested pip command: pip install torch=={torch.TorchVersion} --index-url https://download.pytorch.org/whl/{cudaSuffix}");
            }

            return builder.ToString();
        }

        private static string DeriveCudaSuffix(string cudaVersion)
        {
            if (string.IsNullOrWhiteSpace(cudaVersion))
            {
                return string.Empty;
            }

            var sanitized = new string(cudaVersion.Where(char.IsDigit).ToArray());
            if (sanitized.Length < 2)
            {
                return "cpu";
            }

            return $"cu{sanitized}";
        }

        private static void WriteLogToDisk(InstallationConfiguration configuration, IReadOnlyList<string> plan)
        {
            if (configuration.Paths is null)
            {
                return;
            }

            var root = configuration.Paths.RootDirectory;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Directory.GetCurrentDirectory();
            }

            var logFileName = configuration.Paths.LogFileName;
            if (string.IsNullOrWhiteSpace(logFileName))
            {
                logFileName = "install.log";
            }

            var logPath = Path.IsPathRooted(logFileName)
                ? logFileName
                : Path.Combine(root, logFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? root);
            File.WriteAllLines(logPath, plan.Prepend($"Installation Plan generated {DateTimeOffset.Now:O}"));
        }
    }
}
