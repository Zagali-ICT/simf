using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SpeakerPresentation EF config. Real FKs to Speaker
/// (cascade — presentations belong to the speaker), Session (restrict — avoids a
/// second cascade path; sessions soft-delete anyway) and StoredFile (restrict,
/// for the same reason). Lookup index on (SpeakerId, IsActive) for the
/// per-speaker management list.</summary>
internal sealed class SpeakerPresentationConfiguration
    : IEntityTypeConfiguration<SpeakerPresentation>
{
    public void Configure(EntityTypeBuilder<SpeakerPresentation> builder)
    {
        builder.ToTable("SpeakerPresentations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();

        builder.HasOne(x => x.Speaker)
            .WithMany()
            .HasForeignKey(x => x.SpeakerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // The bytes, in the one file store. Required: a presentation row with no
        // file has nothing to present. Restrict for the same reason Session is —
        // Speaker already owns the only cascade path into this table.
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SpeakerId, x.IsActive });
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.StoredFileId);
    }
}
