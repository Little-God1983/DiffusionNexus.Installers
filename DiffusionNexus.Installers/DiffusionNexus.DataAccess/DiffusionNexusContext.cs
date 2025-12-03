using DiffusionNexus.Core.Models;
using Microsoft.EntityFrameworkCore;

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
        }

        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InstallationConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);

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
            });
        }
    }
}
