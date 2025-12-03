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
        }
    }
}
