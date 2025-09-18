using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Installer.UI.Services;

public sealed class DesktopInteractionService : IUserInteractionService
{
    private readonly Window _window;

    public DesktopInteractionService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public async Task<string?> BrowseForFolderAsync(string? initialDirectory, CancellationToken cancellationToken)
    {
        if (_window.StorageProvider is null)
        {
            var dialog = new OpenFolderDialog
            {
                Directory = initialDirectory
            };
            return await dialog.ShowAsync(_window);
        }

        var options = new FolderPickerOpenOptions
        {
            SuggestedStartLocation = await TryGetStartLocationAsync(initialDirectory),
            AllowMultiple = false
        };

        var folders = await _window.StorageProvider.OpenFolderPickerAsync(options);
        var folder = folders?.Count > 0 ? folders[0] : null;
        return folder?.Path.LocalPath;
    }

    public async Task<string?> SaveLogFileAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(suggestedFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".txt";
        }

        if (_window.StorageProvider is null)
        {
            var dialog = new SaveFileDialog
            {
                InitialFileName = suggestedFileName,
                Filters =
                {
                    new FileDialogFilter { Name = "Text files", Extensions = { "txt" } }
                }
            };
            return await dialog.ShowAsync(_window);
        }

        var saveOptions = new FilePickerSaveOptions
        {
            SuggestedStartLocation = await TryGetStartLocationAsync(null),
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } }
            }
        };

        var file = await _window.StorageProvider.SaveFilePickerAsync(saveOptions);
        return file?.Path.LocalPath;
    }

    public async Task SetClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        var clipboard = TopLevel.GetTopLevel(_window)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private async Task<IStorageFolder?> TryGetStartLocationAsync(string? initialDirectory)
    {
        if (_window.StorageProvider is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            try
            {
                return await _window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
