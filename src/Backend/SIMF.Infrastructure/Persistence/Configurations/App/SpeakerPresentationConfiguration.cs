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
        // CK_SpeakerPresentations_SizeBytes ([SizeBytes] > 0) went with the column
        // it constrained. The size now lives once, on the store row, where it
        // cannot carry the same constraint: StoredFile.SizeBytes is nullable
        // because an ExternalLink row has no byte count, so "> 0" would reject a
        // legitimate file rather than a zero-byte one.
        //
        // What remains is AdminSpeakerPresentationService's 400
        // SPEAKER_PRESENTATION_INVALID on an empty upload, which is where every
        // real presentation is created. The gap the constraint covered - a seed or
        // repair script writing a zero-byte row straight to the table - is now
        // uncovered, and is recorded as such rather than assumed away.
        builder.ToTable("SpeakerPresentations");
        builder.HasKey(x => x.Id);

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
        // Speaker already owns the only cascade path into this table. The
        // navigation is what the name, media type, size and uploader are read
        // through, now that this table no longer keeps its own copies of them.
        builder.HasOne(x => x.StoredFile)
            .WithMany()
            .HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SpeakerId, x.IsActive });
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.StoredFileId);
    }
}
