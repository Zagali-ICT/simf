using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Auditing;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>
/// D-109: EF configuration for the row-audit trail on
/// <see cref="SimfIdentityDbContext"/>. A matching configuration lives
/// under <c>Configurations.App</c> for <see cref="SimfAppDbContext"/>;
/// both write to a <c>RowAudits</c> table local to their own database
/// (one transaction, no cross-DB FK).
/// </summary>
internal sealed class RowAuditConfiguration : IEntityTypeConfiguration<RowAudit>
{
    public void Configure(EntityTypeBuilder<RowAudit> builder)
    {
        // D-157: the Identity context lives in its own physically separate
        // database (SIMF_Identity), so its RowAudits no longer co-habits a DB
        // with the App context's table. The `identity` schema qualifier is
        // retained from the earlier one-shared-DB design (C-1, superseded) —
        // harmless under the split, and changing it now would be a frozen-schema
        // change.
        builder.ToTable("RowAudits", schema: "identity");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).ValueGeneratedOnAdd();

        builder.Property(audit => audit.TableName).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(audit => audit.PrimaryKey).HasMaxLength(256).IsRequired();
        builder.Property(audit => audit.CorrelationId).HasMaxLength(64);
        builder.Property(audit => audit.AffectedColumns).HasMaxLength(2000);
        // D-157 — actor-name snapshot lets forensic queries read the
        // actor's display name without a cross-DB JOIN.
        builder.Property(audit => audit.ActorDisplayName).HasMaxLength(128);
        builder.Property(audit => audit.OldValuesJson);
        builder.Property(audit => audit.NewValuesJson);

        // Forensic-query indexes: "every change on table X" and "every change by user Y".
        builder.HasIndex(audit => new { audit.TableName, audit.OccurredAt })
            .IsDescending(false, true);
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAt })
            .IsDescending(false, true);
    }
}
