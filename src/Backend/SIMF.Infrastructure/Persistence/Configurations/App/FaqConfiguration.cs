using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Faq;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>P2.1 (D-211) — FAQ group + entry configuration. Real DB FK
/// FaqEntry → FaqGroup (same DbContext); deleting a group cascades to its
/// entries (admins soft-delete, so cascade only guards a hard delete).</summary>
internal sealed class FaqGroupConfiguration : IEntityTypeConfiguration<FaqGroup>
{
    public void Configure(EntityTypeBuilder<FaqGroup> builder)
    {
        builder.ToTable("FaqGroups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(g => g.NameAr).HasMaxLength(128).IsRequired();

        builder.HasIndex(g => new { g.IsActive, g.DisplayOrder });

        builder.HasMany(g => g.Entries)
            .WithOne(e => e.Group)
            .HasForeignKey(e => e.FaqGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FaqEntryConfiguration : IEntityTypeConfiguration<FaqEntry>
{
    public void Configure(EntityTypeBuilder<FaqEntry> builder)
    {
        builder.ToTable("FaqEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.QuestionEn).HasMaxLength(512).IsRequired();
        builder.Property(e => e.QuestionAr).HasMaxLength(512).IsRequired();
        builder.Property(e => e.AnswerEn).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.AnswerAr).HasMaxLength(4000).IsRequired();

        builder.HasIndex(e => new { e.FaqGroupId, e.IsActive, e.DisplayOrder });
    }
}
