using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Auditing;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// Row-audit configuration for the <see cref="SimfAppDbContext"/>
/// — same shape as <see cref="Configurations.RowAuditConfiguration"/>,
/// in the App namespace so the App context's
/// <c>ApplyConfigurationsFromAssembly</c> filter picks it up while the
/// Identity context ignores it.
/// </summary>
internal sealed class RowAuditConfigurationApp : IEntityTypeConfiguration<RowAudit>
{
    public void Configure(EntityTypeBuilder<RowAudit> builder)
    {
        // The App context lives in its own physically separate database
        // (SIMF_App), so its RowAudits no longer shares a DB with the Identity
        // context's table. The `app` schema qualifier is retained from the
        // earlier one-shared-DB design (superseded) — it is harmless under the
        // split and it is what keeps the two audit tables visibly distinct when
        // both databases are read side by side.
        builder.ToTable("RowAudits", schema: "app");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).ValueGeneratedOnAdd();

        builder.Property(audit => audit.TableName).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(audit => audit.PrimaryKey).HasMaxLength(256).IsRequired();
        builder.Property(audit => audit.CorrelationId).HasMaxLength(64);
        builder.Property(audit => audit.AffectedColumns).HasMaxLength(2000);
        // Actor-name snapshot, same purpose as the identity-side
        // RowAuditConfiguration.
        builder.Property(audit => audit.ActorDisplayName).HasMaxLength(128);
        builder.Property(audit => audit.OldValuesJson);
        builder.Property(audit => audit.NewValuesJson);

        builder.HasIndex(audit => new { audit.TableName, audit.OccurredAt })
            .IsDescending(false, true);
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAt })
            .IsDescending(false, true);
    }
}
