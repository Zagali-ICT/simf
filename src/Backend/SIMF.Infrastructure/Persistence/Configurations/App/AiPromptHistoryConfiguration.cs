using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Ai;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-188 — append-only snapshot table for
/// <see cref="AiPrompt"/> edit history. One row per
/// (AiPromptId, Version) captured BEFORE the version bump that
/// produced the change. The unique composite index guards against a
/// duplicate snapshot at the same version landing twice (e.g. a
/// retry path).</summary>
internal sealed class AiPromptHistoryConfiguration
    : IEntityTypeConfiguration<AiPromptHistory>
{
    public void Configure(EntityTypeBuilder<AiPromptHistory> builder)
    {
        builder.ToTable("AiPromptHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AiPromptId).IsRequired();
        builder.Property(x => x.Version).IsRequired();

        // Long-text columns map to nvarchar(max), matching the live
        // AiPrompt configuration so a snapshot can hold the same
        // length the live row was.
        builder.Property(x => x.SystemPrompt).IsRequired();
        builder.Property(x => x.UserPromptTemplate).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(64).IsRequired();

        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();

        // Append-only enforcement: the unique (AiPromptId, Version)
        // index rejects a duplicate snapshot at the same version.
        // The history-by-prompt read on the CP uses the same index
        // (PromptId is the lookup; Version is the sort key).
        builder.HasIndex(x => new { x.AiPromptId, x.Version }).IsUnique();
        builder.HasIndex(x => x.CapturedAt);

        // D-611 (Wave B) — AiPromptId becomes a real FK to AiPrompt (both on the
        // App DB). RESTRICT, never Cascade: a snapshot must survive the live
        // row's lifecycle (the live AiPrompt is soft-deleted, so Restrict never
        // actually fires). The unique (AiPromptId, Version) index above covers it.
        builder.HasOne<AiPrompt>()
            .WithMany()
            .HasForeignKey(x => x.AiPromptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
