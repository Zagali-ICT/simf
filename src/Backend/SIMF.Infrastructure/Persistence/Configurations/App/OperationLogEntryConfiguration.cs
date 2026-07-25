using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Auditing;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

internal sealed class OperationLogEntryConfiguration : IEntityTypeConfiguration<OperationLogEntry>
{
    public void Configure(EntityTypeBuilder<OperationLogEntry> builder)
    {
        builder.ToTable("OperationLog");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.EventType).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Outcome).HasConversion<string>().HasMaxLength(16);
        builder.Property(entry => entry.SubjectEmail).HasMaxLength(256);
        // D-157 — display-name snapshots so SOC reviews don't need a
        // cross-DB JOIN to Identity (which lives in a different physical DB).
        builder.Property(entry => entry.SubjectDisplayName).HasMaxLength(128);
        builder.Property(entry => entry.ActorDisplayName).HasMaxLength(128);
        builder.Property(entry => entry.SourceIp).HasMaxLength(64);
        builder.Property(entry => entry.UserAgent).HasMaxLength(512);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(64);
        builder.Property(entry => entry.ErrorCode).HasMaxLength(64);
        builder.Property(entry => entry.Detail).HasMaxLength(1024);

        builder.HasIndex(entry => entry.Timestamp);
        builder.HasIndex(entry => new { entry.EventType, entry.Timestamp });
        builder.HasIndex(entry => entry.SubjectEmail);

        // D-611 (Wave B) — actor-centric audit lookups (who did what, when).
        builder.HasIndex(entry => new { entry.ActorUserId, entry.Timestamp });
    }
}
