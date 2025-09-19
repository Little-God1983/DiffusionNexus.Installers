using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIKnowledge2Go.Installers.Core.Installation;
using AIKnowledge2Go.Installers.Core.Logging;
using AIKnowledge2Go.Installers.Core.Manifests;
using AIKnowledge2Go.Installers.Core.Settings;
using AIKnowledge2Go.Installers.UI.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIKnowledge2Go.Installers.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IManifestProvider _manifestProvider;
    private readonly IInstallerEngine _installerEngine;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IDialogService _dialogService;
    private readonly InMemoryLogSink _inMemoryLog = new();
    private readonly StringBuilder _logBuilder = new();
    private UserSettings _settings = new();
    private string? _logFilePath;
    private IReadOnlyCollection<string> _defaultOptionalSteps = Array.Empty<string>();

    public MainWindowViewModel(
        IManifestProvider manifestProvider,
        IInstallerEngine installerEngine,
        IUserSettingsService userSettingsService,
        IDialogService dialogService)
    {
        _manifestProvider = manifestProvider;
        _installerEngine = installerEngine;
        _userSettingsService = userSettingsService;
        _dialogService = dialogService;

        _manifestProvider.ManifestsChanged += OnManifestsChanged;
        _inMemoryLog.MessageLogged += OnLogMessageLogged;

        BrowseCommand = new AsyncRelayCommand(BrowseForInstallRootAsync, () => !IsInstalling);
        InstallCommand = new AsyncRelayCommand(BeginInstallAsync, CanInstall);
        ExportLogCommand = new AsyncRelayCommand(ExportLogAsync, CanExportLog);
    }

    public ObservableCollection<ManifestDisplayItem> AvailableManifests { get; } = new();

    public ObservableCollection<VramProfileItem> AvailableVramProfiles { get; } = new();

    public IAsyncRelayCommand BrowseCommand { get; }

    public IAsyncRelayCommand InstallCommand { get; }

    public IAsyncRelayCommand ExportLogCommand { get; }

    [ObservableProperty]
    private ManifestDisplayItem? selectedManifest;

    [ObservableProperty]
    private string? installRoot;

    [ObservableProperty]
    private bool isInstalling;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private bool isProgressIndeterminate = true;

    [ObservableProperty]
    private string progressStep = "Idle";

    [ObservableProperty]
    private string logText = string.Empty;

    [ObservableProperty]
    private string? selectedVramProfileId;

    public async Task InitializeAsync()
    {
        UpdateManifests(_manifestProvider.Manifests);
        _settings = await _userSettingsService.LoadAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_settings.LastInstallRoot))
        {
            InstallRoot = _settings.LastInstallRoot;
        }

        if (_settings.LastManifestId is not null)
        {
            SelectedManifest = AvailableManifests.FirstOrDefault(m => m.Descriptor.Manifest.Id == _settings.LastManifestId)
                ?? AvailableManifests.FirstOrDefault();
        }
        else if (AvailableManifests.Count > 0)
        {
            SelectedManifest = AvailableManifests[0];
        }

        if (!string.IsNullOrWhiteSpace(_settings.LastSelectedVramProfile) &&
            AvailableVramProfiles.Any(p => p.Id.Equals(_settings.LastSelectedVramProfile, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedVramProfileId = _settings.LastSelectedVramProfile;
        }
        else if (AvailableVramProfiles.Count > 0)
        {
            SelectedVramProfileId = AvailableVramProfiles[0].Id;
        }
    }

    private async Task BrowseForInstallRootAsync()
    {
        var folder = await _dialogService.BrowseForInstallRootAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        InstallRoot = folder;
    }

    private async Task BeginInstallAsync()
    {
        if (SelectedManifest is null)
        {
            await _dialogService.ShowMessageAsync("Install", "Please choose an install manifest before continuing.").ConfigureAwait(false);
            return;
        }

        if (!IsInstallRootValid(InstallRoot))
        {
            await _dialogService.ShowMessageAsync("Install", "Choose a valid install folder before continuing.").ConfigureAwait(false);
            return;
        }

        var manifest = SelectedManifest.Descriptor.Manifest;
        var installRoot = InstallRoot!;

        _inMemoryLog.Clear();
        _logBuilder.Clear();
        LogText = string.Empty;

        var timestamp = DateTimeOffset.Now;
        _logFilePath = Path.Combine(installRoot, $"install-{timestamp:yyyyMMdd-HHmmss}.log");

        var logSinks = new List<ILogSink>();
        logSinks.Add(_inMemoryLog);
        FileLogSink? fileSink = null;

        try
        {
            fileSink = new FileLogSink(_logFilePath);
            logSinks.Add(fileSink);
        }
        catch (Exception ex)
        {
            _logFilePath = null;
            await _dialogService.ShowMessageAsync("Logging", $"Failed to create log file: {ex.Message}").ConfigureAwait(false);
        }

        var compositeLogSink = new CompositeLogSink(logSinks);

        IsInstalling = true;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        ProgressStep = "Preparing";

        var enabledOptionalSteps = _defaultOptionalSteps;

        var progress = new Progress<InstallProgress>(p =>
        {
            ProgressPercent = p.Percent;
            IsProgressIndeterminate = p.IsIndeterminate;
            ProgressStep = p.Step;
        });

        var options = new InstallOptions
        {
            InstallRoot = installRoot,
            SelectedVramProfileId = SelectedVramProfileId,
            EnabledOptionalStepIds = enabledOptionalSteps,
        };

        try
        {
            var result = await _installerEngine.InstallAsync(manifest, options, progress, compositeLogSink).ConfigureAwait(false);
            if (result.Success)
            {
                await _dialogService.ShowMessageAsync("Install complete", "Installation finished successfully.").ConfigureAwait(false);
            }
            else
            {
                var message = result.ErrorMessage ?? "Installation failed.";
                await _dialogService.ShowMessageAsync("Install failed", message).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Install failed", ex.Message).ConfigureAwait(false);
            _inMemoryLog.Write(new LogMessage(LogLevel.Error, ex.Message, DateTimeOffset.Now, ex));
        }
        finally
        {
            fileSink?.Dispose();
            IsInstalling = false;
            IsProgressIndeterminate = false;
            InstallCommand.NotifyCanExecuteChanged();
            BrowseCommand.NotifyCanExecuteChanged();
            ExportLogCommand.NotifyCanExecuteChanged();
        }

        _settings = _settings with
        {
            LastInstallRoot = installRoot,
            LastManifestId = manifest.Id,
            LastSelectedVramProfile = SelectedVramProfileId,
        };

        await _userSettingsService.SaveAsync(_settings).ConfigureAwait(false);
    }

    private async Task ExportLogAsync()
    {
        if (string.IsNullOrWhiteSpace(_logFilePath) || !File.Exists(_logFilePath))
        {
            await _dialogService.ShowMessageAsync("Export log", "No log file has been created yet.").ConfigureAwait(false);
            return;
        }

        var destination = await _dialogService.ExportLogAsync(Path.GetFileName(_logFilePath)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        try
        {
            File.Copy(_logFilePath, destination, overwrite: true);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync("Export log", ex.Message).ConfigureAwait(false);
        }
    }

    private bool CanInstall()
    {
        return !IsInstalling && SelectedManifest is not null && IsInstallRootValid(InstallRoot);
    }

    private bool CanExportLog()
    {
        return !IsInstalling && !string.IsNullOrWhiteSpace(_logFilePath) && File.Exists(_logFilePath);
    }

    private void OnLogMessageLogged(object? sender, LogMessage e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logBuilder.AppendLine(e.ToString());
            LogText = _logBuilder.ToString();
            ExportLogCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnManifestsChanged(object? sender, ManifestsChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => UpdateManifests(e.Manifests));
    }

    private void UpdateManifests(IReadOnlyList<ManifestDescriptor> descriptors)
    {
        var selectedId = SelectedManifest?.Descriptor.Manifest.Id ?? _settings.LastManifestId;
        AvailableManifests.Clear();
        foreach (var descriptor in descriptors)
        {
            AvailableManifests.Add(new ManifestDisplayItem(descriptor));
        }

        ManifestDisplayItem? nextSelection = null;
        if (selectedId is not null)
        {
            nextSelection = AvailableManifests.FirstOrDefault(m => m.Descriptor.Manifest.Id == selectedId);
        }

        if (nextSelection is null && AvailableManifests.Count > 0)
        {
            nextSelection = AvailableManifests[0];
        }

        SelectedManifest = nextSelection;
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnInstallRootChanged(string? value)
    {
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInstallingChanged(bool value)
    {
        InstallCommand.NotifyCanExecuteChanged();
        BrowseCommand.NotifyCanExecuteChanged();
        ExportLogCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedManifestChanged(ManifestDisplayItem? value)
    {
        AvailableVramProfiles.Clear();
        _defaultOptionalSteps = Array.Empty<string>();

        if (value?.Descriptor.Manifest.VramProfiles is { Count: > 0 } vramProfiles)
        {
            foreach (var profile in vramProfiles)
            {
                AvailableVramProfiles.Add(new VramProfileItem(profile));
            }
        }

        if (value?.Descriptor.Manifest.OptionalSteps is { Count: > 0 } optionalSteps)
        {
            _defaultOptionalSteps = optionalSteps
                .Where(step => step.EnabledByDefault)
                .Select(step => step.Id)
                .ToArray();
        }

        if (AvailableVramProfiles.Count > 0)
        {
            if (SelectedVramProfileId is null ||
                !AvailableVramProfiles.Any(p => p.Id.Equals(SelectedVramProfileId, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedVramProfileId = AvailableVramProfiles[0].Id;
            }
        }
        else
        {
            SelectedVramProfileId = null;
        }

        InstallCommand.NotifyCanExecuteChanged();
    }

    private static bool IsInstallRootValid(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record ManifestDisplayItem(ManifestDescriptor Descriptor)
{
    public string Title => Descriptor.Manifest.Title;

    public override string ToString() => Title;
}

public sealed record VramProfileItem(VramProfile Profile)
{
    public string Id => Profile.Id;

    public string Label => Profile.Label;

    public override string ToString() => Label;
}
