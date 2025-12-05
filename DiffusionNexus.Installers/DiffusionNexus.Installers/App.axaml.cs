using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DiffusionNexus.Core.Services;
using DiffusionNexus.DataAccess;
using DiffusionNexus.Installers.ViewModels;
using DiffusionNexus.Installers.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DiffusionNexus.Installers
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets the service provider for resolving dependencies.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                // Resolve MainWindowViewModel from DI
                var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                desktop.ShutdownRequested += OnShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Register data access layer
            services.AddDiffusionNexusDataAccess();

            // Register Core Services
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<IPythonService, PythonService>();
            services.AddSingleton<IInstallationOrchestrator, InstallationOrchestrator>();
            
            // Register InstallationEngine with orchestrator
            services.AddSingleton<InstallationEngine>(sp =>
            {
                var orchestrator = sp.GetRequiredService<IInstallationOrchestrator>();
                return new InstallationEngine(orchestrator);
            });

            // Register ViewModels
            services.AddTransient<MainWindowViewModel>();
        }

        private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            _serviceProvider?.Dispose();
        }

        private static void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}