using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Calendar.Entities;
using Tyresoles.Data.Features.RemoteAssist.Entities;

namespace Tyresoles.Data.Features.Calendar;

public class CalendarDbContext : DbContext
{
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options) : base(options) { }

    public DbSet<RemoteAssistSession> RemoteAssistSessions => Set<RemoteAssistSession>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<RecurrenceRule> RecurrenceRules => Set<RecurrenceRule>();
    public DbSet<EventTag> EventTags => Set<EventTag>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<CalendarTask> CalendarTasks => Set<CalendarTask>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<CalendarAuditLog> CalendarAuditLogs => Set<CalendarAuditLog>();
    public DbSet<CalendarShare> CalendarShares => Set<CalendarShare>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.Property(x => x.Link).HasMaxLength(1000);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.IsRead);
        });

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerUserId).HasMaxLength(128);
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.TimeZoneId).HasMaxLength(128);
            e.Property(x => x.Location).HasMaxLength(500);
            e.Property(x => x.MeetingLink).HasMaxLength(1000);
            e.Property(x => x.CreatedByUserId).HasMaxLength(128);
            e.Property(x => x.UpdatedByUserId).HasMaxLength(128);
            e.HasIndex(x => new { x.OwnerUserId, x.StartUtc, x.EndUtc });
            e.HasIndex(x => x.IsDeleted);
            e.HasIndex(x => x.RecurrenceRuleId);
            e.HasIndex(x => x.ParentEventId);
            e.HasOne(x => x.RecurrenceRule).WithMany(r => r.Events).HasForeignKey(x => x.RecurrenceRuleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.EventType).WithMany(t => t.Events).HasForeignKey(x => x.EventTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ParentEvent).WithMany(p => p.ExceptionEvents).HasForeignKey(x => x.ParentEventId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecurrenceRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DaysOfWeek).HasMaxLength(100);
            e.Property(x => x.RRule).HasMaxLength(500);
        });

        modelBuilder.Entity<EventTag>(e =>
        {
            e.HasKey(x => new { x.EventId, x.TagType, x.TagKey });
            e.Property(x => x.TagKey).HasMaxLength(128);
            e.HasOne(x => x.Event).WithMany(ev => ev.Tags).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Icon).HasMaxLength(50);
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventId, x.RemindAtUtc, x.IsSent });
            e.HasOne(x => x.Event).WithMany(ev => ev.Reminders).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.Payload).HasMaxLength(4000);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<CalendarShare>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerUserId).HasMaxLength(128);
            e.Property(x => x.SharedWithUserId).HasMaxLength(128);
            e.HasIndex(x => new { x.OwnerUserId, x.SharedWithUserId });
        });

        modelBuilder.Entity<EventAttendee>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Event).WithMany(ev => ev.Attendees).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<CalendarTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.ParentTaskId);
            e.HasOne(x => x.Event).WithMany(ev => ev.Tasks).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentTask).WithMany(p => p.SubTasks).HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationPreference>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Channel });
            e.Property(x => x.UserId).HasMaxLength(128);
        });

        modelBuilder.Entity<RemoteAssistSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JoinCode).HasMaxLength(16);
            e.HasIndex(x => x.JoinCode).IsUnique();
            e.Property(x => x.HostUserId).HasMaxLength(128);
            e.Property(x => x.HostDisplayName).HasMaxLength(200);
            e.Property(x => x.ViewerUserId).HasMaxLength(128);
            e.Property(x => x.ViewerDisplayName).HasMaxLength(200);
            e.Property(x => x.EndedByUserId).HasMaxLength(128);
            e.HasIndex(x => x.HostUserId);
            e.HasIndex(x => x.ExpiresAtUtc);
        });
    }
}
