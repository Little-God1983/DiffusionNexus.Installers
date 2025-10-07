using Avalonia;
using DiffusionNexus.Installers;
using Serilog;
using System;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
             .WriteTo.File("logs/Install.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Avalonia");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args); // <-- keep args here
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "App crashed on startup");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace(); // or .LogToSerilog() if using the adapter
}
