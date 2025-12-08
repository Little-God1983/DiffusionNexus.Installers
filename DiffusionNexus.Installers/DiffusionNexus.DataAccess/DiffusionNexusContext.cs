using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiffusionNexus.DataAccess;

/// <summary>
/// Entity Framework Core DbContext for DiffusionNexus configurations.
/// Connection string should be injected via DbContextOptions.
/// </summary>
public sealed class DiffusionNexusContext : DbContext
{
    public DbSet<InstallationConfiguration> InstallationConfigurations { get; set; } = null!;

    public DiffusionNexusContext(DbContextOptions<DiffusionNexusContext> options) : base(options)
    {
        // Ensure database schema is created (suitable for SQLite/LocalDB installer scenarios)
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureInstallationConfiguration(modelBuilder);
        ConfigureGitRepository(modelBuilder);
        ConfigureModelDownload(modelBuilder);
        ConfigureModelDownloadLink(modelBuilder);
    }

    private static void ConfigureInstallationConfiguration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstallationConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();

            // Owned types (stored in same table as columns)
            entity.OwnsOne(e => e.Repository, nav =>
            {
                nav.Property(r => r.Type).IsRequired();
                nav.Property(r => r.RepositoryUrl).IsRequired();
            });

            entity.OwnsOne(e => e.Python, nav =>
            {
                nav.Property(p => p.PythonVersion).IsRequired();
            });

            entity.OwnsOne(e => e.Torch);
            entity.OwnsOne(e => e.Paths, nav =>
            {
                nav.Property(p => p.RootDirectory).IsRequired();
            });

            entity.OwnsOne(e => e.Vram);

            // Collections (separate tables)
            entity.HasMany(e => e.GitRepositories)
                .WithOne()
                .HasForeignKey("InstallationConfigurationId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ModelDownloads)
                .WithOne()
                .HasForeignKey("InstallationConfigurationId")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGitRepository(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitRepository>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }

    private static void ConfigureModelDownload(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModelDownload>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasMany(m => m.DownloadLinks)
                .WithOne()
                .HasForeignKey("ModelDownloadId")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureModelDownloadLink(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModelDownloadLink>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}

/// <summary>
/// Design-time factory for Entity Framework Core migrations.
/// </summary>
public sealed class DiffusionNexusContextFactory : IDesignTimeDbContextFactory<DiffusionNexusContext>
{
    public DiffusionNexusContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DiffusionNexusContext>();
        var dbPath = ServiceCollectionExtensions.GetDefaultDatabasePath();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new DiffusionNexusContext(optionsBuilder.Options);
    }
}
