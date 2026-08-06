using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF configuration for <see cref="Gate"/>. Unique index on
/// Code (case-insensitive uniqueness is enforced by the service layer via
/// upper-case normalisation, matching the Hall + Theme pattern).</summary>
internal sealed class GateConfiguration : IEntityTypeConfiguration<Gate>
{
    public void Configure(EntityTypeBuilder<Gate> builder)
    {
        builder.ToTable("Gates");
        builder.HasKey(gate => gate.Id);

        builder.Property(gate => gate.Code).HasMaxLength(16).IsRequired();
        builder.Property(gate => gate.Name).HasMaxLength(128).IsRequired();
        builder.Property(gate => gate.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(gate => gate.Description).HasMaxLength(1024);
        builder.Property(gate => gate.DescriptionArabic).HasMaxLength(1024);
        builder.Property(gate => gate.DirectionMode).HasConversion<int>();

        builder.HasIndex(gate => gate.Code).IsUnique();
        builder.HasIndex(gate => new { gate.IsActive, gate.Name });

        // Optional hall-door binding. Real FK to Hall (same
        // DbContext, App DB — no Identity crossing). Restrict mirrors
        // HallAttendance's Hall FK so a hall cannot be hard-deleted out from under
        // a hall-door gate (halls are soft-deleted). Navigation-less HasOne<Hall>
        // form, as elsewhere. Null = perimeter gate. Indexed for the
        // "list the door gates of hall X" lookup.
        builder.HasIndex(gate => gate.HallId);
        builder.HasOne<SIMF.Domain.Programme.Hall>()
            .WithMany()
            .HasForeignKey(gate => gate.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(gate => gate.AllowedProfileTypes)
            .WithOne(allow => allow.Gate!)
            .HasForeignKey(allow => allow.GateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (was Cascade): deleting a Gate must not
        // silently delete its operator assignments; deactivate the Gate instead.
        builder.HasMany(gate => gate.Assignments)
            .WithOne(assignment => assignment.Gate!)
            .HasForeignKey(assignment => assignment.GateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
