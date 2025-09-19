using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIKnowledge2Go.Installers.Core.Logging;
using AIKnowledge2Go.Installers.Core.Manifests;

namespace AIKnowledge2Go.Installers.Core.Installation;

public sealed class InstallerEngine : IInstallerEngine
{
    public async Task<InstallResult> InstallAsync(
        InstallManifest manifest,
        InstallOptions options,
        IProgress<InstallProgress>? progress,
        ILogSink logSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logSink);
        ArgumentException.ThrowIfNullOrEmpty(options.InstallRoot);

        var steps = BuildSteps(manifest, options, logSink);
        var progressPercentPerStep = steps.Count == 0 ? 100d : 100d / steps.Count;
        var completedSteps = 0d;

        progress?.Report(new InstallProgress(0, "Preparing installation", true));
        logSink.Write(new LogMessage(LogLevel.Information, $"Starting installation for manifest '{manifest.Title}'", DateTimeOffset.Now));
        logSink.Write(new LogMessage(LogLevel.Information, $"Install root: {options.InstallRoot}", DateTimeOffset.Now));

        if (!string.IsNullOrWhiteSpace(options.SelectedVramProfileId))
        {
            logSink.Write(new LogMessage(LogLevel.Information, $"VRAM profile: {options.SelectedVramProfileId}", DateTimeOffset.Now));
        }

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new InstallProgress(Math.Clamp(completedSteps, 0, 99), step.Name, false));
            logSink.Write(new LogMessage(LogLevel.Information, $"→ {step.Name}", DateTimeOffset.Now));

            try
            {
                await step.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                completedSteps += progressPercentPerStep;
                logSink.Write(new LogMessage(LogLevel.Information, $"✓ {step.Name}", DateTimeOffset.Now));
            }
            catch (Exception ex)
            {
                logSink.Write(new LogMessage(LogLevel.Error, $"Step '{step.Name}' failed: {ex.Message}", DateTimeOffset.Now, ex));
                progress?.Report(new InstallProgress(completedSteps, $"Failed: {step.Name}", false));
                return InstallResult.Failed($"Step '{step.Name}' failed", ex);
            }
        }

        progress?.Report(new InstallProgress(100, "Completed", false, true));
        logSink.Write(new LogMessage(LogLevel.Information, "Installation completed successfully", DateTimeOffset.Now));
        return InstallResult.Completed();
    }

    private static List<InstallStep> BuildSteps(InstallManifest manifest, InstallOptions options, ILogSink log)
    {
        var steps = new List<InstallStep>();
        var installRoot = Path.GetFullPath(options.InstallRoot);

        steps.Add(new InstallStep("Create install root", _ =>
        {
            Directory.CreateDirectory(installRoot);
            return ValueTask.CompletedTask;
        }));

        steps.Add(new InstallStep($"Prepare base software ({manifest.BaseSoftware.Name})", async ct =>
        {
            var target = ResolveInstallPath(installRoot, manifest.BaseSoftware.Target);
            Directory.CreateDirectory(target);
            var infoFile = Path.Combine(target, "INSTALLER_INFO.txt");
            var content = new StringBuilder()
                .AppendLine($"Source: {manifest.BaseSoftware.Repository ?? manifest.BaseSoftware.Archive ?? "n/a"}")
                .AppendLine($"Reference: {manifest.BaseSoftware.Reference ?? "default"}")
                .AppendLine($"Generated: {DateTimeOffset.Now:u}")
                .ToString();
            await File.WriteAllTextAsync(infoFile, content, ct).ConfigureAwait(false);
        }));

        if (manifest.Dependencies is { } dependencies)
        {
            steps.Add(new InstallStep("Record dependency plan", async ct =>
            {
                var planPath = Path.Combine(installRoot, "dependencies-plan.txt");
                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(dependencies.Python))
                {
                    builder.AppendLine($"Python: {dependencies.Python}");
                }

                if (!string.IsNullOrWhiteSpace(dependencies.Cuda))
                {
                    builder.AppendLine($"CUDA: {dependencies.Cuda}");
                }

                if (dependencies.PipRequirements is { Count: > 0 })
                {
                    builder.AppendLine("Pip requirements:");
                    foreach (var requirement in dependencies.PipRequirements)
                    {
                        builder.AppendLine($"  - {requirement.Path} (relative to {requirement.RelativeTo ?? "install root"})");
                    }
                }

                await File.WriteAllTextAsync(planPath, builder.ToString(), ct).ConfigureAwait(false);
            }));
        }

        if (manifest.Models is { Count: > 0 } models)
        {
            foreach (var model in models)
            {
                steps.Add(new InstallStep($"Register model: {model.Name}", async ct =>
                {
                    var target = ResolveInstallPath(installRoot, model.Target);
                    Directory.CreateDirectory(target);
                    var metadataPath = Path.Combine(target, SanitizeFileName(model.Name) + ".model.txt");
                    var metadata = new StringBuilder()
                        .AppendLine($"Source: {model.Source ?? "n/a"}")
                        .AppendLine($"Repository: {model.Repository ?? "n/a"}")
                        .AppendLine($"Match: {model.Match ?? "n/a"}")
                        .AppendLine($"Prefer: {model.Prefer?.ToString() ?? "n/a"}")
                        .AppendLine($"Url: {model.Url ?? "n/a"}")
                        .ToString();
                    await File.WriteAllTextAsync(metadataPath, metadata, ct).ConfigureAwait(false);
                }));
            }
        }

        if (manifest.Extensions is { Count: > 0 } extensions)
        {
            foreach (var extension in extensions)
            {
                steps.Add(new InstallStep($"Register extension: {extension.Name}", async ct =>
                {
                    var target = ResolveInstallPath(installRoot, extension.Target);
                    Directory.CreateDirectory(target);
                    var metadataPath = Path.Combine(target, SanitizeFileName(extension.Name) + ".extension.txt");
                    var metadata = $"Repository: {extension.Repository}{Environment.NewLine}Created: {DateTimeOffset.Now:u}";
                    await File.WriteAllTextAsync(metadataPath, metadata, ct).ConfigureAwait(false);
                }));
            }
        }

        if (manifest.OptionalSteps is { Count: > 0 } optionalSteps)
        {
            var enabledSteps = options.EnabledOptionalStepIds ?? Array.Empty<string>();
            foreach (var step in optionalSteps)
            {
                if (!enabledSteps.Contains(step.Id, StringComparer.OrdinalIgnoreCase))
                {
                    log.Write(new LogMessage(LogLevel.Information, $"Skipping optional step {step.Id}", DateTimeOffset.Now));
                    continue;
                }

                steps.Add(new InstallStep($"Execute optional step: {step.Description}", async ct =>
                {
                    var optionalLogPath = Path.Combine(installRoot, "optional-steps.log");
                    var logLine = $"[{DateTimeOffset.Now:u}] Would execute: {step.Shell} (working dir: {step.WorkingDirectory ?? installRoot})";
                    await File.AppendAllTextAsync(optionalLogPath, logLine + Environment.NewLine, ct).ConfigureAwait(false);
                }));
            }
        }

        return steps;
    }

    private static string ResolveInstallPath(string root, string relative)
    {
        var normalisedRelative = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(root, normalisedRelative));
        var rootFull = Path.GetFullPath(root);

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!combined.StartsWith(rootFull, comparison))
        {
            throw new InvalidOperationException($"Path '{relative}' escapes the install root.");
        }

        return combined;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(invalid.Contains(ch) ? '-' : ch);
        }

        return builder.ToString();
    }

    private sealed record InstallStep(string Name, Func<CancellationToken, ValueTask> ExecuteAsync);
}
