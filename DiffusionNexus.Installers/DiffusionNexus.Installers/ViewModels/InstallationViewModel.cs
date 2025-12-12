using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Enums;
using DiffusionNexus.Core.Services;
using DiffusionNexus.DataAccess;
using System.Text;

namespace DiffusionNexus.Installers.ViewModels;

/// <summary>
/// Service interface for folder picker interaction.
/// </summary>
public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for saving files.
/// </summary>
public interface IFileSaveService
{
    /// <summary>
    /// Opens a save file dialog and returns the selected file path.
    /// </summary>
    /// <param name="defaultFileName">Default file name to suggest.</param>
    /// <param name="filters">File type filters (e.g., "Text files|*.txt").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    Task<string?> SaveFileAsync(string defaultFileName, string filters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for clipboard operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">Text to copy.</param>
    Task SetTextAsync(string text);
}

/// <summary>
/// Service interface for user prompts and dialogs.
/// </summary>
public interface IUserPromptService
{
    /// <summary>
    /// Shows a confirmation dialog with Yes/No options.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Message to display.</param>
    /// <param name="yesButtonText">Text for the Yes button.</param>
    /// <param name="noButtonText">Text for the No button.</param>
    /// <returns>True if user clicked Yes, false otherwise.</returns>
    Task<bool> ConfirmAsync(string title, string message, string yesButtonText = "Yes", string noButtonText = "No");

    /// <summary>
    /// Shows an error message dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Error message to display.</param>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Shows an information message dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Message to display.</param>
    Task ShowInfoAsync(string title, string message);
}

/// <summary>
/// Result of pre-installation validation.
/// </summary>
public enum PreInstallationCheckResult
{
    /// <summary>
    /// Installation can proceed without issues.
    /// </summary>
    CanProceed,

    /// <summary>
    /// Target folder is not empty but configuration has models/nodes to install.
    /// User should be asked if they want to switch to Models/Nodes only mode.
    /// </summary>
    SuggestModelsNodesOnly,

    /// <summary>
    /// Target folder is not empty and no models/nodes are configured.
    /// Installation cannot proceed.
    /// </summary>
    TargetFolderNotEmpty,

    /// <summary>
    /// Installation was cancelled by user.
    /// </summary>
    Cancelled
}

/// <summary>
/// ViewModel for the Installation tab.
/// </summary>
public partial class InstallationViewModel : ViewModelBase
{
    private readonly IConfigurationRepository? _configurationRepository;
    private readonly InstallationEngine? _installationEngine;
    private IFolderPickerService? _folderPickerService;
    private IUserPromptService? _userPromptService;
    private IFileSaveService? _fileSaveService;
    private IClipboardService? _clipboardService;
    private InstallationConfiguration? _selectedConfiguration;

    /// <summary>
    /// Default constructor for design-time or standalone use.
    /// </summary>
    public InstallationViewModel() : this(null, null)
    {
    }

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    public InstallationViewModel(
        IConfigurationRepository? configurationRepository,
        InstallationEngine? installationEngine)
    {
        _configurationRepository = configurationRepository;
        _installationEngine = installationEngine;
        LogEntries = [];
        AvailableVramProfiles = [];
        AvailableInstallationTypes = [];
        SavedConfigurations = [];
        
        // VRAM profiles are hidden by default - will be shown when a configuration with VRAM profiles is loaded
        IsVramProfileVisible = false;
        
        // Initialize installation types
        InitializeInstallationTypes();
        
        // Load saved configurations if repository is available
        if (_configurationRepository is not null)
        {
            _ = LoadSavedConfigurationsAsync();
        }
    }

    /// <summary>
    /// Attaches the folder picker service for folder selection.
    /// </summary>
    public void AttachFolderPickerService(IFolderPickerService folderPickerService) =>
        _folderPickerService = folderPickerService;

    /// <summary>
    /// Attaches the user prompt service for dialogs and confirmations.
    /// </summary>
    public void AttachUserPromptService(IUserPromptService userPromptService) =>
        _userPromptService = userPromptService;

    /// <summary>
    /// Attaches the file save service for exporting files.
    /// </summary>
    public void AttachFileSaveService(IFileSaveService fileSaveService) =>
        _fileSaveService = fileSaveService;

