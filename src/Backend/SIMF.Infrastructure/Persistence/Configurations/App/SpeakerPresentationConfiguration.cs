using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>P2.3 — D-228: SpeakerPresentation EF config. Real FKs to Speaker
/// (cascade — presentations belong to the speaker) and Session (restrict —
/// avoids a second cascade path; sessions soft-delete anyway). Lookup index on
/// (SpeakerId, IsActive) for the per-speaker management list.</summary>
internal sealed class SpeakerPresentationConfiguration
    : IEntityTypeConfiguration<SpeakerPresentation>
{
    public void Configure(EntityTypeBuilder<SpeakerPresentation> builder)
    {
        builder.ToTable("SpeakerPresentations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();

        builder.HasOne(x => x.Speaker)
            .WithMany()
            .HasForeignKey(x => x.SpeakerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SpeakerId, x.IsActive });
        builder.HasIndex(x => x.SessionId);
    }
}
