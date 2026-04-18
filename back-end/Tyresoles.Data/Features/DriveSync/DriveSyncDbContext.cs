using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

public class DriveSyncDbContext : DbContext
{
    public DriveSyncDbContext(DbContextOptions<DriveSyncDbContext> options) : base(options) { }

    public DbSet<DriveSyncUserConfig> UserConfigs => Set<DriveSyncUserConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<DriveSyncUserConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.TargetFolderId).HasMaxLength(256);
            e.HasIndex(x => x.UserId).IsUnique();
        });
    }
}
