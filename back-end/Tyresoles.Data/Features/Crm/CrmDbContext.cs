using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Data.Features.Crm;

public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options) { }

    public DbSet<CrmContactType> CrmContactTypes => Set<CrmContactType>();
    public DbSet<CrmSource> CrmSources => Set<CrmSource>();
    public DbSet<CrmStage> CrmStages => Set<CrmStage>();
    public DbSet<CrmPriority> CrmPriorities => Set<CrmPriority>();
    public DbSet<CrmActivityType> CrmActivityTypes => Set<CrmActivityType>();
    public DbSet<CrmActivityOutcome> CrmActivityOutcomes => Set<CrmActivityOutcome>();
    public DbSet<CrmContact> CrmContacts => Set<CrmContact>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<CrmContactType>(e =>
        {
            e.ToTable("CrmContactType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmSource>(e =>
        {
            e.ToTable("CrmSource");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmStage>(e =>
        {
            e.ToTable("CrmStage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmPriority>(e =>
        {
            e.ToTable("CrmPriority");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmActivityType>(e =>
        {
            e.ToTable("CrmActivityType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmActivityOutcome>(e =>
        {
            e.ToTable("CrmActivityOutcome");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmContact>(e =>
        {
            e.ToTable("CrmContact");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.ContactType).HasColumnType("nvarchar(max)");
            e.Property(x => x.CompanyName).HasColumnType("nvarchar(max)");
            e.Property(x => x.MobileNo).HasColumnType("nvarchar(max)");
            e.Property(x => x.MobileNo2).HasColumnType("nvarchar(max)");
            e.Property(x => x.EmailIds).HasColumnType("nvarchar(max)");
            e.Property(x => x.Address).HasColumnType("nvarchar(max)");
            e.Property(x => x.City).HasColumnType("nvarchar(max)");
            e.Property(x => x.State).HasColumnType("nvarchar(max)");
            e.Property(x => x.RespCenter).HasColumnType("nvarchar(max)");
            e.Property(x => x.ERPCustomerNos).HasColumnType("nvarchar(max)");
            e.Property(x => x.ERPAreaCodes).HasColumnType("nvarchar(max)");
            e.Property(x => x.Tags).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasColumnType("nvarchar(max)");
            e.Property(x => x.AssignedTo).HasColumnType("nvarchar(max)");
        });
    }
}