    /// <summary>
    /// Attaches the clipboard service for copy operations.
    /// </summary>
    public void AttachClipboardService(IClipboardService clipboardService) =>
        _clipboardService = clipboardService;

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

    [ObservableProperty]
    private ConfigurationSelectionItem? _selectedSavedConfiguration;

    /// <summary>
    /// Gets or sets whether the user has accepted the disclaimer.
    /// Must be checked before installation can proceed.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartInstallationCommand))]
    private bool _disclaimerAccepted;

    /// <summary>
    /// Gets whether the VRAM profile selection should be visible.
    /// Only visible when the configuration has VRAM profiles defined.
    /// </summary>
    [ObservableProperty]
    private bool _isVramProfileVisible;

    /// <summary>
    /// Gets or sets whether verbose logging is enabled.
    /// When enabled, shows detailed command information including exact Python/Git commands.
    /// </summary>
    [ObservableProperty]
    private bool _verboseLogging;

    /// <summary>
    /// Gets or sets whether a desktop shortcut should be created.
    /// </summary>
    [ObservableProperty]
    private bool _createDesktopShortcut = true;

    /// <summary>
    /// Gets or sets whether a Start Menu shortcut should be created.
    /// </summary>
    [ObservableProperty]
    private bool _createStartMenuShortcut = true;

    /// <summary>
    /// Gets whether all requirements are met to start installation.
    /// Used for UI binding to show ready state.
    /// </summary>
    public bool IsReadyToInstall => 
        !string.IsNullOrWhiteSpace(TargetInstallFolder) && !IsInstalling && DisclaimerAccepted && SelectedSavedConfiguration is not null;

    partial void OnSelectedSavedConfigurationChanged(ConfigurationSelectionItem? value)
    {
        if (value is not null)
        {
            _ = LoadConfigurationAsync(value.Id);
        }
        else
        {
            // No configuration selected, hide VRAM profiles
            IsVramProfileVisible = false;
            AvailableVramProfiles.Clear();
        }
        OnPropertyChanged(nameof(IsReadyToInstall));
    }
    
