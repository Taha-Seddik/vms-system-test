using Microsoft.EntityFrameworkCore;
using Vms.Api.Data.Entities;

namespace Vms.Api.Data;

public sealed class VmsDbContext(DbContextOptions<VmsDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserCameraAssignment> UserCameraAssignments => Set<UserCameraAssignment>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.HasKey(item => item.Id);
        user.Property(item => item.Username).HasMaxLength(100).IsRequired();
        user.Property(item => item.NormalizedUsername).HasMaxLength(100).IsRequired();
        user.HasIndex(item => item.NormalizedUsername).IsUnique();
        user.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
        user.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(item => item.Role).HasConversion<string>().HasMaxLength(32);

        var assignment = modelBuilder.Entity<UserCameraAssignment>();
        assignment.HasKey(item => new { item.UserId, item.CameraId });
        assignment.Property(item => item.CameraId).HasMaxLength(100);
        assignment
            .HasOne(item => item.User)
            .WithMany(item => item.CameraAssignments)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var session = modelBuilder.Entity<UserSession>();
        session.HasKey(item => item.Id);
        session.Property(item => item.RevokedReason).HasMaxLength(200);
        session.HasIndex(item => new { item.UserId, item.ExpiresAt });
        session
            .HasOne(item => item.User)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var systemEvent = modelBuilder.Entity<SystemEvent>();
        systemEvent.HasKey(item => item.Id);
        systemEvent.Property(item => item.Type).HasConversion<string>().HasMaxLength(64);
        systemEvent.Property(item => item.CameraId).HasMaxLength(100);
        systemEvent.Property(item => item.Severity).HasConversion<string>().HasMaxLength(32);
        systemEvent.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        systemEvent.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        systemEvent.HasIndex(item => item.Timestamp);
        systemEvent.HasIndex(item => new { item.Type, item.Timestamp });
    }
}

