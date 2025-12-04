using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DiffusionNexus.Core.Models;
using DiffusionNexus.Installers.ViewModels;

namespace DiffusionNexus.Installers.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.AttachStorageInteraction(new AvaloniaStorageInteractionService(this));
                vm.AttachGitRepositoryInteraction(new AvaloniaGitRepositoryInteractionService(this));
                vm.AttachConflictResolutionService(new AvaloniaConflictResolutionService(this));
                vm.AttachConfigurationNameService(new AvaloniaConfigurationNameService(this, vm));
            }
        }

        private void OnMoveUpClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is GitRepositoryItemViewModel repository &&
                DataContext is MainWindowViewModel vm)
            {
                vm.MoveRepositoryUpWithParameter(repository);
            }
        }

        private void OnMoveDownClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is GitRepositoryItemViewModel repository &&
                DataContext is MainWindowViewModel vm)
            {
                vm.MoveRepositoryDownWithParameter(repository);
            }
        }

        private sealed class AvaloniaStorageInteractionService : IStorageInteractionService
        {
            private readonly Window _window;

            public AvaloniaStorageInteractionService(Window window)
            {
                _window = window;
            }

            public async Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
            {
                var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open Configuration",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Configuration Files")
                        {
                            Patterns = new[] { "*.json", "*.xml" }
                        },
                        new FilePickerFileType("JSON Files")
                        {
                            Patterns = new[] { "*.json" }
                        },
                        new FilePickerFileType("XML Files")
                        {
                            Patterns = new[] { "*.xml" }
                        },
                        FilePickerFileTypes.All
                    }
                });

                return files.FirstOrDefault()?.Path.LocalPath;
            }

            public async Task<string?> PickSaveFileAsync(string? suggestedFileName, CancellationToken cancellationToken = default)
            {
                var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Configuration",
                    SuggestedFileName = suggestedFileName ?? "configuration.json",
                    DefaultExtension = "json",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("JSON Files")
                        {
                            Patterns = new[] { "*.json" }
                        },
                        new FilePickerFileType("XML Files")
                        {
                            Patterns = new[] { "*.xml" }
                        }
                    }
                });

                return file?.Path.LocalPath;
            }

            public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
            {
                var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder",
                    AllowMultiple = false
                });

                return folders.FirstOrDefault()?.Path.LocalPath;
            }
        }

        private sealed class AvaloniaGitRepositoryInteractionService : IGitRepositoryInteractionService
        {
            private readonly Window _window;

            public AvaloniaGitRepositoryInteractionService(Window window)
            {
                _window = window;
            }

            public Task<GitRepository?> CreateRepositoryAsync()
            {
                var draft = new GitRepository();
                var dialog = new GitRepositoryEditorWindow(draft, isNew: true);
                return dialog.ShowDialog<GitRepository?>(_window);
            }

            public async Task<GitRepository?> EditRepositoryAsync(GitRepository repository)
            {
                var draft = new GitRepository
                {
                    Id = repository.Id,
                    Name = repository.Name,
                    Url = repository.Url,
                    InstallRequirements = repository.InstallRequirements,
                    Priority = repository.Priority
                };

                var dialog = new GitRepositoryEditorWindow(draft, isNew: false);
                var result = await dialog.ShowDialog<GitRepository?>(_window);
                if (result is null)
                {
                    return null;
                }

                result.Id = repository.Id;
                result.Priority = repository.Priority;
                return result;
            }
        }

        private sealed class AvaloniaConflictResolutionService : IConflictResolutionService
        {
            private readonly Window _window;

            public AvaloniaConflictResolutionService(Window window)
            {
                _window = window;
            }

            public async Task<SaveConflictResolution> ResolveSaveConflictAsync(string configurationName)
            {
                var dialog = new SaveConflictDialog(configurationName);
                var result = await dialog.ShowDialog<SaveConflictResolution>(_window);
                return result;
            }
        }

        private sealed class AvaloniaConfigurationNameService : IConfigurationNameService
        {
            private readonly Window _window;
            private readonly MainWindowViewModel _viewModel;

            public AvaloniaConfigurationNameService(Window window, MainWindowViewModel viewModel)
            {
                _window = window;
                _viewModel = viewModel;
            }

            public async Task<string?> PromptForNameAsync(string currentName, Guid? excludeId)
            {
                var dialog = new ConfigurationNameDialog(
                    currentName, 
                    excludeId,
                    async (name, excludeGuid) =>
                    {
                        // Use the repository from the view model's context
                        var context = new DiffusionNexus.DataAccess.DiffusionNexusContext(
                            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DiffusionNexus.DataAccess.DiffusionNexusContext>().Options);
                        var repo = new DiffusionNexus.DataAccess.ConfigurationRepository(context);
                        return await repo.NameExistsAsync(name, excludeGuid);
                    });
                
                var result = await dialog.ShowDialog<string?>(_window);
                return result;
            }
        }
    }
}
