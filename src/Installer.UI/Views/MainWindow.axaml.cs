using System;
using System.IO;
using Avalonia.Controls;
using Installer.Core.Installation;
using Installer.Core.Manifests;
using Installer.Core.Settings;
using Installer.UI.Services;
using Installer.UI.ViewModels;

namespace Installer.UI.Views;

public partial class MainWindow : Window
{
    private readonly ManifestProvider _manifestProvider;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var manifestDirectory = Path.Combine(AppContext.BaseDirectory, "manifests");
        _manifestProvider = new ManifestProvider(manifestDirectory);

        var installerEngine = new InstallerEngine();
        var settingsService = new UserSettingsService();
        var interactionService = new DesktopInteractionService(this);
        _viewModel = new MainWindowViewModel(_manifestProvider, installerEngine, settingsService, interactionService);
        DataContext = _viewModel;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await _viewModel.InitializeAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
        _manifestProvider.Dispose();
    }
}
