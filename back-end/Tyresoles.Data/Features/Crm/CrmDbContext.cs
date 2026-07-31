using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Data.Features.Crm;

public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options) { }

    public DbSet<CrmEntityType> CrmEntityTypes => Set<CrmEntityType>();
    public DbSet<CrmContactType> CrmContactTypes => Set<CrmContactType>();
    public DbSet<CrmContactCategory> CrmContactCategories => Set<CrmContactCategory>();
    public DbSet<CrmSource> CrmSources => Set<CrmSource>();
    public DbSet<CrmStage> CrmStages => Set<CrmStage>();
    public DbSet<CrmPriority> CrmPriorities => Set<CrmPriority>();
    public DbSet<CrmActivityType> CrmActivityTypes => Set<CrmActivityType>();
    public DbSet<CrmActivityOutcome> CrmActivityOutcomes => Set<CrmActivityOutcome>();
    public DbSet<CrmContact> CrmContacts => Set<CrmContact>();
    public DbSet<CrmCallLog> CrmCallLogs => Set<CrmCallLog>();
    public DbSet<CrmCallReminder> CrmCallReminders => Set<CrmCallReminder>();
    public DbSet<CrmAgentContact> CrmAgentContacts => Set<CrmAgentContact>();
    public DbSet<CrmSetting> CrmSettings => Set<CrmSetting>();
    public DbSet<CrmWhatsappImage> CrmWhatsappImages => Set<CrmWhatsappImage>();
    public DbSet<CrmWhatsappTemplate> CrmWhatsappTemplates => Set<CrmWhatsappTemplate>();
    public DbSet<CrmContactFleetDetail> CrmContactFleetDetails => Set<CrmContactFleetDetail>();
    public DbSet<CrmFleetVehicleType> CrmFleetVehicleTypes => Set<CrmFleetVehicleType>();
    public DbSet<CrmFleetVehicleMake> CrmFleetVehicleMakes => Set<CrmFleetVehicleMake>();
    public DbSet<CrmFleetVehicleModel> CrmFleetVehicleModels => Set<CrmFleetVehicleModel>();
    public DbSet<CrmFleetApplication> CrmFleetApplications => Set<CrmFleetApplication>();
    public DbSet<CrmProduct> CrmProducts => Set<CrmProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<CrmEntityType>(e =>
        {
            e.ToTable("CrmEntityType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmContactType>(e =>
        {
            e.ToTable("CrmContactType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<CrmContactCategory>(e =>
        {
            e.ToTable("CrmContactCategory");
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
            e.Property(x => x.ActivityTypeId).HasColumnType("int");
            e.Property(x => x.IsPositive).HasColumnType("bit").IsRequired();
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
            e.Property(x => x.Products).HasColumnType("nvarchar(max)");
            e.Property(x => x.Tags).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasColumnType("nvarchar(max)");
            e.Property(x => x.LastCallDate).HasColumnType("datetime2").HasConversion(utcConverter);
            e.Property(x => x.LastCallOutcome).HasColumnType("nvarchar(100)");

            e.HasIndex(x => new { x.IsActive, x.RespCenter, x.LastCallOutcome, x.LastCallDate });
        });

        modelBuilder.Entity<CrmContactFleetDetail>(e =>
        {
            e.ToTable("CrmContactFleetDetail");
            e.HasKey(x => x.Id);
            e.Property(x => x.VehicleType).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.Make).HasColumnType("nvarchar(max)");
            e.Property(x => x.Model).HasColumnType("nvarchar(max)");
            e.Property(x => x.TyreSize).HasColumnType("nvarchar(max)");
            e.Property(x => x.Application).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasConversion(utcConverter);
        });

        modelBuilder.Entity<CrmCallLog>(e =>
        {
            e.ToTable("CrmCallLog");
            e.HasKey(x => x.Id);
            e.Property(x => x.Outcome).HasColumnType("nvarchar(100)").IsRequired();
            e.Property(x => x.Notes).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasColumnType("nvarchar(128)").IsRequired();
            e.Property(x => x.CallDate).HasConversion(utcConverter);
        });

        modelBuilder.Entity<CrmCallReminder>(e =>
        {
            e.ToTable("CrmCallReminder");
            e.HasKey(x => x.Id);
            e.Property(x => x.Notes).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasColumnType("nvarchar(128)").IsRequired();
            e.Property(x => x.ReminderDate).HasConversion(utcConverter);
            e.Property(x => x.CreatedAt).HasConversion(utcConverter);
        });

        modelBuilder.Entity<CrmAgentContact>(e =>
        {
            e.ToTable("CrmAgentContact");
            e.HasKey(x => x.Id);
            e.Property(x => x.AgentUsername).HasColumnType("nvarchar(128)").IsRequired();
            e.Property(x => x.AllocatedAt).HasConversion(utcConverter);
            e.Property(x => x.DeallocatedAt).HasConversion(utcConverter);
            e.Property(x => x.DeallocatedBy).HasColumnType("nvarchar(128)");
            e.Property(x => x.LastCallOutcome).HasColumnType("nvarchar(100)");
            e.Property(x => x.LastCallDate).HasConversion(utcConverter);
            e.Property(x => x.LastCallNotes).HasColumnType("nvarchar(max)");
            e.Property(x => x.CallCount).HasColumnType("int").HasDefaultValue(0);
            e.HasOne(x => x.Contact)
                .WithMany()
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.ContactId, x.DeallocatedAt });
            e.HasIndex(x => new { x.AgentUsername, x.DeallocatedAt, x.AllocatedAt });
        });

        modelBuilder.Entity<CrmSetting>(e =>
        {
            e.ToTable("CrmSetting");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnType("nvarchar(100)").IsRequired();
            e.Property(x => x.Value).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.Description).HasColumnType("nvarchar(max)");
            e.HasData(
                new CrmSetting { Key = "ContactsRecentSalesDaysCooldown", Value = "30", Description = "Days from latest invoice to cool down contact from allocation" }
            );
        });

        modelBuilder.Entity<CrmWhatsappImage>(e =>
        {
            e.ToTable("CrmWhatsappImage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.ImageUrl).HasColumnType("nvarchar(max)");
            e.Property(x => x.Base64Data).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasConversion(utcConverter);
        });

        modelBuilder.Entity<CrmWhatsappTemplate>(e =>
        {
            e.ToTable("CrmWhatsappTemplate");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.Language).HasColumnType("nvarchar(100)").IsRequired();
            e.Property(x => x.MessageText).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.CreatedAt).HasConversion(utcConverter);
        });

        modelBuilder.Entity<CrmFleetVehicleType>(e =>
        {
            e.ToTable("CrmFleetVehicleType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.HasData(
                new CrmFleetVehicleType { Id = 1, Name = "Truck" },
                new CrmFleetVehicleType { Id = 2, Name = "Bus" },
                new CrmFleetVehicleType { Id = 3, Name = "LCV" },
                new CrmFleetVehicleType { Id = 4, Name = "Tractor" }
            );
        });

        modelBuilder.Entity<CrmFleetVehicleMake>(e =>
        {
            e.ToTable("CrmFleetVehicleMake");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.ParentId).HasColumnType("int");
            e.HasData(
                new CrmFleetVehicleMake { Id = 1, Name = "Tata Motors", ParentId = 1 },
                new CrmFleetVehicleMake { Id = 2, Name = "Ashok Leyland", ParentId = 1 },
                new CrmFleetVehicleMake { Id = 3, Name = "Eicher", ParentId = 1 },
                new CrmFleetVehicleMake { Id = 4, Name = "BharatBenz", ParentId = 1 },
                new CrmFleetVehicleMake { Id = 5, Name = "Mahindra", ParentId = 1 },
                new CrmFleetVehicleMake { Id = 6, Name = "Tata Motors (Bus)", ParentId = 2 },
                new CrmFleetVehicleMake { Id = 7, Name = "Ashok Leyland (Bus)", ParentId = 2 }
            );
        });

        modelBuilder.Entity<CrmFleetVehicleModel>(e =>
        {
            e.ToTable("CrmFleetVehicleModel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.ParentId).HasColumnType("int");
            e.HasData(
                new CrmFleetVehicleModel { Id = 1, Name = "Signa", ParentId = 1 },
                new CrmFleetVehicleModel { Id = 2, Name = "Prima", ParentId = 1 },
                new CrmFleetVehicleModel { Id = 3, Name = "LPT", ParentId = 1 },
                new CrmFleetVehicleModel { Id = 4, Name = "Dost", ParentId = 2 },
                new CrmFleetVehicleModel { Id = 5, Name = "Partner", ParentId = 2 },
                new CrmFleetVehicleModel { Id = 6, Name = "Boss", ParentId = 2 },
                new CrmFleetVehicleModel { Id = 7, Name = "Ecomet", ParentId = 2 },
                new CrmFleetVehicleModel { Id = 8, Name = "Pro 2000", ParentId = 3 },
                new CrmFleetVehicleModel { Id = 9, Name = "Pro 3000", ParentId = 3 }
            );
        });

        modelBuilder.Entity<CrmFleetApplication>(e =>
        {
            e.ToTable("CrmFleetApplication");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnType("nvarchar(max)").IsRequired();
            e.HasData(
                new CrmFleetApplication { Id = 1, Name = "Long Haul" },
                new CrmFleetApplication { Id = 2, Name = "Mining" },
                new CrmFleetApplication { Id = 3, Name = "Construction" },
                new CrmFleetApplication { Id = 4, Name = "Passenger Transport" },
                new CrmFleetApplication { Id = 5, Name = "City Distribution" }
            );
        });

        modelBuilder.Entity<CrmProduct>(e =>
        {
            e.ToTable("CrmProduct");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasColumnType("nvarchar(200)").IsRequired();
            e.Property(x => x.Category).HasColumnType("nvarchar(200)").IsRequired(false);
            e.Property(x => x.ProductGroup).HasColumnType("nvarchar(200)").IsRequired(false);
            e.Property(x => x.FinalPrice).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(x => x.RespCenters).HasColumnType("nvarchar(max)").IsRequired(false);
            e.Property(x => x.CreatedAt).HasConversion(utcConverter);
        });
    }
}
