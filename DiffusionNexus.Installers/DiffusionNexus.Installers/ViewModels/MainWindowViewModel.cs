using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DiffusionNexus.Core.Models;
using DiffusionNexus.Core.Services;
using DiffusionNexus.DataAccess;

namespace DiffusionNexus.Installers.ViewModels;

/// <summary>
/// Main window ViewModel that coordinates the Configuration and Installation views.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the ConfigurationViewModel for the Configuration tab.
    /// </summary>
    public ConfigurationViewModel ConfigurationViewModel { get; }

    /// <summary>
    /// Gets the InstallationViewModel for the Installation tab.
    /// </summary>
    public InstallationViewModel InstallationViewModel { get; }

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    public MainWindowViewModel(
        IConfigurationRepository configurationRepository,
        IDatabaseManagementService databaseManagementService,
        InstallationEngine installationEngine)
    {
        ArgumentNullException.ThrowIfNull(configurationRepository);
        ArgumentNullException.ThrowIfNull(databaseManagementService);
        ArgumentNullException.ThrowIfNull(installationEngine);

        ConfigurationViewModel = new ConfigurationViewModel(configurationRepository, databaseManagementService, installationEngine);
        InstallationViewModel = new InstallationViewModel(configurationRepository, installationEngine);
    }
}

#region Item ViewModels

public partial class GitRepositoryItemViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public GitRepositoryItemViewModel(GitRepository model, Action onChanged)
    {
        Model = model;
        _onChanged = onChanged;
        _name = model.Name;
        _url = model.Url;
        _installRequirements = model.InstallRequirements;
        _priority = model.Priority;
    }

    public GitRepository Model { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private bool _installRequirements;
    [ObservableProperty] private int _priority;

    partial void OnNameChanged(string value) { Model.Name = value; _onChanged(); }
    partial void OnUrlChanged(string value) { Model.Url = value; _onChanged(); }
    partial void OnInstallRequirementsChanged(bool value) { Model.InstallRequirements = value; _onChanged(); }
    partial void OnPriorityChanged(int value) { Model.Priority = value; _onChanged(); }
}

public partial class ModelDownloadItemViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public ModelDownloadItemViewModel(ModelDownload model, Action onChanged)
    {
        Model = model;
        _onChanged = onChanged;
        _name = model.Name;
        _url = model.Url;
        _destination = model.Destination;
        _vramProfile = model.VramProfile;
        _enabled = model.Enabled;
        _downloadLinksCount = model.DownloadLinks.Count;
    }

    public ModelDownload Model { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _destination = string.Empty;
    [ObservableProperty] private VramProfile _vramProfile;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private int _downloadLinksCount;

    partial void OnNameChanged(string value) { Model.Name = value; _onChanged(); }
    partial void OnUrlChanged(string value) { Model.Url = value; _onChanged(); }
    partial void OnDestinationChanged(string value) { Model.Destination = value; _onChanged(); }
    partial void OnVramProfileChanged(VramProfile value) { Model.VramProfile = value; _onChanged(); }
    partial void OnEnabledChanged(bool value) { Model.Enabled = value; _onChanged(); }
    
    /// <summary>
    /// Refreshes the download links count from the underlying model.
    /// </summary>
    public void RefreshDownloadLinksCount()
    {
        DownloadLinksCount = Model.DownloadLinks.Count;
    }
}

public class InstallLogEntryViewModel(InstallLogEntry entry)
{
    public DateTimeOffset Timestamp { get; } = entry.Timestamp;
    public string Message { get; } = entry.Message;
    public LogLevel Level { get; } = entry.Level;
    public string Display => $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
}

public class ConfigurationListItemViewModel(InstallationConfiguration configuration, Action onChanged)
{
    public InstallationConfiguration Configuration { get; } = configuration;
    public Action OnChanged { get; } = onChanged;
    public Guid Id => Configuration.Id;
    public string Name => Configuration.Name;
    public string Description => Configuration.Description;
    public string Display => string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} - {Description}";
}

#endregion
