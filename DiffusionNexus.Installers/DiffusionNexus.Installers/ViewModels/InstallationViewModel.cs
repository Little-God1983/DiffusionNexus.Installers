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
        AvailableVramProfiles = [];
        AvailableInstallationTypes = [];
        
        // Initialize with default VRAM profiles from shared constants
        SetAvailableVramProfiles(VramProfileConstants.DefaultProfiles);
        
        // Initialize installation types
        InitializeInstallationTypes();
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

    [ObservableProperty]
    private int _selectedVramProfile = VramProfileConstants.DefaultSelectedProfile;

    [ObservableProperty]
    private InstallationType _selectedInstallationType = InstallationType.FullInstall;

    public ObservableCollection<InstallationLogEntry> LogEntries { get; }

    public ObservableCollection<VramProfileOption> AvailableVramProfiles { get; }

    public ObservableCollection<InstallationTypeOption> AvailableInstallationTypes { get; }

    #endregion

    #region VRAM Profile Methods

    /// <summary>
    /// Sets the available VRAM profiles based on configuration.
    /// </summary>
    public void SetAvailableVramProfiles(int[] profiles)
    {
        AvailableVramProfiles.Clear();
        foreach (var profile in profiles)
        {
            var option = new VramProfileOption(profile, profile == SelectedVramProfile, SelectVramProfile);
            AvailableVramProfiles.Add(option);
        }
    }

    private void SelectVramProfile(int value)
    {
        SelectedVramProfile = value;
        
        // Update all profiles without triggering callbacks to prevent infinite loop
        foreach (var profile in AvailableVramProfiles)
        {
            profile.SetSelectedWithoutCallback(profile.Value == value);
        }
        
        AddLogEntry($"VRAM profile set to {value} GB", LogEntryLevel.Info);
    }

    #endregion

    #region Installation Type Methods

    /// <summary>
    /// Initializes the list of available installation types.
    /// </summary>
    private void InitializeInstallationTypes()
    {
        AvailableInstallationTypes.Clear();

        AvailableInstallationTypes.Add(new InstallationTypeOption(
            InstallationType.FullInstall, 
            "Full Install", 
            true, 
            SelectInstallationType));
        AvailableInstallationTypes.Add(new InstallationTypeOption(
            InstallationType.ModelsNodesOnly, 
            "Models/Nodes Only", 
            false, 
            SelectInstallationType));
    }

    private void SelectInstallationType(InstallationType type)
    {
        SelectedInstallationType = type;
        
        // Update all options without triggering callbacks to prevent infinite loop
        foreach (var option in AvailableInstallationTypes)
        {
            option.SetSelectedWithoutCallback(option.Type == type);
        }
        
        var displayName = type == InstallationType.FullInstall ? "Full Install" : "Models/Nodes Only";
        AddLogEntry($"Installation type set to {displayName}", LogEntryLevel.Info);
    }

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
            AddLogEntry($"VRAM Profile: {SelectedVramProfile} GB", LogEntryLevel.Info);
            AddLogEntry($"Installation Type: {SelectedInstallationType}", LogEntryLevel.Info);

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
/// Represents the type of installation to perform.
/// </summary>
public enum InstallationType
{
    FullInstall,
    ModelsNodesOnly
}

/// <summary>
/// Represents an installation type option for selection.
/// </summary>
public partial class InstallationTypeOption : ObservableObject
{
    private readonly Action<InstallationType>? _onSelected;
    private bool _suppressCallback;

    public InstallationTypeOption(InstallationType type, string displayName, bool isSelected = false, Action<InstallationType>? onSelected = null)
    {
        Type = type;
        DisplayName = displayName;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }

    public InstallationType Type { get; }
    
    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppressCallback && value)
        {
            _onSelected?.Invoke(Type);
        }
    }

    /// <summary>
    /// Sets the selected state without triggering the callback.
    /// </summary>
    public void SetSelectedWithoutCallback(bool selected)
    {
        _suppressCallback = true;
        IsSelected = selected;
        _suppressCallback = false;
    }
}

/// <summary>
/// Represents a VRAM profile option for selection.
/// </summary>
public partial class VramProfileOption : ObservableObject
{
    private readonly Action<int>? _onSelected;
    private bool _suppressCallback;

    public VramProfileOption(int value, bool isSelected = false, Action<int>? onSelected = null)
    {
        Value = value;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }

    public int Value { get; }
    
    public string DisplayName => $"{Value} GB";

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppressCallback)
        {
            _onSelected?.Invoke(Value);
        }
    }

    /// <summary>
    /// Sets the selected state without triggering the callback.
    /// </summary>
    public void SetSelectedWithoutCallback(bool selected)
    {
        _suppressCallback = true;
        IsSelected = selected;
        _suppressCallback = false;
    }
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
