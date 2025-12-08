// File: DiffusionNexus.DataAccess/ServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiffusionNexus.DataAccess;

/// <summary>
/// Extension methods for registering data access services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DiffusionNexus data access services with SQLite.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Optional custom database path. If null, uses default LocalApplicationData path.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDiffusionNexusDataAccess(
        this IServiceCollection services,
        string? databasePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var dbPath = databasePath ?? GetDefaultDatabasePath();

        // For desktop applications, use Singleton lifetime since there's no HTTP request scope
        services.AddDbContext<DiffusionNexusContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        }, ServiceLifetime.Singleton);

        // Use Singleton for desktop applications (no HTTP request scope available)
        services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();

        // Register database management service as singleton (holds path and import event)
        services.AddSingleton<IDatabaseManagementService>(sp =>
            new DatabaseManagementService(dbPath, sp));

        return services;
    }

    /// <summary>
    /// Gets the default database path in LocalApplicationData.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        return Path.Join(path, "diffusion_nexus.db");
    }
}
