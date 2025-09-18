using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Installer.Core.Logging;
using Installer.Core.Manifests;

namespace Installer.Core.Installation;

public sealed class InstallerEngine : IInstallerEngine
{
    public async Task<InstallResult> InstallAsync(
        InstallRequest request,
        IProgress<InstallProgress>? progress,
        ILogSink logSink,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        var stopwatch = Stopwatch.StartNew();
        var logFilePath = PrepareLogFilePath(request);
        FileLogSink? fileSink = null;
        ILogSink effectiveSink = logSink;

        try
        {
            if (!string.IsNullOrWhiteSpace(logFilePath))
            {
                fileSink = new FileLogSink(logFilePath);
                effectiveSink = new CompositeLogSink(logSink, fileSink);
            }
        }
        catch (Exception ex)
        {
            var pathForLog = logFilePath ?? "(unspecified)";
            logSink.Warn("Unable to create log file at {0}: {1}", pathForLog, ex.Message);
            logFilePath = null;
            fileSink?.Dispose();
            fileSink = null;
            effectiveSink = logSink;
        }

        try
        {
            var context = new InstallContext(request, effectiveSink);

            effectiveSink.Info("Starting install for {0}", context.Request.Manifest.Title);
            effectiveSink.Verbose("Install root: {0}", context.RootDirectory);

            var stages = BuildStages(context);
            for (var index = 0; index < stages.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stage = stages[index];
                var stageContext = new StageExecutionContext(stage.Name, index, stages.Count, progress);
                stageContext.Report(0, stage.IsIndeterminate);
                await stage.RunAsync(context, stageContext, cancellationToken).ConfigureAwait(false);
                stageContext.Report(100, false);
            }

            effectiveSink.Info("Installation completed successfully.");
            stopwatch.Stop();
            return InstallResult.Successful(stopwatch.Elapsed, logFilePath);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            effectiveSink.Warn("Installation cancelled.");
            return InstallResult.CancelledResult(stopwatch.Elapsed, logFilePath);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            effectiveSink.Error("Installation failed: {0}", ex.Message);
            effectiveSink.Verbose(ex.ToString());
            return InstallResult.Failed(stopwatch.Elapsed, ex, logFilePath);
        }
        finally
        {
            fileSink?.Dispose();
        }
    }

