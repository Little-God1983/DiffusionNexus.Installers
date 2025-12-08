using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DiffusionNexus.Core.Models.Entities;
using DiffusionNexus.Core.Models.Enums;
using DiffusionNexus.Installers.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DiffusionNexus.Installers.Views;

public partial class ConfigurationView : UserControl
{
    private bool _servicesAttached;

    public ConfigurationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        TryAttachServices();
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        TryAttachServices();
    }

    private void TryAttachServices()
    {
        if (_servicesAttached) return;

        if (DataContext is not ConfigurationViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        vm.AttachStorageInteraction(new AvaloniaStorageInteractionService(window));
        vm.AttachGitRepositoryInteraction(new AvaloniaGitRepositoryInteractionService(window));
        vm.AttachModelEditorInteraction(new AvaloniaModelEditorInteractionService(window));
        vm.AttachConflictResolutionService(new AvaloniaConflictResolutionService(window));
        vm.AttachConfigurationNameService(new AvaloniaConfigurationNameService(window, vm));
        vm.AttachConfigurationManagementService(new AvaloniaConfigurationManagementService(window));
        vm.AttachDatabaseExportInteraction(new AvaloniaDatabaseExportInteractionService(window));
        
        _servicesAttached = true;
    }

    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is GitRepositoryItemViewModel repository &&
            DataContext is ConfigurationViewModel vm)
        {
            vm.MoveRepositoryUpWithParameter(repository);
        }
    }

    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is GitRepositoryItemViewModel repository &&
            DataContext is ConfigurationViewModel vm)
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
                FileTypeFilter =
                [
                    new FilePickerFileType("Configuration Files")
                    {
                        Patterns = ["*.json", "*.xml"]
                    },
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = ["*.json"]
                    },
                    new FilePickerFileType("XML Files")
                    {
                        Patterns = ["*.xml"]
                    },
                    FilePickerFileTypes.All
                ]
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
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = ["*.json"]
                    },
                    new FilePickerFileType("XML Files")
                    {
                        Patterns = ["*.xml"]
                    }
                ]
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

    private sealed class AvaloniaModelEditorInteractionService : IModelEditorInteractionService
    {
        private readonly Window _window;

        public AvaloniaModelEditorInteractionService(Window window)
        {
            _window = window;
        }

        public Task<ModelDownload?> CreateModelAsync(bool vramEnabled, string[] availableVramProfiles)
        {
            var draft = new ModelDownload
            {
                Name = "New Model",
                Enabled = true
            };
            var dialog = new ModelEditorDialog(draft, vramEnabled, availableVramProfiles, isNew: true);
            return dialog.ShowDialog<ModelDownload?>(_window);
        }

        public async Task<ModelDownload?> EditModelAsync(ModelDownload model, bool vramEnabled, string[] availableVramProfiles)
        {
            var draft = new ModelDownload
            {
                Id = model.Id,
                Name = model.Name,
                Destination = model.Destination,
                Enabled = model.Enabled,
                DownloadLinks = model.DownloadLinks.Select(link => new ModelDownloadLink
                {
                    Id = link.Id,
                    Url = link.Url,
                    VramProfile = link.VramProfile,
                    Destination = link.Destination,
                    Enabled = link.Enabled
                }).ToList()
            };

            var dialog = new ModelEditorDialog(draft, vramEnabled, availableVramProfiles, isNew: false);
            var result = await dialog.ShowDialog<ModelDownload?>(_window);
            if (result is null)
            {
                return null;
            }

            result.Id = model.Id;
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
        private readonly ConfigurationViewModel _viewModel;

        public AvaloniaConfigurationNameService(Window window, ConfigurationViewModel viewModel)
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
                    // Use the repository from DI instead of creating an unconfigured context
                    var repo = App.Services.GetRequiredService<DataAccess.IConfigurationRepository>();
                    return await repo.NameExistsAsync(name, excludeGuid);
                });

            var result = await dialog.ShowDialog<string?>(_window);
            return result;
        }
    }

    private sealed class AvaloniaConfigurationManagementService : IConfigurationManagementService
    {
        private readonly Window _window;

        public AvaloniaConfigurationManagementService(Window window)
        {
            _window = window;
        }

        public async Task<bool> ConfirmDeleteAsync(string configurationName)
        {
            var dialog = new Window
            {
                Title = "Confirm Delete",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var messageText = new TextBlock
            {
                Text = $"Are you sure you want to delete the configuration '{configurationName}'?\n\nThis action cannot be undone.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(16)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Avalonia.Thickness(16, 0, 16, 16)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100
            };

            var deleteButton = new Button
            {
                Content = "Delete",
                Width = 100,
                Background = Avalonia.Media.Brushes.Red,
                Foreground = Avalonia.Media.Brushes.White
            };

            cancelButton.Click += (s, e) => dialog.Close(false);
            deleteButton.Click += (s, e) => dialog.Close(true);

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(deleteButton);

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(messageText);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;

            var result = await dialog.ShowDialog<bool>(_window);
            return result;
        }
    }

    private sealed class AvaloniaDatabaseExportInteractionService : IDatabaseExportInteractionService
    {
        private readonly Window _window;

        public AvaloniaDatabaseExportInteractionService(Window window)
        {
            _window = window;
        }

        public async Task<string?> PickExportPathAsync(string suggestedFileName, CancellationToken cancellationToken = default)
        {
            var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Database",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "db",
                FileTypeChoices =
                [
                    new FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db"]
                    },
                    FilePickerFileTypes.All
                ]
            });

            return file?.Path.LocalPath;
        }

        public async Task<string?> PickImportPathAsync(CancellationToken cancellationToken = default)
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Database",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db"]
                    },
                    FilePickerFileTypes.All
                ]
            });

            return files.FirstOrDefault()?.Path.LocalPath;
        }

        public async Task<bool> ConfirmImportAsync()
        {
            var dialog = new Window
            {
                Title = "Confirm Database Import",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var messageText = new TextBlock
            {
                Text = "Are you sure you want to import this database?\n\nThis will replace all existing configurations with the ones from the imported database.\n\nThis action cannot be undone.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(16)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Avalonia.Thickness(16, 0, 16, 16)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100
            };

            var importButton = new Button
            {
                Content = "Import",
                Width = 100,
                Background = Avalonia.Media.Brushes.OrangeRed,
                Foreground = Avalonia.Media.Brushes.White
            };

            cancelButton.Click += (s, e) => dialog.Close(false);
            importButton.Click += (s, e) => dialog.Close(true);

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(importButton);

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(messageText);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;

            var result = await dialog.ShowDialog<bool>(_window);
            return result;
        }
    }
}
