using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
            }
        }

        private sealed class AvaloniaStorageInteractionService : IStorageInteractionService
        {
            private static readonly FilePickerFileType AllFilesFilter = new("All files")
            {
                Patterns = new[] { "*" }
            };

            private readonly Window _window;

            public AvaloniaStorageInteractionService(Window window)
            {
                _window = window;
            }

            public async Task<string?> PickOpenFileAsync(CancellationToken cancellationToken = default)
            {
                if (!_window.StorageProvider.CanOpen)
                {
                    return null;
                }

                var options = new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Configuration files")
                        {
                            Patterns = new[] { "*.json", "*.xml" }
                        }
                    }
                };

                var results = await _window.StorageProvider.OpenFilePickerAsync(options);
                return results?.FirstOrDefault()?.TryGetLocalPath();
            }

            public async Task<string?> PickSaveFileAsync(string? suggestedFileName, CancellationToken cancellationToken = default)
            {
                if (!_window.StorageProvider.CanSave)
                {
                    return null;
                }

                var options = new FilePickerSaveOptions
                {
                    SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName)
                        ? "configuration.json"
                        : System.IO.Path.GetFileName(suggestedFileName),
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("JSON configuration")
                        {
                            Patterns = new[] { "*.json" }
                        },
                        new FilePickerFileType("XML configuration")
                        {
                            Patterns = new[] { "*.xml" }
                        }
                    }
                };

                var result = await _window.StorageProvider.SaveFilePickerAsync(options);
                return result?.TryGetLocalPath();
            }

            public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
            {
                if (!_window.StorageProvider.CanOpen)
                {
                    return null;
                }

                var results = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    AllowMultiple = false
                });

                return results?.FirstOrDefault()?.TryGetLocalPath();
            }

            public async Task<string?> PickInterpreterAsync(CancellationToken cancellationToken = default)
            {
                if (!_window.StorageProvider.CanOpen)
                {
                    return null;
                }

                var options = new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter = new[] { AllFilesFilter }
                };

                var results = await _window.StorageProvider.OpenFilePickerAsync(options);
                return results?.FirstOrDefault()?.TryGetLocalPath();
            }
        }
    }
}
