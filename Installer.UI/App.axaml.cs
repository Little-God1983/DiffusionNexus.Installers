using System;
using System.IO;
using System.Linq;
using AIKnowledge2Go.Installers.Core.Installation;
using AIKnowledge2Go.Installers.Core.Manifests;
using AIKnowledge2Go.Installers.Core.Settings;
using AIKnowledge2Go.Installers.UI.Services;
using AIKnowledge2Go.Installers.UI.ViewModels;
using AIKnowledge2Go.Installers.UI.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AIKnowledge2Go.Installers.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            ConfigureServices(services, desktop);
            _serviceProvider = services.BuildServiceProvider();

            var manifestProvider = _serviceProvider.GetRequiredService<IManifestProvider>();
            manifestProvider.InitializeAsync().GetAwaiter().GetResult();

            var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            mainViewModel.InitializeAsync().GetAwaiter().GetResult();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var manifestsDirectory = Path.Combine(AppContext.BaseDirectory, "manifests");

        services.AddSingleton<IManifestProvider>(_ => new ManifestProvider(manifestsDirectory));
        services.AddSingleton<IInstallerEngine, InstallerEngine>();
        services.AddSingleton<IUserSettingsService, UserSettingsService>();
        services.AddSingleton<IDialogService>(_ => new DesktopDialogService(() => lifetime.MainWindow));
        services.AddSingleton<MainWindowViewModel>();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _serviceProvider?.Dispose();
    }
}
