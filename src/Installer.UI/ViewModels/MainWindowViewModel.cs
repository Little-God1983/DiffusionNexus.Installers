using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Installer.Core.Installation;
using Installer.Core.Logging;
using Installer.Core.Manifests;
using Installer.Core.Settings;
using Installer.UI.Commands;
using Installer.UI.Services;

namespace Installer.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IManifestProvider _manifestProvider;
    private readonly IInstallerEngine _installerEngine;
    private readonly IUserSettingsService _settingsService;
    private readonly IUserInteractionService _interactionService;
    private readonly ObservableCollection<ManifestItemViewModel> _availableManifests = new();
    private readonly ObservableCollection<VramProfileViewModel> _availableVramProfiles = new();
    private readonly StringBuilder _logBuilder = new();
    private readonly object _logLock = new();

    private ManifestItemViewModel? _selectedManifest;
    private VramProfileViewModel? _selectedVramProfile;
    private string _installRoot = string.Empty;
    private double _progressPercent;
    private bool _isProgressIndeterminate = true;
    private bool _isInstalling;
    private string _currentStep = "Idle";
    private string _statusMessage = "Ready.";
    private bool _hasError;
    private string _logText = string.Empty;
    private string? _logFilePath;
    private bool _telemetryOptIn;
    private UserSettings _userSettings = UserSettings.Default;
    private BufferingLogSink? _bufferingLogSink;
    private IBrush _statusMessageBrush = default!;

    public MainWindowViewModel(
        IManifestProvider manifestProvider,
        IInstallerEngine installerEngine,
        IUserSettingsService settingsService,
        IUserInteractionService interactionService)
    {
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _installerEngine = installerEngine ?? throw new ArgumentNullException(nameof(installerEngine));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));

        BrowseCommand = new AsyncCommand(BrowseAsync, () => !IsInstalling);
        InstallCommand = new AsyncCommand(InstallAsync, CanInstall);
        ExportLogCommand = new AsyncCommand(ExportLogAsync, () => !string.IsNullOrWhiteSpace(LogText));
        CopyLogCommand = new AsyncCommand(CopyLogAsync, () => !string.IsNullOrWhiteSpace(LogText));

        _manifestProvider.ManifestsChanged += OnManifestProviderChanged;

        StatusMessageBrush = GetDefaultStatusBrush();
    }

    public ObservableCollection<ManifestItemViewModel> AvailableManifests => _availableManifests;

    public ObservableCollection<VramProfileViewModel> AvailableVramProfiles => _availableVramProfiles;

    public ManifestItemViewModel? SelectedManifest
    {
        get => _selectedManifest;
        set
        {
            if (SetField(ref _selectedManifest, value))
            {
                UpdateVramProfiles(value);
                InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public VramProfileViewModel? SelectedVramProfile
    {
        get => _selectedVramProfile;
        set => SetField(ref _selectedVramProfile, value);
    }

    public string InstallRoot
    {
        get => _installRoot;
        set
        {
            if (SetField(ref _installRoot, value?.Trim() ?? string.Empty))
            {
                InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetField(ref _isProgressIndeterminate, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (SetField(ref _isInstalling, value))
            {
                BrowseCommand.RaiseCanExecuteChanged();
                InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CurrentStep
    {
        get => _currentStep;
        private set => SetField(ref _currentStep, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetField(ref _hasError, value))
            {
                UpdateStatusAppearance();
            }
        }
    }

    public string LogText
    {
        get => _logText;
        private set
        {
            if (SetField(ref _logText, value))
            {
                ExportLogCommand.RaiseCanExecuteChanged();
                CopyLogCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? LogFilePath
    {
        get => _logFilePath;
        private set => SetField(ref _logFilePath, value);
    }

    public AsyncCommand BrowseCommand { get; }

    public AsyncCommand InstallCommand { get; }

    public AsyncCommand ExportLogCommand { get; }

    public AsyncCommand CopyLogCommand { get; }

    public IBrush StatusMessageBrush
    {
        get => _statusMessageBrush;
        private set => SetField(ref _statusMessageBrush, value);
    }

    public bool TelemetryOptIn
    {
        get => _telemetryOptIn;
        set
        {
            if (SetField(ref _telemetryOptIn, value))
            {
                _userSettings.TelemetryOptIn = value;
                PersistSettingsSilently();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _userSettings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_userSettings.LastInstallDirectory))
        {
            InstallRoot = _userSettings.LastInstallDirectory!;
        }

        TelemetryOptIn = _userSettings.TelemetryOptIn;

        await ReloadManifestsAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool CanInstall()
    {
        return !IsInstalling
               && !string.IsNullOrWhiteSpace(InstallRoot)
               && SelectedManifest is not null;
    }

    private async Task ReloadManifestsAsync(CancellationToken cancellationToken)
    {
        var manifests = await _manifestProvider.GetManifestsAsync(cancellationToken).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var selectedId = SelectedManifest?.Descriptor.Manifest.Id;
            _availableManifests.Clear();
            foreach (var manifest in manifests)
            {
                _availableManifests.Add(new ManifestItemViewModel(manifest));
            }

            SelectedManifest = _availableManifests.FirstOrDefault(m => m.Descriptor.Manifest.Id == selectedId)
                                 ?? _availableManifests.FirstOrDefault();
        });
    }

    private void UpdateVramProfiles(ManifestItemViewModel? manifest)
    {
        _availableVramProfiles.Clear();
        if (manifest is null)
        {
            SelectedVramProfile = null;
            return;
        }

        foreach (var profile in manifest.Descriptor.Manifest.VramProfiles)
        {
            _availableVramProfiles.Add(new VramProfileViewModel(profile));
        }

        SelectedVramProfile = _availableVramProfiles.FirstOrDefault();
    }

    private async Task BrowseAsync()
    {
        var folder = await _interactionService.BrowseForFolderAsync(InstallRoot, CancellationToken.None).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            InstallRoot = folder!;
            await SaveSettingsAsync().ConfigureAwait(false);
        }
    }

    private async Task InstallAsync()
    {
        if (!CanInstall())
        {
            return;
        }

        var descriptor = SelectedManifest!.Descriptor;
        var selectedProfileId = SelectedVramProfile?.Profile.Id;
        var optionalSteps = Enumerable.Empty<string>();
        var request = new InstallRequest(
            descriptor,
            InstallRoot,
            selectedProfileId,
            optionalSteps,
            null);

        await SaveSettingsAsync().ConfigureAwait(false);

        StartLogCapture();
        ResetProgress();

        IsInstalling = true;
        HasError = false;
        StatusMessage = $"Installing {descriptor.Manifest.Title}...";

        var progress = new Progress<InstallProgress>(progressUpdate =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = Math.Clamp(progressUpdate.Percent, 0, 100);
                IsProgressIndeterminate = progressUpdate.IsIndeterminate;
                if (!string.IsNullOrWhiteSpace(progressUpdate.StepName))
                {
                    CurrentStep = progressUpdate.StepName;
                }
            });
        });

        try
        {
            var logSink = _bufferingLogSink ?? new BufferingLogSink();
            var result = await Task.Run(() => _installerEngine.InstallAsync(request, progress, logSink, CancellationToken.None))
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsInstalling = false;
                ProgressPercent = 100;
                IsProgressIndeterminate = false;
                LogFilePath = result.LogFilePath;

                if (result.Success)
                {
                    StatusMessage = !string.IsNullOrEmpty(result.LogFilePath)
                        ? $"Installation completed successfully. Log: {result.LogFilePath}"
                        : "Installation completed successfully.";
                    HasError = false;
                }
                else if (result.Cancelled)
                {
                    StatusMessage = "Installation cancelled.";
                    HasError = false;
                }
                else
                {
                    StatusMessage = result.Error?.Message ?? "Installation failed.";
                    HasError = true;
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsInstalling = false;
                HasError = true;
                StatusMessage = ex.Message;
            });
        }
    }

    private void StartLogCapture()
    {
        _logBuilder.Clear();
        LogText = string.Empty;

        if (_bufferingLogSink is not null)
        {
            _bufferingLogSink.MessageLogged -= OnLogMessageLogged;
        }
        _bufferingLogSink = new BufferingLogSink();
        _bufferingLogSink.MessageLogged += OnLogMessageLogged;
    }

    private void ResetProgress()
    {
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        CurrentStep = "Preparing";
    }

    private void OnLogMessageLogged(object? sender, LogMessage logMessage)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_logLock)
            {
                _logBuilder.AppendLine(logMessage.ToString());
                LogText = _logBuilder.ToString();
            }
        });
    }

    private async Task ExportLogAsync()
    {
        if (string.IsNullOrWhiteSpace(LogText))
        {
            return;
        }

        var suggestedFileName = SelectedManifest is null
            ? "install.log"
            : $"{SelectedManifest.Descriptor.Manifest.Id}-install.log";

        var path = await _interactionService.SaveLogFileAsync(suggestedFileName, CancellationToken.None).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path!, LogText, Encoding.UTF8).ConfigureAwait(false);
        StatusMessage = $"Log exported to {path}";
    }

    private async Task CopyLogAsync()
    {
        if (string.IsNullOrWhiteSpace(LogText))
        {
            return;
        }

        await _interactionService.SetClipboardTextAsync(LogText, CancellationToken.None).ConfigureAwait(false);
        StatusMessage = "Log copied to clipboard.";
    }

    private void UpdateStatusAppearance()
    {
        StatusMessageBrush = HasError
            ? new SolidColorBrush(Color.Parse("#C2504C"))
            : GetDefaultStatusBrush();
    }

    private static IBrush GetDefaultStatusBrush()
    {
        if (Application.Current?.Resources.TryGetResource("ThemeForegroundBrush", ThemeVariant.Default, out var resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    private async Task SaveSettingsAsync()
    {
        _userSettings.LastInstallDirectory = InstallRoot;
        await _settingsService.SaveAsync(_userSettings, CancellationToken.None).ConfigureAwait(false);
    }

    private void PersistSettingsSilently()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _settingsService.SaveAsync(_userSettings, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ignore persistence failures for background telemetry toggles.
            }
        });
    }

    private async void OnManifestProviderChanged(object? sender, EventArgs e)
    {
        try
        {
            await ReloadManifestsAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Ignore manifest reload errors to avoid breaking the UI.
        }
    }

    public void Dispose()
    {
        _manifestProvider.ManifestsChanged -= OnManifestProviderChanged;
        if (_bufferingLogSink is not null)
        {
            _bufferingLogSink.MessageLogged -= OnLogMessageLogged;
        }
    }

    public sealed class ManifestItemViewModel
    {
        public ManifestItemViewModel(ManifestDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public ManifestDescriptor Descriptor { get; }

        public string Title => Descriptor.Manifest.Title;

        public override string ToString() => Title;
    }

    public sealed class VramProfileViewModel
    {
        public VramProfileViewModel(VramProfileConfig profile)
        {
            Profile = profile;
        }

        public VramProfileConfig Profile { get; }

        public string Label => string.IsNullOrWhiteSpace(Profile.Label) ? Profile.Id : Profile.Label;

        public override string ToString() => Label;
    }
}
