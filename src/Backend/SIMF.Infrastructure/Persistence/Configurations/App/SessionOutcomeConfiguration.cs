using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SessionOutcome EF config — the "أبرز المخرجات" key-outcome bullets on
/// the public session-detail page. Real FK to Session (cascade — outcomes belong
/// to the session). Lookup index on (SessionId, DisplayOrder) for the per-session
/// ordered read. Every HasMaxLength is the single source of truth the CP form +
/// validator align to. Additive under the schema freeze-lift.</summary>
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

        // IsActive is deliberately NOT a leg here. Outcomes are re-synced
        // wholesale: AdminSessionService.ReplaceOutcomes hard-deletes the old
        // rows (RemoveRange) and re-adds the new set renumbered, so no path ever
        // writes IsActive = false and the column is constant-true for every row.
        // A constant leading-adjacent leg costs a byte per row and narrows
        // nothing. The reads still filter it — a seek on SessionId with the rows
        // already in DisplayOrder leaves IsActive as a free residual predicate,
        // and the bullet text forces a key lookup either way.
        builder.HasIndex(o => new { o.SessionId, o.DisplayOrder });
    }
}
