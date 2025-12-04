using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DiffusionNexus.Installers.ViewModels;

/// <summary>
/// Service interface for folder picker interaction.
/// </summary>
public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// ViewModel for the Installation tab.
/// </summary>
public partial class InstallationViewModel : ViewModelBase
{
    private IFolderPickerService? _folderPickerService;

    public InstallationViewModel()
    {
        LogEntries = [];
    }

    /// <summary>
    /// Attaches the folder picker service for folder selection.
    /// </summary>
    public void AttachFolderPickerService(IFolderPickerService folderPickerService) =>
        _folderPickerService = folderPickerService;

    #region Observable Properties

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartInstallationCommand))]
    private string _targetInstallFolder = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMaximum = 100;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _statusMessage = "Ready to install";

    public ObservableCollection<InstallationLogEntry> LogEntries { get; }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task BrowseTargetFolderAsync(CancellationToken cancellationToken)
    {
        if (_folderPickerService is null) return;

        var path = await _folderPickerService.PickFolderAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            TargetInstallFolder = path;
            AddLogEntry("Target folder selected: " + path, LogEntryLevel.Info);
        }
    }

    private bool CanStartInstallation() =>
        !string.IsNullOrWhiteSpace(TargetInstallFolder) && !IsInstalling;

    [RelayCommand(CanExecute = nameof(CanStartInstallation))]
    private async Task StartInstallationAsync(CancellationToken cancellationToken)
    {
        if (IsInstalling) return;

        IsInstalling = true;
        ProgressValue = 0;
        StatusMessage = "Starting installation...";
        StartInstallationCommand.NotifyCanExecuteChanged();

        try
        {
            AddLogEntry("Installation started", LogEntryLevel.Info);
            AddLogEntry($"Target folder: {TargetInstallFolder}", LogEntryLevel.Info);

            // Placeholder for actual installation logic
            for (var i = 0; i <= 100; i += 10)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(200, cancellationToken);
                ProgressValue = i;
                StatusMessage = $"Installing... {i}%";
            }

            AddLogEntry("Installation completed successfully!", LogEntryLevel.Success);
            StatusMessage = "Installation completed!";
        }
        catch (OperationCanceledException)
        {
            AddLogEntry("Installation cancelled", LogEntryLevel.Warning);
            StatusMessage = "Installation cancelled";
        }
        catch (Exception ex)
        {
            AddLogEntry($"Installation failed: {ex.Message}", LogEntryLevel.Error);
            StatusMessage = "Installation failed";
        }
        finally
        {
            IsInstalling = false;
            StartInstallationCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void OpenYoutube()
    {
        OpenUrl("https://youtube.com/@AIKnowledge2Go");
    }

    [RelayCommand]
    private void OpenCivitai()
    {
        OpenUrl("https://civitai.com/user/AIknowlege2go");
    }

    [RelayCommand]
    private void OpenPatreon()
    {
        OpenUrl("https://patreon.com/AIKnowledgeCentral?utm_medium=unknown&utm_source=join_link&utm_campaign=creatorshare_creator&utm_content=copyLink");
    }

    #endregion

    #region Private Helpers

    private void AddLogEntry(string message, LogEntryLevel level)
    {
        LogEntries.Add(new InstallationLogEntry(message, level));
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // Silently fail if browser cannot be opened
        }
    }

    #endregion
}

/// <summary>
/// Log entry level for color-coded display.
/// </summary>
public enum LogEntryLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Represents a single log entry in the installation log.
/// </summary>
public class InstallationLogEntry
{
    public InstallationLogEntry(string message, LogEntryLevel level)
    {
        Timestamp = DateTimeOffset.Now;
        Message = message;
        Level = level;
    }

    public DateTimeOffset Timestamp { get; }
    public string Message { get; }
    public LogEntryLevel Level { get; }

    public string Display => $"[{Timestamp:HH:mm:ss}] {Message}";

    public IBrush ForegroundBrush => Level switch
    {
        LogEntryLevel.Success => Brushes.Green,
        LogEntryLevel.Warning => Brushes.Orange,
        LogEntryLevel.Error => Brushes.Red,
        _ => Brushes.White
    };
}
