using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.NavisionEdits.Entities;

namespace Tyresoles.Data.Features.NavisionEdits;

/// <summary>
/// EF Core context for the Navision Edit Request module.
/// Uses the Db_Extra database (same connection string as Calendar).
/// </summary>
public class NavEditDbContext : DbContext
{
    public NavEditDbContext(DbContextOptions<NavEditDbContext> options) : base(options) { }

    public DbSet<NavEditRequestType> RequestTypes => Set<NavEditRequestType>();
    public DbSet<NavEditRequest> Requests => Set<NavEditRequest>();
    public DbSet<NavEditApproval> Approvals => Set<NavEditApproval>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<NavEditRequestType>(e =>
        {
            e.ToTable("NavEditRequestTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(50);
            e.Property(x => x.NavTable).HasMaxLength(200).IsRequired();
            e.Property(x => x.NavPrimaryKeyColumn).HasMaxLength(200).IsRequired();
            e.Property(x => x.FieldsJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(128);
            e.Property(x => x.UpdatedBy).HasMaxLength(128);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<NavEditRequest>(e =>
        {
            e.ToTable("NavEditRequests");
            e.HasKey(x => x.Id);
            e.Property(x => x.RecordKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.RequestBody).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.UserId).HasMaxLength(128).IsRequired();
            e.Property(x => x.UserFullName).HasMaxLength(200);
            e.Property(x => x.Remark).HasMaxLength(1000);
            e.Property(x => x.AdminRemark).HasMaxLength(1000);
            e.Property(x => x.ProcessedBy).HasMaxLength(128);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.RequestType).WithMany().HasForeignKey(x => x.RequestTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NavEditApproval>(e =>
        {
            e.ToTable("NavEditApprovals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(100).IsRequired();
            e.Property(x => x.RoleLabel).HasMaxLength(200);
            e.Property(x => x.ApproverUserIdsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.ApprovedBy).HasMaxLength(128);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => x.RequestId);
            e.HasIndex(x => new { x.RequestId, x.Level });
            e.HasOne(x => x.Request).WithMany(r => r.Approvals).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
