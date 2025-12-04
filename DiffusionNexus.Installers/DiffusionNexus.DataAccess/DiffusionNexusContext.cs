using DiffusionNexus.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiffusionNexus.DataAccess
{
    public class DiffusionNexusContext : DbContext
    {
        public DbSet<InstallationConfiguration> InstallationConfigurations { get; set; }

        public string DbPath { get; }

        public DiffusionNexusContext(DbContextOptions<DiffusionNexusContext> options) : base(options)
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "diffusion_nexus.db");
            
            Database.EnsureCreated();
        }

        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                options.UseSqlite($"Data Source={DbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InstallationConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.Name).IsUnique();

                // Configure owned types (complex types that don't need their own tables)
                entity.OwnsOne(e => e.Repository, navigation =>
                {
                    navigation.Property(r => r.Type).IsRequired();
                    navigation.Property(r => r.RepositoryUrl).IsRequired();
                });

                entity.OwnsOne(e => e.Python, navigation =>
                {
                    navigation.Property(p => p.PythonVersion).IsRequired();
                });

                entity.OwnsOne(e => e.Torch);

                entity.OwnsOne(e => e.Paths, navigation =>
                {
                    navigation.Property(p => p.RootDirectory).IsRequired();
                });

                entity.OwnsOne(e => e.Vram);

                // Configure collections - these will be separate tables with foreign keys
                entity.HasMany(e => e.GitRepositories)
                    .WithOne()
                    .HasForeignKey("InstallationConfigurationId")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ModelDownloads)
                    .WithOne()
                    .HasForeignKey("InstallationConfigurationId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GitRepository>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<ModelDownload>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Configure the DownloadLinks collection
                entity.HasMany(m => m.DownloadLinks)
                    .WithOne()
                    .HasForeignKey("ModelDownloadId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModelDownloadLink>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }

    /// <summary>
    /// Design-time factory for Entity Framework Core migrations.
    /// This allows EF tools to create instances of the DbContext at design time.
    /// </summary>
    public class DiffusionNexusContextFactory : IDesignTimeDbContextFactory<DiffusionNexusContext>
    {
        public DiffusionNexusContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DiffusionNexusContext>();
            
            // Use a temporary path for design-time operations
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            var dbPath = System.IO.Path.Join(path, "diffusion_nexus.db");
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new DiffusionNexusContext(optionsBuilder.Options);
        }
    }
}
