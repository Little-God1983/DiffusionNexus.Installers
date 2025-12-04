using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DiffusionNexus.Installers.ViewModels;

namespace DiffusionNexus.Installers.Views;

public partial class InstallationView : UserControl, IFolderPickerService
{
    public InstallationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is InstallationViewModel viewModel)
        {
            viewModel.AttachFolderPickerService(this);
        }
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Installation Folder",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
