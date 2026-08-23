using ChronosMesh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChronosMesh.Infrastructure.Persistence;

public class ChronosMeshDbContext : DbContext
{
    public ChronosMeshDbContext(DbContextOptions<ChronosMeshDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<CalendarEntity> Calendars => Set<CalendarEntity>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ScheduleDefinition> Schedules => Set<ScheduleDefinition>();
    public DbSet<WorkingDayEntity> WorkingDays => Set<WorkingDayEntity>();
    public DbSet<AvailabilityOverride> AvailabilityOverrides => Set<AvailabilityOverride>();
    public DbSet<HolidayEntity> Holidays => Set<HolidayEntity>();
    public DbSet<BookingService> BookingServices => Set<BookingService>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(320).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
        });

        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasOne(w => w.Organization).WithMany(o => o.Workspaces).HasForeignKey(w => w.OrganizationId);
        });

        modelBuilder.Entity<WorkspaceMember>(e =>
        {
            e.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
            e.HasOne(m => m.Workspace).WithMany(w => w.Members).HasForeignKey(m => m.WorkspaceId);
            e.HasOne(m => m.User).WithMany(u => u.WorkspaceMemberships).HasForeignKey(m => m.UserId);
        });

        modelBuilder.Entity<CalendarEntity>(e =>
        {
            e.HasOne<Workspace>().WithMany(w => w.Calendars).HasForeignKey(c => c.WorkspaceId);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.HasOne(ev => ev.Calendar).WithMany(c => c.Events).HasForeignKey(ev => ev.CalendarId);
            e.HasIndex(ev => new { ev.CalendarId, ev.StartUtc, ev.EndUtc });
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasIndex(t => t.WorkspaceId);
            e.HasIndex(t => t.DeadlineUtc);
        });

        modelBuilder.Entity<BookingService>(e =>
        {
            e.HasIndex(b => b.BookingSlug).IsUnique();
        });

        modelBuilder.Entity<SettingEntity>(e =>
        {
            e.HasIndex(s => new { s.WorkspaceId, s.Key }).IsUnique();
        });
    }
}
