using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffusionNexus.Core.Models;
using DiffusionNexus.Core.Services;
using DiffusionNexus.DataAccess;

namespace DiffusionNexus.Installers.ViewModels;

#region Service Interfaces

public interface IStorageInteractionService
{
    Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default);
    Task<string?> PickSaveFileAsync(string? suggestedFileName, CancellationToken cancellationToken = default);
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}

public interface IGitRepositoryInteractionService
{
    Task<GitRepository?> CreateRepositoryAsync();
    Task<GitRepository?> EditRepositoryAsync(GitRepository repository);
}

public interface IConflictResolutionService
{
    Task<SaveConflictResolution> ResolveSaveConflictAsync(string configurationName);
}

public interface IConfigurationNameService
{
    Task<string?> PromptForNameAsync(string currentName, Guid? excludeId);
}

public interface IConfigurationManagementService
{
    Task<bool> ConfirmDeleteAsync(string configurationName);
}

public interface IModelEditorInteractionService
{
    Task<ModelDownload?> CreateModelAsync(bool vramEnabled, string[] availableVramProfiles);
    Task<ModelDownload?> EditModelAsync(ModelDownload model, bool vramEnabled, string[] availableVramProfiles);
}

#endregion

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly ConfigurationService _configurationService;
    private readonly InstallationEngine _installationEngine;

    private InstallationConfiguration _configuration = new();
    private string? _currentFilePath;
    private ConfigurationFormat? _currentFormat;

    // UI interaction services (attached by view)
    private IStorageInteractionService? _storageInteraction;
    private IGitRepositoryInteractionService? _gitRepositoryInteraction;
    private IModelEditorInteractionService? _modelEditorInteraction;
    private IConflictResolutionService? _conflictResolution;
    private IConfigurationNameService? _nameService;
    private IConfigurationManagementService? _configurationManagement;

    public event EventHandler<GitRepositoryItemViewModel>? EditRepositoryRequested;

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    public MainWindowViewModel(IConfigurationRepository configurationRepository)
    {
        ArgumentNullException.ThrowIfNull(configurationRepository);

        _configurationRepository = configurationRepository;
        _configurationService = new ConfigurationService();
        _installationEngine = new InstallationEngine();

        GitRepositories = [];
        ModelDownloads = [];
        Logs = [];
        SavedConfigurations = [];

        NewConfiguration();
        _ = LoadSavedConfigurationsAsync();
    }

    #region Collections

    public ObservableCollection<GitRepositoryItemViewModel> GitRepositories { get; }
    public ObservableCollection<ModelDownloadItemViewModel> ModelDownloads { get; }
    public ObservableCollection<InstallLogEntryViewModel> Logs { get; }
    public ObservableCollection<ConfigurationListItemViewModel> SavedConfigurations { get; }

    #endregion

    #region Static Options

    public RepositoryType[] RepositoryTypes { get; } = Enum.GetValues<RepositoryType>();
    public string[] PythonVersions { get; } = ["3.8", "3.9", "3.10", "3.11", "3.12", "3.13"];
    public string[] SuggestedCudaVersions { get; } = ["12.8", "12.4", "12.1", "11.8"];
    public string[] SuggestedTorchVersions { get; } = [string.Empty, "2.4.0", "2.3.1", "2.2.2"];

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private string _previewPlan = string.Empty;

    [ObservableProperty]
    private string _torchCompatibilityHint = string.Empty;

    [ObservableProperty]
    private InstallLogEntryViewModel? _selectedLogEntry;

    [ObservableProperty]
    private GitRepositoryItemViewModel? _selectedRepository;

    [ObservableProperty]
    private ModelDownloadItemViewModel? _selectedModel;

    [ObservableProperty]
    private ConfigurationListItemViewModel? _selectedSavedConfiguration;

    partial void OnSelectedSavedConfigurationChanged(ConfigurationListItemViewModel? value)
    {
        if (value is not null)
        {
            _ = LoadConfigurationFromDatabaseAsync(value);
        }
    }

    #endregion

    #region Configuration Properties

    public RepositoryType SelectedRepositoryType
    {
        get => _configuration.Repository.Type;
        set
        {
            if (_configuration.Repository.Type != value)
            {
                _configuration.Repository.Type = value;

                if (value == RepositoryType.ComfyUI)
                {
                    ApplyComfyUIDefaults();
                }

                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string RepositoryUrl
    {
        get => _configuration.Repository.RepositoryUrl;
        set
        {
            if (_configuration.Repository.RepositoryUrl != value)
            {
                _configuration.Repository.RepositoryUrl = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string SelectedPythonVersion
    {
        get => _configuration.Python.PythonVersion;
        set
        {
            if (_configuration.Python.PythonVersion != value)
            {
                _configuration.Python.PythonVersion = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public bool CreateVirtualEnvironment
    {
        get => _configuration.Python.CreateVirtualEnvironment;
        set
        {
            if (_configuration.Python.CreateVirtualEnvironment != value)
            {
                _configuration.Python.CreateVirtualEnvironment = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public bool CreateVramSettings
    {
        get => _configuration.Python.CreateVramSettings;
        set
        {
            if (_configuration.Python.CreateVramSettings != value)
            {
                _configuration.Python.CreateVramSettings = value;

                if (value && string.IsNullOrWhiteSpace(_configuration.Vram.VramProfiles))
                {
                    _configuration.Vram.VramProfiles = "8,16,24,24+";
                    OnPropertyChanged(nameof(VramProfiles));
                }

                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string InterpreterPathOverride
    {
        get => _configuration.Python.InterpreterPathOverride;
        set
        {
            if (_configuration.Python.InterpreterPathOverride != value)
            {
                _configuration.Python.InterpreterPathOverride = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string VirtualEnvironmentName
    {
        get => _configuration.Python.VirtualEnvironmentName;
        set
        {
            if (_configuration.Python.VirtualEnvironmentName != value)
            {
                _configuration.Python.VirtualEnvironmentName = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public bool InstallTriton
    {
        get => _configuration.Python.InstallTriton;
        set
        {
            if (_configuration.Python.InstallTriton != value)
            {
                _configuration.Python.InstallTriton = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public bool InstallSageAttention
    {
        get => _configuration.Python.InstallSageAttention;
        set
        {
            if (_configuration.Python.InstallSageAttention != value)
            {
                _configuration.Python.InstallSageAttention = value;

                // Sage Attention requires Triton
                if (value && !_configuration.Python.InstallTriton)
                {
                    _configuration.Python.InstallTriton = true;
                    OnPropertyChanged(nameof(InstallTriton));
                }

                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string CudaVersion
    {
        get => _configuration.Torch.CudaVersion;
        set
        {
            if (_configuration.Torch.CudaVersion != value)
            {
                _configuration.Torch.CudaVersion = value;
                OnPropertyChanged();
                UpdateCompatibilityHint();
                MarkDirty();
            }
        }
    }

    public string TorchVersion
    {
        get => _configuration.Torch.TorchVersion;
        set
        {
            if (_configuration.Torch.TorchVersion != value)
            {
                _configuration.Torch.TorchVersion = value;
                OnPropertyChanged();
                UpdateCompatibilityHint();
                MarkDirty();
            }
        }
    }

    public string TorchIndexUrl
    {
        get => _configuration.Torch.IndexUrl ?? string.Empty;
        set
        {
            if ((_configuration.Torch.IndexUrl ?? string.Empty) != value)
            {
                _configuration.Torch.IndexUrl = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string RootDirectory
    {
        get => _configuration.Paths.RootDirectory;
        set
        {
            if (_configuration.Paths.RootDirectory != value)
            {
                _configuration.Paths.RootDirectory = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string DefaultModelDirectory
    {
        get => _configuration.Paths.DefaultModelDownloadDirectory ?? string.Empty;
        set
        {
            if ((_configuration.Paths.DefaultModelDownloadDirectory ?? string.Empty) != value)
            {
                _configuration.Paths.DefaultModelDownloadDirectory = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string LogFileName
    {
        get => _configuration.Paths.LogFileName;
        set
        {
            if (_configuration.Paths.LogFileName != value)
            {
                _configuration.Paths.LogFileName = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string VramProfiles
    {
        get => _configuration.Vram.VramProfiles;
        set
        {
            if (_configuration.Vram.VramProfiles != value)
            {
                _configuration.Vram.VramProfiles = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    #endregion

    #region Service Attachment Methods

    public void AttachStorageInteraction(IStorageInteractionService storageInteraction) =>
        _storageInteraction = storageInteraction;

    public void AttachGitRepositoryInteraction(IGitRepositoryInteractionService gitRepositoryInteraction) =>
        _gitRepositoryInteraction = gitRepositoryInteraction;

    public void AttachModelEditorInteraction(IModelEditorInteractionService modelEditorInteraction) =>
        _modelEditorInteraction = modelEditorInteraction;

    public void AttachConflictResolutionService(IConflictResolutionService conflictResolution) =>
        _conflictResolution = conflictResolution;

    public void AttachConfigurationNameService(IConfigurationNameService nameService) =>
        _nameService = nameService;

    public void AttachConfigurationManagementService(IConfigurationManagementService configurationManagement) =>
        _configurationManagement = configurationManagement;

    #endregion

    #region Commands - Configuration Management

    [RelayCommand]
    private void NewConfiguration()
    {
        _configuration = new InstallationConfiguration
        {
            Id = Guid.NewGuid(),
            Repository = new MainRepositorySettings
            {
                Type = RepositoryType.ComfyUI,
                RepositoryUrl = "https://github.com/comfyanonymous/ComfyUI"
            },
            Python = new PythonEnvironmentSettings
            {
                PythonVersion = "3.12",
                CreateVirtualEnvironment = true,
                VirtualEnvironmentName = "venv",
                InstallSageAttention = true,
                InstallTriton = true
            },
            Torch = new TorchSettings
            {
                CudaVersion = SuggestedCudaVersions[0],
                TorchVersion = SuggestedTorchVersions[1]
            },
            Paths = new PathSettings
            {
                RootDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "DiffusionNexus"),
                LogFileName = "install.log"
            },
            Vram = new VramSettings
            {
                VramProfiles = string.Empty
            }
        };

        _currentFilePath = null;
        _currentFormat = null;
        SelectedSavedConfiguration = null;
        GitRepositories.Clear();
        ModelDownloads.Clear();
        Logs.Clear();
        PreviewPlan = string.Empty;
        ValidationSummary = string.Empty;
        ReloadCollectionsFromConfiguration();
        UpdateCompatibilityHint();
        OnPropertyChanged(string.Empty);
    }

    private void ApplyComfyUIDefaults()
    {
        _configuration.Python.PythonVersion = "3.12";
        _configuration.Python.InstallSageAttention = true;
        _configuration.Python.InstallTriton = true;

        OnPropertyChanged(nameof(SelectedPythonVersion));
        OnPropertyChanged(nameof(InstallSageAttention));
        OnPropertyChanged(nameof(InstallTriton));
    }

    [RelayCommand]
    private async Task ImportConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_storageInteraction is null) return;

        var path = await _storageInteraction.PickOpenFileAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path)) return;

        var configuration = await _configurationService.LoadAsync(path, cancellationToken);
        _configuration = configuration;
        _currentFilePath = path;
        _currentFormat = ConfigurationService.GetFormatFromExtension(path);
        ReloadCollectionsFromConfiguration();
        UpdateCompatibilityHint();
        ValidationSummary = string.Empty;
        PreviewPlan = string.Empty;
        Logs.Clear();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await SaveAsAsync(cancellationToken);
            return;
        }

        await SaveConfigurationAsync(_currentFilePath!, _currentFormat!.Value, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsAsync(CancellationToken cancellationToken)
    {
        if (_storageInteraction is null) return;

        var path = await _storageInteraction.PickSaveFileAsync(_currentFilePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(path)) return;

        var format = ConfigurationService.GetFormatFromExtension(path);
        await SaveConfigurationAsync(path, format, cancellationToken);
        _currentFilePath = path;
        _currentFormat = format;
    }

    #endregion

    #region Commands - Execution

    [RelayCommand]
    private async Task PreviewPlanAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (!TryValidate(out var summary))
        {
            ValidationSummary = summary;
            return;
        }

        ValidationSummary = summary;
        var plan = _installationEngine.BuildPlan(_configuration);
        PreviewPlan = string.Join(Environment.NewLine, plan);
    }

    [RelayCommand]
    private async Task DryRunAsync(CancellationToken cancellationToken)
    {
        if (!TryValidate(out var summary))
        {
            ValidationSummary = summary;
            return;
        }

        ValidationSummary = summary;
        await ExecuteEngineAsync(progress =>
            _installationEngine.DryRunAsync(_configuration, progress, cancellationToken));
    }

    [RelayCommand]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        if (!TryValidate(out var summary))
        {
            ValidationSummary = summary;
            return;
        }

        ValidationSummary = summary;
        await ExecuteEngineAsync(progress =>
            _installationEngine.InstallAsync(_configuration, progress, cancellationToken));
    }

    #endregion

    #region Commands - Repository Management

    [RelayCommand]
    private async Task AddRepositoryAsync()
    {
        if (_gitRepositoryInteraction is null) return;

        var repository = await _gitRepositoryInteraction.CreateRepositoryAsync();
        if (repository is null) return;

        repository.Priority = GitRepositories.Count + 1;
        _configuration.GitRepositories.Add(repository);

        var vm = new GitRepositoryItemViewModel(repository, MarkDirty);
        GitRepositories.Add(vm);
        UpdateRepositoryPriorities();
        SelectedRepository = vm;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveRepository()
    {
        if (SelectedRepository is null) return;

        _configuration.GitRepositories.Remove(SelectedRepository.Model);
        GitRepositories.Remove(SelectedRepository);
        UpdateRepositoryPriorities();
        MarkDirty();
    }

    [RelayCommand]
    private void MoveRepositoryUp()
    {
        if (SelectedRepository is null) return;

        var index = GitRepositories.IndexOf(SelectedRepository);
        if (index <= 0) return;

        GitRepositories.Move(index, index - 1);
        _configuration.GitRepositories.Remove(SelectedRepository.Model);
        _configuration.GitRepositories.Insert(index - 1, SelectedRepository.Model);
        UpdateRepositoryPriorities();
        MarkDirty();
    }

    [RelayCommand]
    private void MoveRepositoryDown()
    {
        if (SelectedRepository is null) return;

        var index = GitRepositories.IndexOf(SelectedRepository);
        if (index < 0 || index >= GitRepositories.Count - 1) return;

        GitRepositories.Move(index, index + 1);
        _configuration.GitRepositories.Remove(SelectedRepository.Model);
        _configuration.GitRepositories.Insert(index + 1, SelectedRepository.Model);
        UpdateRepositoryPriorities();
        MarkDirty();
    }

    public void MoveRepositoryUpWithParameter(GitRepositoryItemViewModel repository)
    {
        if (repository is null) return;

        var index = GitRepositories.IndexOf(repository);
        if (index <= 0) return;

        GitRepositories.Move(index, index - 1);
        _configuration.GitRepositories.Remove(repository.Model);
        _configuration.GitRepositories.Insert(index - 1, repository.Model);
        UpdateRepositoryPriorities();
        SelectedRepository = repository;
        MarkDirty();
    }

    public void MoveRepositoryDownWithParameter(GitRepositoryItemViewModel repository)
    {
        if (repository is null) return;

        var index = GitRepositories.IndexOf(repository);
        if (index < 0 || index >= GitRepositories.Count - 1) return;

        GitRepositories.Move(index, index + 1);
        _configuration.GitRepositories.Remove(repository.Model);
        _configuration.GitRepositories.Insert(index + 1, repository.Model);
        UpdateRepositoryPriorities();
        SelectedRepository = repository;
        MarkDirty();
    }

    [RelayCommand]
    private async Task EditRepositoryAsync(GitRepositoryItemViewModel? repository)
    {
        if (repository is null) return;

        SelectedRepository = repository;
        EditRepositoryRequested?.Invoke(this, repository);

        if (_gitRepositoryInteraction is null) return;

        var updatedRepository = await _gitRepositoryInteraction.EditRepositoryAsync(repository.Model);
        if (updatedRepository is null) return;

        repository.Name = updatedRepository.Name;
        repository.Url = updatedRepository.Url;
        repository.InstallRequirements = updatedRepository.InstallRequirements;
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteRepository(GitRepositoryItemViewModel? repository)
    {
        if (repository is null) return;

        var index = GitRepositories.IndexOf(repository);
        _configuration.GitRepositories.Remove(repository.Model);
        GitRepositories.Remove(repository);

        if (SelectedRepository == repository)
        {
            SelectedRepository = GitRepositories.Count == 0
                ? null
                : GitRepositories[Math.Clamp(index, 0, GitRepositories.Count - 1)];
        }

        UpdateRepositoryPriorities();
        MarkDirty();
    }

    #endregion

    #region Commands - Model Management

    [RelayCommand]
    private async Task AddModelAsync()
    {
        if (_modelEditorInteraction is null) return;

        var vramProfiles = GetAvailableVramProfiles();
        var model = await _modelEditorInteraction.CreateModelAsync(CreateVramSettings, vramProfiles);
        if (model is null) return;

        _configuration.ModelDownloads.Add(model);
        var vm = new ModelDownloadItemViewModel(model, MarkDirty);
        ModelDownloads.Add(vm);
        SelectedModel = vm;
        MarkDirty();
    }

    [RelayCommand]
    private async Task EditModelAsync(ModelDownloadItemViewModel? modelVm)
    {
        if (modelVm is null || _modelEditorInteraction is null) return;

        SelectedModel = modelVm;

        var vramProfiles = GetAvailableVramProfiles();
        var updatedModel = await _modelEditorInteraction.EditModelAsync(modelVm.Model, CreateVramSettings, vramProfiles);
        if (updatedModel is null) return;

        modelVm.Name = updatedModel.Name;
        modelVm.Destination = updatedModel.Destination;
        modelVm.Enabled = updatedModel.Enabled;
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteModel(ModelDownloadItemViewModel? modelVm)
    {
        if (modelVm is null) return;

        var index = ModelDownloads.IndexOf(modelVm);
        _configuration.ModelDownloads.Remove(modelVm.Model);
        ModelDownloads.Remove(modelVm);

        if (SelectedModel == modelVm)
        {
            SelectedModel = ModelDownloads.Count == 0
                ? null
                : ModelDownloads[Math.Clamp(index, 0, ModelDownloads.Count - 1)];
        }

        MarkDirty();
    }

    #endregion

    #region Commands - Browse

    [RelayCommand]
    private async Task BrowseInterpreterAsync(CancellationToken cancellationToken)
    {
        if (_storageInteraction is null) return;

        var path = await _storageInteraction.PickOpenFileAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            InterpreterPathOverride = path;
        }
    }

    [RelayCommand]
    private async Task BrowseRootDirectoryAsync(CancellationToken cancellationToken)
    {
        if (_storageInteraction is null) return;

        var path = await _storageInteraction.PickFolderAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            RootDirectory = path;
        }
    }

    [RelayCommand]
    private async Task BrowseModelDirectoryAsync(CancellationToken cancellationToken)
    {
        if (_storageInteraction is null) return;

        var path = await _storageInteraction.PickFolderAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DefaultModelDirectory = path;
        }
    }

    #endregion

    #region Commands - Database Operations

    [RelayCommand]
    private async Task LoadConfigurationFromDatabaseAsync(ConfigurationListItemViewModel? item)
    {
        if (item is null) return;

        var configuration = await _configurationRepository.GetByIdAsync(item.Id);
        if (configuration is null) return;

        _configuration = configuration;
        _currentFilePath = null;
        _currentFormat = null;
        ReloadCollectionsFromConfiguration();
        UpdateCompatibilityHint();
        ValidationSummary = string.Empty;
        PreviewPlan = string.Empty;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task SaveToDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!TryValidate(out var summary))
        {
            ValidationSummary = summary;
            return;
        }

        if (_nameService is null || _conflictResolution is null)
        {
            ValidationSummary = "Name service or conflict resolution service not available.";
            return;
        }

        var isNew = !await _configurationRepository.ExistsAsync(_configuration.Id, cancellationToken);
        var newName = await _nameService.PromptForNameAsync(_configuration.Name, _configuration.Id);

        if (string.IsNullOrWhiteSpace(newName)) return;

        _configuration.Name = newName;
        ValidationSummary = summary;

        var exists = await _configurationRepository.ExistsAsync(_configuration.Id, cancellationToken);

        if (exists && !isNew)
        {
            var resolution = await _conflictResolution.ResolveSaveConflictAsync(_configuration.Name);

            switch (resolution)
            {
                case SaveConflictResolution.Cancel:
                    return;
                case SaveConflictResolution.Overwrite:
                    await _configurationRepository.SaveAsync(_configuration, cancellationToken);
                    break;
                case SaveConflictResolution.SaveAsNew:
                    _configuration = await _configurationRepository.SaveAsNewAsync(_configuration, cancellationToken);
                    ReloadCollectionsFromConfiguration();
                    break;
            }
        }
        else
        {
            await _configurationRepository.SaveAsync(_configuration, cancellationToken);
        }

        await LoadSavedConfigurationsAsync();
        ValidationSummary = $"Configuration '{_configuration.Name}' saved successfully.";
    }

    [RelayCommand]
    private async Task RenameConfigurationAsync(CancellationToken cancellationToken)
    {
        if (SelectedSavedConfiguration is null)
        {
            ValidationSummary = "No configuration selected to rename.";
            return;
        }

        if (_nameService is null)
        {
            ValidationSummary = "Name service not available.";
            return;
        }

        var configuration = await _configurationRepository.GetByIdAsync(SelectedSavedConfiguration.Id, cancellationToken);
        if (configuration is null)
        {
            ValidationSummary = "Configuration not found.";
            return;
        }

        var newName = await _nameService.PromptForNameAsync(configuration.Name, configuration.Id);
        if (string.IsNullOrWhiteSpace(newName)) return;

        configuration.Name = newName;
        await _configurationRepository.SaveAsync(configuration, cancellationToken);
        await LoadSavedConfigurationsAsync();

        if (_configuration.Id == configuration.Id)
        {
            _configuration.Name = newName;
        }

        ValidationSummary = $"Configuration renamed to '{newName}' successfully.";
    }

    [RelayCommand]
    private async Task DeleteConfigurationAsync(CancellationToken cancellationToken)
    {
        if (SelectedSavedConfiguration is null)
        {
            ValidationSummary = "No configuration selected to delete.";
            return;
        }

        if (_configurationManagement is null)
        {
            ValidationSummary = "Configuration management service not available.";
            return;
        }

        var configName = SelectedSavedConfiguration.Name;
        var confirmed = await _configurationManagement.ConfirmDeleteAsync(configName);
        if (!confirmed) return;

        var configIdToDelete = SelectedSavedConfiguration.Id;
        await _configurationRepository.DeleteAsync(configIdToDelete, cancellationToken);
        await LoadSavedConfigurationsAsync();

        if (_configuration.Id == configIdToDelete)
        {
            NewConfiguration();
        }

        ValidationSummary = $"Configuration '{configName}' deleted successfully.";
    }

    #endregion

    #region Private Helpers

    private async Task LoadSavedConfigurationsAsync()
    {
        var configurations = await _configurationRepository.GetAllAsync();
        SavedConfigurations.Clear();
        foreach (var config in configurations)
        {
            SavedConfigurations.Add(new ConfigurationListItemViewModel(config, MarkDirty));
        }
    }

    private string[] GetAvailableVramProfiles()
    {
        if (!CreateVramSettings || string.IsNullOrWhiteSpace(VramProfiles))
        {
            return [];
        }

        return VramProfiles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.EndsWith('+') ? p : p + "GB")
            .ToArray();
    }

    private async Task SaveConfigurationAsync(string path, ConfigurationFormat format, CancellationToken cancellationToken)
    {
        if (!TryValidate(out var summary))
        {
            ValidationSummary = summary;
            return;
        }

        ValidationSummary = summary;
        await _configurationService.SaveAsync(_configuration, path, format, cancellationToken);
    }

    private async Task ExecuteEngineAsync(Func<IProgress<InstallLogEntry>, Task> execute)
    {
        if (IsBusy) return;

        IsBusy = true;
        Logs.Clear();

        var progress = new Progress<InstallLogEntry>(entry =>
        {
            var vm = new InstallLogEntryViewModel(entry);
            Logs.Add(vm);
            SelectedLogEntry = vm;
        });

        try
        {
            await execute(progress);
        }
        catch (OperationCanceledException)
        {
            Logs.Add(new InstallLogEntryViewModel(new InstallLogEntry
            {
                Level = LogLevel.Warning,
                Message = "Operation cancelled."
            }));
        }
        catch (Exception ex)
        {
            Logs.Add(new InstallLogEntryViewModel(new InstallLogEntry
            {
                Level = LogLevel.Error,
                Message = ex.Message
            }));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate(out string summary)
    {
        var validation = _configuration.Validate();
        if (validation.IsValid && validation.Warnings.Count == 0)
        {
            summary = "Configuration is valid.";
            return true;
        }

        var lines = validation.Errors
            .Select(e => $"Error: {e}")
            .Concat(validation.Warnings.Select(w => $"Warning: {w}"));
        summary = string.Join(Environment.NewLine, lines);
        return validation.IsValid;
    }

    private void UpdateRepositoryPriorities()
    {
        for (var index = 0; index < GitRepositories.Count; index++)
        {
            GitRepositories[index].Priority = index + 1;
        }
    }

    private void ReloadCollectionsFromConfiguration()
    {
        GitRepositories.Clear();
        ModelDownloads.Clear();

        foreach (var repo in _configuration.GitRepositories.OrderBy(r => r.Priority))
        {
            GitRepositories.Add(new GitRepositoryItemViewModel(repo, MarkDirty));
        }

        foreach (var model in _configuration.ModelDownloads)
        {
            ModelDownloads.Add(new ModelDownloadItemViewModel(model, MarkDirty));
        }

        UpdateRepositoryPriorities();
    }

    private void UpdateCompatibilityHint()
    {
        TorchCompatibilityHint = _installationEngine.GetTorchCompatibilityHint(_configuration.Torch);
    }

    private void MarkDirty()
    {
        ValidationSummary = string.Empty;
        PreviewPlan = string.Empty;
    }

    #endregion
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
    }

    public ModelDownload Model { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _destination = string.Empty;
    [ObservableProperty] private VramProfile _vramProfile;
    [ObservableProperty] private bool _enabled;

    partial void OnNameChanged(string value) { Model.Name = value; _onChanged(); }
    partial void OnUrlChanged(string value) { Model.Url = value; _onChanged(); }
    partial void OnDestinationChanged(string value) { Model.Destination = value; _onChanged(); }
    partial void OnVramProfileChanged(VramProfile value) { Model.VramProfile = value; _onChanged(); }
    partial void OnEnabledChanged(bool value) { Model.Enabled = value; _onChanged(); }
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