    private static string? PrepareLogFilePath(InstallRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LogFilePath))
        {
            return request.LogFilePath;
        }

        if (string.IsNullOrWhiteSpace(request.InstallRoot))
        {
            return null;
        }

        var root = Path.GetFullPath(request.InstallRoot);
        Directory.CreateDirectory(root);
        var name = $"install-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine(root, name);
    }

    private static IReadOnlyList<InstallStage> BuildStages(InstallContext context)
    {
        var stages = new List<InstallStage>
        {
            new("Prepare", true, PrepareAsync),
            new("Base software", false, InstallBaseSoftwareAsync),
            new("Dependencies", false, ResolveDependenciesAsync),
            new("Models", false, FetchModelsAsync),
            new("Extensions", false, InstallExtensionsAsync),
            new("Optional steps", false, ExecuteOptionalStepsAsync)
        };

        return stages;
    }

    private static Task PrepareAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        stageContext.Report(5, true);
        context.Log.Info("Preparing installation directories...");

        var baseSoftware = context.Request.Manifest.BaseSoftware;
        if (baseSoftware is not null && !string.IsNullOrWhiteSpace(baseSoftware.Target))
        {
            var targetPath = context.CombineWithRoot(baseSoftware.Target);
            Directory.CreateDirectory(targetPath);
            context.Log.Verbose("Ensured base software directory: {0}", targetPath);
        }

        stageContext.Report(100, false);
        return Task.CompletedTask;
    }

    private static Task InstallBaseSoftwareAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        var baseSoftware = context.Request.Manifest.BaseSoftware;
        if (baseSoftware is null)
        {
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        stageContext.Report(5, false);
        if (!string.IsNullOrWhiteSpace(baseSoftware.RepositoryUrl))
        {
            context.Log.Info("Fetching base software {0} from {1}", baseSoftware.Name, baseSoftware.RepositoryUrl);
            if (!string.IsNullOrWhiteSpace(baseSoftware.Ref))
            {
                context.Log.Verbose("Requested ref: {0}", baseSoftware.Ref);
            }
        }
        else
        {
            context.Log.Info("Setting up base software {0}", baseSoftware.Name);
        }

        var targetPath = context.CombineWithRoot(string.IsNullOrWhiteSpace(baseSoftware.Target)
            ? baseSoftware.Name
            : baseSoftware.Target);
        Directory.CreateDirectory(targetPath);
        context.Log.Verbose("Base software target directory prepared at {0}", targetPath);
        stageContext.Report(100, false);
        return Task.CompletedTask;
    }

    private static Task ResolveDependenciesAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        var dependencies = context.Request.Manifest.Dependencies;
        if (dependencies is null)
        {
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(dependencies.Python))
        {
            context.Log.Info("Ensuring Python {0} is available", dependencies.Python);
        }

        if (!string.IsNullOrWhiteSpace(dependencies.Cuda))
        {
            context.Log.Info("Checking CUDA {0}", dependencies.Cuda);
        }

        if (dependencies.PipRequirements.Count > 0)
        {
            var total = dependencies.PipRequirements.Count;
            for (var index = 0; index < total; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = dependencies.PipRequirements[index];
                var resolvedPath = context.ResolvePath(item.RelativeTo, item.Path);
                context.Log.Info("Pip requirements: {0}", resolvedPath);
                stageContext.Report(((index + 1) / (double)total) * 100);
            }
        }
        else
        {
            stageContext.Report(100);
        }

        return Task.CompletedTask;
    }

    private static Task FetchModelsAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        var models = context.Request.Manifest.Models;
        if (models.Count == 0)
        {
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        for (var index = 0; index < models.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = models[index];
            var targetDirectory = context.CombineWithRoot(model.Target);
            Directory.CreateDirectory(targetDirectory);
            context.Log.Info("Preparing model {0}", model.Name);
            context.Log.Verbose("Source: {0} ({1})", model.Source, model.Repository ?? model.Match ?? "n/a");
            context.Log.Verbose("Target directory: {0}", targetDirectory);

            if (!string.IsNullOrWhiteSpace(model.PreferExpression) && context.SelectedVramProfile is not null)
            {
                var profile = context.SelectedVramProfile;
                context.Log.Info("VRAM profile '{0}' selected for models.", profile!.Label);
                if (profile.GgufPreference.Count > 0)
                {
                    context.Log.Verbose("Preference order: {0}", string.Join(", ", profile.GgufPreference));
                }
            }

            var stagePercent = ((index + 1) / (double)models.Count) * 100;
            stageContext.Report(stagePercent, false);
        }

        return Task.CompletedTask;
    }

    private static Task InstallExtensionsAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        var extensions = context.Request.Manifest.Extensions;
        if (extensions.Count == 0)
        {
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        for (var index = 0; index < extensions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = extensions[index];
            var targetDirectory = context.CombineWithRoot(extension.Target);
            Directory.CreateDirectory(targetDirectory);
            context.Log.Info("Installing extension {0}", extension.Name);
            context.Log.Verbose("Source repository: {0}", extension.Repository);
            context.Log.Verbose("Target directory: {0}", targetDirectory);
            stageContext.Report(((index + 1) / (double)extensions.Count) * 100, false);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteOptionalStepsAsync(InstallContext context, StageExecutionContext stageContext, CancellationToken cancellationToken)
    {
        var steps = context.Request.Manifest.OptionalSteps;
        if (steps.Count == 0)
        {
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        var selectedSteps = DetermineOptionalSteps(context.Request, steps);
        if (selectedSteps.Count == 0)
        {
            context.Log.Info("No optional steps selected.");
            stageContext.Report(100, false);
            return Task.CompletedTask;
        }

        for (var index = 0; index < selectedSteps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = selectedSteps[index];
            context.Log.Info("Queued optional step: {0}", step.Description);
            if (!string.IsNullOrWhiteSpace(step.WorkingDirectory))
            {
                var workingDir = context.ResolvePath(null, step.WorkingDirectory);
                context.Log.Verbose("Working directory: {0}", workingDir);
            }

            context.Log.Verbose("Shell command: {0}", step.Shell);
            stageContext.Report(((index + 1) / (double)selectedSteps.Count) * 100, false);
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<OptionalStepConfig> DetermineOptionalSteps(
        InstallRequest request,
        IReadOnlyList<OptionalStepConfig> steps)
    {
        if (request.EnabledOptionalStepIds.Count == 0)
        {
            return steps.Where(step => step.EnabledByDefault).ToList();
        }

        return steps
            .Where(step => request.EnabledOptionalStepIds.Contains(step.Id))
            .ToList();
    }

    private sealed record InstallStage(string Name, bool IsIndeterminate, Func<InstallContext, StageExecutionContext, CancellationToken, Task> RunAsync);

    private sealed class StageExecutionContext
    {
        private readonly string _stageName;
        private readonly int _stageIndex;
        private readonly int _totalStages;
        private readonly IProgress<InstallProgress>? _progress;

        public StageExecutionContext(string stageName, int stageIndex, int totalStages, IProgress<InstallProgress>? progress)
        {
            _stageName = stageName;
            _stageIndex = stageIndex;
            _totalStages = Math.Max(1, totalStages);
            _progress = progress;
        }

        public void Report(double stagePercent, bool isIndeterminate = false)
        {
            if (_progress is null)
            {
                return;
            }

            stagePercent = Math.Clamp(stagePercent, 0, 100);
            var rangeStart = (_stageIndex / (double)_totalStages) * 100;
            var rangeEnd = ((_stageIndex + 1) / (double)_totalStages) * 100;
            var absolutePercent = rangeStart + (rangeEnd - rangeStart) * (stagePercent / 100d);
            _progress.Report(new InstallProgress(_stageName, absolutePercent, isIndeterminate));
        }
    }
}
