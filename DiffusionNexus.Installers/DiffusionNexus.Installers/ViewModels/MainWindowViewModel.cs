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

namespace DiffusionNexus.Installers.ViewModels
{
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

    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ConfigurationService _configurationService = new();
        private readonly InstallationEngine _installationEngine = new();
        private InstallationConfiguration _configuration = new();
        private string? _currentFilePath;
        private ConfigurationFormat? _currentFormat;
        private IStorageInteractionService? _storageInteraction;
        private IGitRepositoryInteractionService? _gitRepositoryInteraction;

        public event EventHandler<GitRepositoryItemViewModel>? EditRepositoryRequested;

        public MainWindowViewModel()
        {
            GitRepositories = new ObservableCollection<GitRepositoryItemViewModel>();
            ModelDownloads = new ObservableCollection<ModelDownloadItemViewModel>();
            Logs = new ObservableCollection<InstallLogEntryViewModel>();

            NewConfiguration();
        }

        public ObservableCollection<GitRepositoryItemViewModel> GitRepositories { get; }

        public ObservableCollection<ModelDownloadItemViewModel> ModelDownloads { get; }

        public ObservableCollection<InstallLogEntryViewModel> Logs { get; }

        public RepositoryType[] RepositoryTypes { get; } =
            Enum.GetValues<RepositoryType>();

        public string[] PythonVersions { get; } =
            new[] { "3.8", "3.9", "3.10", "3.11", "3.12", "3.13" };

        public string[] SuggestedCudaVersions { get; } =
            new[] { "12.8", "12.4", "12.1", "11.8" };

        public string[] SuggestedTorchVersions { get; } =
            new[] { string.Empty, "2.4.0", "2.3.1", "2.2.2" };

        public VramProfile[] VramProfiles { get; } =
            Enum.GetValues<VramProfile>();

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

