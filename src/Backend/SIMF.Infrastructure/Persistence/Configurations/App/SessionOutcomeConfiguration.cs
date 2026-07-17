using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SessionOutcome EF config — the "أبرز المخرجات" key-outcome bullets on
/// the public session-detail page. Real FK to Session (cascade — outcomes belong
/// to the session). Lookup index on (SessionId, IsActive, DisplayOrder) for the
/// per-session ordered read. Every HasMaxLength is the single source of truth the
/// CP form + validator align to (§7). Additive under the D-219 freeze-lift.</summary>
internal sealed class SessionOutcomeConfiguration
    : IEntityTypeConfiguration<SessionOutcome>
{
    public void Configure(EntityTypeBuilder<SessionOutcome> builder)
    {
        builder.ToTable("SessionOutcomes");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Text).HasMaxLength(512).IsRequired();
        builder.Property(o => o.TextArabic).HasMaxLength(512).IsRequired();

        builder.HasOne(o => o.Session)
            .WithMany(s => s.Outcomes)
            .HasForeignKey(o => o.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.SessionId, o.IsActive, o.DisplayOrder });
    }
}