    partial void OnTargetInstallFolderChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToInstall));
    }
    
    partial void OnDisclaimerAcceptedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToInstall));
    }
    
    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToInstall));
    }

    public ObservableCollection<InstallationLogEntry> LogEntries { get; }

    public ObservableCollection<VramProfileOption> AvailableVramProfiles { get; }

    public ObservableCollection<InstallationTypeOption> AvailableInstallationTypes { get; }

    /// <summary>
    /// Gets the collection of saved configurations available for selection.
    /// </summary>
    public ObservableCollection<ConfigurationSelectionItem> SavedConfigurations { get; }

    #endregion

    #region Configuration Loading

    /// <summary>
    /// Loads saved configurations from the database.
    /// </summary>
    public async Task LoadSavedConfigurationsAsync()
    {
        if (_configurationRepository is null) return;

        var configurations = await _configurationRepository.GetAllAsync();
        SavedConfigurations.Clear();
        
        foreach (var config in configurations)
        {
            SavedConfigurations.Add(new ConfigurationSelectionItem(config.Id, config.Name, config.Description));
        }
    }

    /// <summary>
    /// Loads a specific configuration by ID and applies its settings.
    /// </summary>
    private async Task LoadConfigurationAsync(Guid configurationId)
    {
        if (_configurationRepository is null) return;

        var configuration = await _configurationRepository.GetByIdAsync(configurationId);
        if (configuration is null) return;

        _selectedConfiguration = configuration;
        ApplyConfigurationSettings(configuration);
        AddLogEntry($"Configuration '{configuration.Name}' loaded", LogEntryLevel.Info);
    }

    /// <summary>
    /// Applies configuration settings to the view model.
    /// </summary>
    private void ApplyConfigurationSettings(InstallationConfiguration configuration)
    {
        // Apply target folder from configuration paths if available
        if (!string.IsNullOrWhiteSpace(configuration.Paths.RootDirectory))
        {
            TargetInstallFolder = configuration.Paths.RootDirectory;
        }

        // Apply VRAM profiles - only show if configuration has VRAM profiles
        if (!string.IsNullOrWhiteSpace(configuration.Vram.VramProfiles))
        {
            var profiles = configuration.Vram.VramProfiles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p.Replace("GB", "").Replace("+", ""), out var val) ? val : 0)
                .Where(v => v > 0)
                .ToArray();

            if (profiles.Length > 0)
            {
                SetAvailableVramProfiles(profiles);
                // Select the first profile by default
                SelectVramProfile(profiles[0]);
                IsVramProfileVisible = true;
            }
            else
            {
                IsVramProfileVisible = false;
                AvailableVramProfiles.Clear();
            }
        }
        else
        {
            // No VRAM profiles configured, hide the section
            IsVramProfileVisible = false;
            AvailableVramProfiles.Clear();
        }
    }

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

        // Show or hide the VRAM profile selection based on availability
        IsVramProfileVisible = AvailableVramProfiles.Count > 0;
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
            "Full NEW ComfyUI Install", 
            true, 
            SelectInstallationType));
        AvailableInstallationTypes.Add(new InstallationTypeOption(
            InstallationType.ModelsNodesOnly, 
            "Models/Nodes + Updates Only", 
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
        !string.IsNullOrWhiteSpace(TargetInstallFolder) && !IsInstalling && DisclaimerAccepted;

    [RelayCommand(CanExecute = nameof(CanStartInstallation), IncludeCancelCommand = true)]
    private async Task StartInstallationAsync(CancellationToken cancellationToken)
    {
        if (IsInstalling) return;

        IsInstalling = true;
        ProgressValue = 0;
        StatusMessage = "Running pre-installation checks...";
        LogEntries.Clear();
        StartInstallationCommand.NotifyCanExecuteChanged();

        try
        {
            AddLogEntry("Running pre-installation checks...", LogEntryLevel.Info);

            // Check if configuration is selected
            if (_selectedConfiguration is null)
            {
                AddLogEntry("No configuration selected. Please select a configuration to install.", LogEntryLevel.Warning);
                StatusMessage = "No configuration selected";
                return;
            }

            // Check if installation engine is available
            if (_installationEngine is null)
            {
                AddLogEntry("Installation engine not available.", LogEntryLevel.Error);
                StatusMessage = "Installation engine not available";
                return;
            }

            // Run pre-installation validation
            var preCheckResult = await RunPreInstallationChecksAsync(cancellationToken);
            
            if (preCheckResult == PreInstallationCheckResult.Cancelled)
            {
                AddLogEntry("Installation cancelled by user.", LogEntryLevel.Warning);
                StatusMessage = "Installation cancelled";
                return;
            }

            if (preCheckResult == PreInstallationCheckResult.TargetFolderNotEmpty)
            {
                // Error was already shown to user
                StatusMessage = "Installation cannot proceed";
                return;
            }

            // Show confirmation dialog before proceeding
            var userConfirmed = await ShowInstallationConfirmationAsync();
            if (!userConfirmed)
            {
                AddLogEntry("Installation cancelled by user.", LogEntryLevel.Warning);
                StatusMessage = "Installation cancelled";
                return;
            }

            // Proceed with installation
            StatusMessage = "Starting installation...";
            AddLogEntry("Pre-installation checks passed.", LogEntryLevel.Success);
            AddLogEntry($"Target folder: {TargetInstallFolder}", LogEntryLevel.Info);
            AddLogEntry($"VRAM Profile: {SelectedVramProfile} GB", LogEntryLevel.Info);
            AddLogEntry($"Installation Type: {SelectedInstallationType}", LogEntryLevel.Info);
            AddLogEntry($"Using configuration: {_selectedConfiguration.Name}", LogEntryLevel.Info);

            // Apply UI-selected settings to the configuration
            _selectedConfiguration.Paths.RootDirectory = TargetInstallFolder;

            // Create installation options from UI selections
            var installationOptions = new InstallationOptions
            {
                OnlyModelDownload = SelectedInstallationType == InstallationType.ModelsNodesOnly,
                SelectedVramProfile = SelectedVramProfile,
                VerboseLogging = VerboseLogging,
                CreateDesktopShortcut = CreateDesktopShortcut,
                CreateStartMenuShortcut = CreateStartMenuShortcut
            };

            // Create progress reporters
            var logProgress = new Progress<InstallLogEntry>(entry =>
            {
                // Skip debug/verbose messages if verbose logging is disabled
                if (entry.Level == Core.Models.Enums.LogLevel.Debug && !VerboseLogging)
                {
                    return;
                }

                // Map Core.Models.Enums.LogLevel to ViewModels.LogEntryLevel
                var level = entry.Level switch
                {
                    Core.Models.Enums.LogLevel.Debug => LogEntryLevel.Debug,
                    Core.Models.Enums.LogLevel.Trace => LogEntryLevel.Debug,
                    Core.Models.Enums.LogLevel.Success => LogEntryLevel.Success,
                    Core.Models.Enums.LogLevel.Warning => LogEntryLevel.Warning,
                    Core.Models.Enums.LogLevel.Error => LogEntryLevel.Error,
                    Core.Models.Enums.LogLevel.Critical => LogEntryLevel.Error,
                    _ => LogEntryLevel.Info
                };
                AddLogEntry(entry.Message, level);
            });

            var stepProgress = new Progress<InstallationProgress>(progress =>
            {
                ProgressValue = progress.ProgressPercentage;
                StatusMessage = progress.Message;
            });

            // Run the actual installation
            var result = await _installationEngine.RunInstallationAsync(
                _selectedConfiguration,
                TargetInstallFolder,
                installationOptions,
                logProgress,
                stepProgress,
                cancellationToken);

            if (result.IsSuccess)
            {
                AddLogEntry("Installation completed successfully!", LogEntryLevel.Success);
                StatusMessage = "Installation completed!";
                ProgressValue = 100;

                if (!string.IsNullOrWhiteSpace(result.RepositoryPath))
                {
                    AddLogEntry($"Repository: {result.RepositoryPath}", LogEntryLevel.Info);
                }

                if (!string.IsNullOrWhiteSpace(result.VirtualEnvironmentPath))
                {
                    AddLogEntry($"Virtual Environment: {result.VirtualEnvironmentPath}", LogEntryLevel.Info);
                }
            }
            else
            {
                AddLogEntry($"Installation failed: {result.Message}", LogEntryLevel.Error);
                StatusMessage = "Installation failed";
            }
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

    /// <summary>
    /// Runs pre-installation checks and handles user prompts.
    /// </summary>
    private async Task<PreInstallationCheckResult> RunPreInstallationChecksAsync(CancellationToken cancellationToken)
    {
        if (_selectedConfiguration is null)
        {
            return PreInstallationCheckResult.Cancelled;
        }

        var isFullInstall = SelectedInstallationType == InstallationType.FullInstall;
        var validator = new PreInstallationValidator();

        // For Models/Nodes only mode, validate that a ComfyUI installation exists
        if (!isFullInstall)
        {
            AddLogEntry("Models/Nodes only installation - checking for existing ComfyUI installation.", LogEntryLevel.Info);
            
            if (!validator.IsValidComfyUIInstallation(TargetInstallFolder))
            {
                var errorMessage = $"No valid ComfyUI installation found at '{TargetInstallFolder}'.\n\n" +
                    "Models/Nodes Only mode requires an existing ComfyUI installation.\n\n" +
                    "A valid installation must be either:\n" +
                    "• A folder named 'ComfyUI'\n" +
                    "• A folder containing 'venv' or 'python_embeded' directory\n\n" +
                    "Please select a valid ComfyUI installation folder or use 'Full Install' mode.";

                AddLogEntry($"No valid ComfyUI installation found at '{TargetInstallFolder}'.", LogEntryLevel.Error);

                if (_userPromptService is not null)
                {
                    await _userPromptService.ShowErrorAsync("No ComfyUI Installation Found", errorMessage);
                }

                return PreInstallationCheckResult.TargetFolderNotEmpty;
            }

            AddLogEntry("Valid ComfyUI installation found.", LogEntryLevel.Success);
            return PreInstallationCheckResult.CanProceed;
        }

        // Check the target directory for full install
        var validationResult = validator.Validate(_selectedConfiguration, TargetInstallFolder, isFullInstall);

        AddLogEntry($"Checking target path: {validationResult.FullTargetPath}", LogEntryLevel.Info);

        if (validationResult.CanProceed)
        {
            AddLogEntry("Target directory is available for installation.", LogEntryLevel.Info);
            return PreInstallationCheckResult.CanProceed;
        }

        // Target directory exists and is not empty
        AddLogEntry($"Target directory '{validationResult.FullTargetPath}' exists and is not empty.", LogEntryLevel.Warning);

        if (validationResult.ShouldSuggestModelsNodesOnly)
        {
            // Configuration has models and/or custom nodes - ask user if they want to switch
            var hasModelsText = validationResult.HasModels ? "models" : "";
            var hasNodesText = validationResult.HasCustomNodes ? "custom nodes" : "";
            var contentDescription = string.Join(" and ", new[] { hasModelsText, hasNodesText }.Where(s => !string.IsNullOrEmpty(s)));

            var message = $"The target folder '{validationResult.FullTargetPath}' already exists and is not empty.\n\n" +
                         $"Your configuration includes {contentDescription} to install.\n\n" +
                         $"Would you like to switch to 'Models/Nodes Only' mode?\n" +
                         $"This will install only the {contentDescription} to the existing installation.\n\n" +
                         $"Note: This requires a valid ComfyUI installation in the target folder.";

            if (_userPromptService is not null)
            {
                var switchToModelsOnly = await _userPromptService.ConfirmAsync(
                    "Target Folder Not Empty",
                    message,
                    "Switch to Models/Nodes Only",
                    "Cancel Installation");

                if (switchToModelsOnly)
                {
                    // Switch to Models/Nodes only mode
                    SelectInstallationType(InstallationType.ModelsNodesOnly);
                    AddLogEntry("Switched to Models/Nodes Only installation mode.", LogEntryLevel.Info);
                    
                    // Validate the ComfyUI installation before proceeding
                    if (!validator.IsValidComfyUIInstallation(validationResult.FullTargetPath ?? TargetInstallFolder))
                    {
                        var noInstallError = "No valid ComfyUI installation found in the target folder.\n\n" +
                            "Models/Nodes Only mode requires an existing ComfyUI installation.";
                        
                        AddLogEntry("No valid ComfyUI installation found.", LogEntryLevel.Error);
                        
                        await _userPromptService.ShowErrorAsync("No ComfyUI Installation Found", noInstallError);
                        return PreInstallationCheckResult.TargetFolderNotEmpty;
                    }

                    AddLogEntry("Valid ComfyUI installation found.", LogEntryLevel.Success);
                    return PreInstallationCheckResult.CanProceed;
                }
                else
                {
                    return PreInstallationCheckResult.Cancelled;
                }
            }
            else
            {
                // No prompt service available, log warning and cancel
                AddLogEntry("Cannot prompt user - installation cancelled.", LogEntryLevel.Warning);
                return PreInstallationCheckResult.Cancelled;
            }
        }
        else
        {
            // No models or custom nodes configured - show error
            var errorMessage = validationResult.ErrorMessage ?? 
                $"The target folder '{validationResult.FullTargetPath}' is not empty and no models or custom nodes are configured.";

            AddLogEntry(errorMessage, LogEntryLevel.Error);

            if (_userPromptService is not null)
            {
                await _userPromptService.ShowErrorAsync("Cannot Install", errorMessage);
            }

            return PreInstallationCheckResult.TargetFolderNotEmpty;
        }
    }

    /// <summary>
    /// Shows a confirmation dialog with installation details before proceeding.
    /// </summary>
    private async Task<bool> ShowInstallationConfirmationAsync()
    {
        if (_userPromptService is null || _selectedConfiguration is null)
        {
            // No prompt service available, proceed without confirmation
            return true;
        }

        var installationTypeName = SelectedInstallationType == InstallationType.FullInstall
            ? "Full Install"
            : "Models/Nodes Only";

        var message = $"""
            Installation Summary
            ====================

            Type:        {installationTypeName}
            Workload:    {_selectedConfiguration.Name}
            Target:      {TargetInstallFolder}
            Python:      {_selectedConfiguration.Python.PythonVersion}

            Do you want to continue?
            """;

        return await _userPromptService.ConfirmAsync(
            "Confirm Installation",
            message,
            "Continue",
            "Cancel");
    }

    /// <summary>
    /// Truncates a path to fit within the specified maximum length.
    /// </summary>
    private static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
        {
            return path;
        }

        // Show beginning and end of path with ellipsis
        var ellipsis = "...";
        var availableLength = maxLength - ellipsis.Length;
        var startLength = availableLength / 2;
        var endLength = availableLength - startLength;

        return $"{path[..startLength]}{ellipsis}{path[^endLength..]}";
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

    [RelayCommand]
    private async Task ExportLogAsync(CancellationToken cancellationToken)
    {
        if (LogEntries.Count == 0)
        {
            if (_userPromptService is not null)
            {
                await _userPromptService.ShowInfoAsync("Export Log", "The log is empty. Nothing to export.");
            }
            return;
        }

        if (_fileSaveService is null)
        {
            AddLogEntry("File save service not available.", LogEntryLevel.Warning);
            return;
        }

        var defaultFileName = $"installation-log-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt";
        var filePath = await _fileSaveService.SaveFileAsync(defaultFileName, "Text files|*.txt", cancellationToken);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return; // User cancelled
        }

        try
        {
            var logContent = BuildLogContent();
            await File.WriteAllTextAsync(filePath, logContent, cancellationToken);
            AddLogEntry($"Log exported to: {filePath}", LogEntryLevel.Success);
        }
        catch (Exception ex)
        {
            AddLogEntry($"Failed to export log: {ex.Message}", LogEntryLevel.Error);
        }
    }

    [RelayCommand]
    private async Task CopyLogToClipboardAsync()
    {
        if (LogEntries.Count == 0)
        {
            if (_userPromptService is not null)
            {
                await _userPromptService.ShowInfoAsync("Copy Log", "The log is empty. Nothing to copy.");
            }
            return;
        }

        if (_clipboardService is null)
        {
            AddLogEntry("Clipboard service not available.", LogEntryLevel.Warning);
            return;
        }

        try
        {
            var logContent = BuildLogContent();
            await _clipboardService.SetTextAsync(logContent);
            AddLogEntry("Log copied to clipboard.", LogEntryLevel.Success);
        }
        catch (Exception ex)
        {
            AddLogEntry($"Failed to copy log: {ex.Message}", LogEntryLevel.Error);
        }
    }

    #endregion

    #region Private Helpers

    private string BuildLogContent()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Installation Log");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        // Iterate in reverse order since newest entries are first in the collection
        for (var i = LogEntries.Count - 1; i >= 0; i--)
        {
            sb.AppendLine(LogEntries[i].Display);
        }

        return sb.ToString();
    }

    private void AddLogEntry(string message, LogEntryLevel level)
    {
        // Insert at the beginning so newest entries appear first (rolling log)
        LogEntries.Insert(0, new InstallationLogEntry(message, level));
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
/// Represents a configuration item for selection in the dropdown.
/// </summary>
public class ConfigurationSelectionItem
{
    public ConfigurationSelectionItem(Guid id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Display => string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} - {Description}";
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
/// Acts like a radio button - cannot be deselected, only another option can be selected.
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
        if (_suppressCallback) return;

        if (value)
        {
            // User selected this option - notify to update others
            _onSelected?.Invoke(Type);
        }
        else
        {
            // Prevent deselection - re-select this option
            _suppressCallback = true;
            IsSelected = true;
            _suppressCallback = false;
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
/// Acts like a radio button - cannot be deselected, only another option can be selected.
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
        if (_suppressCallback) return;

        if (value)
        {
            // User selected this option - notify to update others
            _onSelected?.Invoke(Value);
        }
        else
        {
            // Prevent deselection - re-select this option
            _suppressCallback = true;
            IsSelected = true;
            _suppressCallback = false;
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
    Debug,
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
        LogEntryLevel.Debug => Brushes.Cyan,
        LogEntryLevel.Success => Brushes.Green,
        LogEntryLevel.Warning => Brushes.Orange,
        LogEntryLevel.Error => Brushes.Red,
        _ => Brushes.White
    };
}