        public RepositoryType SelectedRepositoryType
        {
            get => _configuration.Repository.Type;
            set
            {
                if (_configuration.Repository.Type != value)
                {
                    _configuration.Repository.Type = value;
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

        public void AttachStorageInteraction(IStorageInteractionService storageInteraction)
        {
            _storageInteraction = storageInteraction;
        }

        public void AttachGitRepositoryInteraction(IGitRepositoryInteractionService gitRepositoryInteraction)
        {
            _gitRepositoryInteraction = gitRepositoryInteraction;
        }

        [RelayCommand]
        private void NewConfiguration()
        {
            _configuration = new InstallationConfiguration
            {
                Repository = new MainRepositorySettings
                {
                    Type = RepositoryType.ComfyUI,
                    RepositoryUrl = "https://github.com/comfyanonymous/ComfyUI"
                },
                Python = new PythonEnvironmentSettings
                {
                    PythonVersion = PythonVersions.Last(),
                    CreateVirtualEnvironment = true,
                    VirtualEnvironmentName = "venv"
                },
                Torch = new TorchSettings
                {
                    CudaVersion = SuggestedCudaVersions.First(),
                    TorchVersion = SuggestedTorchVersions[1]
                },
                Paths = new PathSettings
                {
                    RootDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "DiffusionNexus"),
                    LogFileName = "install.log"
                }
            };

            _currentFilePath = null;
            _currentFormat = null;
            GitRepositories.Clear();
            ModelDownloads.Clear();
            Logs.Clear();
            PreviewPlan = string.Empty;
            ValidationSummary = string.Empty;
            ReloadCollectionsFromConfiguration();
            UpdateCompatibilityHint();
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private async Task OpenAsync(CancellationToken cancellationToken)
        {
            if (_storageInteraction is null)
            {
                return;
            }

            var path = await _storageInteraction.PickOpenFileAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

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
            if (_storageInteraction is null)
            {
                return;
            }

            var path = await _storageInteraction.PickSaveFileAsync(_currentFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var format = ConfigurationService.GetFormatFromExtension(path);
            await SaveConfigurationAsync(path, format, cancellationToken);
            _currentFilePath = path;
            _currentFormat = format;
        }

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
            await ExecuteEngineAsync(progress => _installationEngine.DryRunAsync(_configuration, progress, cancellationToken));
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
            await ExecuteEngineAsync(progress => _installationEngine.InstallAsync(_configuration, progress, cancellationToken));
        }

        [RelayCommand]
        private async Task AddRepositoryAsync()
        {
            if (_gitRepositoryInteraction is null)
            {
                return;
            }

            var repository = await _gitRepositoryInteraction.CreateRepositoryAsync();
            if (repository is null)
            {
                return;
            }

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
            if (SelectedRepository is null)
            {
                return;
            }

            _configuration.GitRepositories.Remove(SelectedRepository.Model);
            GitRepositories.Remove(SelectedRepository);
            UpdateRepositoryPriorities();
            MarkDirty();
        }

        [RelayCommand]
        private void MoveRepositoryUp()
        {
            if (SelectedRepository is null)
            {
                return;
            }

            var index = GitRepositories.IndexOf(SelectedRepository);
            if (index <= 0)
            {
                return;
            }

            GitRepositories.Move(index, index - 1);
            _configuration.GitRepositories.Remove(SelectedRepository.Model);
            _configuration.GitRepositories.Insert(index - 1, SelectedRepository.Model);
            UpdateRepositoryPriorities();
            MarkDirty();
        }

        [RelayCommand]
        private void MoveRepositoryDown()
        {
            if (SelectedRepository is null)
            {
                return;
            }

            var index = GitRepositories.IndexOf(SelectedRepository);
            if (index < 0 || index >= GitRepositories.Count - 1)
            {
                return;
            }

            GitRepositories.Move(index, index + 1);
            _configuration.GitRepositories.Remove(SelectedRepository.Model);
            _configuration.GitRepositories.Insert(index + 1, SelectedRepository.Model);
            UpdateRepositoryPriorities();
            MarkDirty();
        }

        [RelayCommand]
        private async Task EditRepositoryAsync(GitRepositoryItemViewModel? repository)
        {
            if (repository is null)
            {
                return;
            }

            SelectedRepository = repository;
            EditRepositoryRequested?.Invoke(this, repository);

            if (_gitRepositoryInteraction is null)
            {
                return;
            }

            var updatedRepository = await _gitRepositoryInteraction.EditRepositoryAsync(repository.Model);
            if (updatedRepository is null)
            {
                return;
            }

            repository.Name = updatedRepository.Name;
            repository.Url = updatedRepository.Url;
            repository.InstallRequirements = updatedRepository.InstallRequirements;
            MarkDirty();
        }

        [RelayCommand]
        private void DeleteRepository(GitRepositoryItemViewModel? repository)
        {
            if (repository is null)
            {
                return;
            }

            var index = GitRepositories.IndexOf(repository);
            _configuration.GitRepositories.Remove(repository.Model);
            GitRepositories.Remove(repository);

            if (SelectedRepository == repository)
            {
                if (GitRepositories.Count == 0)
                {
                    SelectedRepository = null;
                }
                else
                {
                    var nextIndex = Math.Clamp(index, 0, GitRepositories.Count - 1);
                    SelectedRepository = GitRepositories[nextIndex];
                }
            }

            UpdateRepositoryPriorities();
            MarkDirty();
        }

        [RelayCommand]
        private void AddModel()
        {
            var model = new ModelDownload
            {
                Name = "New Model",
                VramProfile = VramProfile.VRAM_16GB,
                Enabled = true
            };
            var vm = new ModelDownloadItemViewModel(model, MarkDirty);
            ModelDownloads.Add(vm);
            _configuration.ModelDownloads.Add(model);
            SelectedModel = vm;
            MarkDirty();
        }

        [RelayCommand]
        private void RemoveModel()
        {
            if (SelectedModel is null)
            {
                return;
            }

            _configuration.ModelDownloads.Remove(SelectedModel.Model);
            ModelDownloads.Remove(SelectedModel);
            MarkDirty();
        }

        [RelayCommand]
        private async Task BrowseInterpreterAsync(CancellationToken cancellationToken)
        {
            if (_storageInteraction is null)
            {
                return;
            }

            var path = await _storageInteraction.PickOpenFileAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(path))
            {
                InterpreterPathOverride = path;
            }
        }

        [RelayCommand]
        private async Task BrowseRootDirectoryAsync(CancellationToken cancellationToken)
        {
            if (_storageInteraction is null)
            {
                return;
            }

            var path = await _storageInteraction.PickFolderAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(path))
            {
                RootDirectory = path;
            }
        }

        [RelayCommand]
        private async Task BrowseModelDirectoryAsync(CancellationToken cancellationToken)
        {
            if (_storageInteraction is null)
            {
                return;
            }

            var path = await _storageInteraction.PickFolderAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(path))
            {
                DefaultModelDirectory = path;
            }
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
            if (IsBusy)
            {
                return;
            }

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
                var vm = new GitRepositoryItemViewModel(repo, MarkDirty);
                GitRepositories.Add(vm);
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
    }

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

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private bool _installRequirements;

        [ObservableProperty]
        private int _priority;

        partial void OnNameChanged(string value)
        {
            Model.Name = value;
            _onChanged();
        }

        partial void OnUrlChanged(string value)
        {
            Model.Url = value;
            _onChanged();
        }

        partial void OnInstallRequirementsChanged(bool value)
        {
            Model.InstallRequirements = value;
            _onChanged();
        }

        partial void OnPriorityChanged(int value)
        {
            Model.Priority = value;
            _onChanged();
        }
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

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private string _destination = string.Empty;

        [ObservableProperty]
        private VramProfile _vramProfile;

        [ObservableProperty]
        private bool _enabled;

        partial void OnNameChanged(string value)
        {
            Model.Name = value;
            _onChanged();
        }

        partial void OnUrlChanged(string value)
        {
            Model.Url = value;
            _onChanged();
        }

        partial void OnDestinationChanged(string value)
        {
            Model.Destination = value;
            _onChanged();
        }

        partial void OnVramProfileChanged(VramProfile value)
        {
            Model.VramProfile = value;
            _onChanged();
        }

        partial void OnEnabledChanged(bool value)
        {
            Model.Enabled = value;
            _onChanged();
        }
    }

    public class InstallLogEntryViewModel
    {
        public InstallLogEntryViewModel(InstallLogEntry entry)
        {
            Timestamp = entry.Timestamp;
            Message = entry.Message;
            Level = entry.Level;
        }

        public DateTimeOffset Timestamp { get; }
        public string Message { get; }
        public LogLevel Level { get; }
        public string Display => $"[{Timestamp:HH:mm:ss}] {Level}: {Message}";
    }
}
