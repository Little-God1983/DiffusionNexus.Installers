using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace AIKnowledge2Go.Installers.UI.Services;

public sealed class DesktopDialogService : IDialogService
{
    private readonly Func<Window?> _windowAccessor;

    public DesktopDialogService(Func<Window?> windowAccessor)
    {
        _windowAccessor = windowAccessor;
    }

    public async Task<string?> BrowseForInstallRootAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var owner = _windowAccessor();
        if (owner is null)
        {
            return null;
        }

        var provider = owner.StorageProvider;
        if (provider is null)
        {
            return null;
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select installation folder",
            AllowMultiple = false,
        }).ConfigureAwait(false);

        return folders?.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> ExportLogAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var owner = _windowAccessor();
        if (owner is null)
        {
            return null;
        }

        var provider = owner.StorageProvider;
        if (provider is null)
        {
            return null;
        }

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export log",
            DefaultExtension = "txt",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } }
            }
        }).ConfigureAwait(false);

        return file?.TryGetLocalPath();
    }

    public async Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var owner = _windowAccessor();
        if (owner is null)
        {
            return;
        }

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var stackPanel = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(16),
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });

        stackPanel.Children.Add(okButton);

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 200,
            Content = stackPanel,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner).ConfigureAwait(false);
    }
}
