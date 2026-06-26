using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Requests;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-500 (Wave 5, الطلبات) — ParticipationDocumentRequest EF config.
/// No counterparty FK (the document is issued off-band); RequestedByUserId is a
/// logical FK to SimfUser on the Identity DB (no cross-DB relation, D-157).
/// Mirrors <see cref="SpeakerMeetingRequestConfiguration"/>.</summary>
internal sealed class ParticipationDocumentRequestConfiguration
    : IEntityTypeConfiguration<ParticipationDocumentRequest>
{
    public void Configure(EntityTypeBuilder<ParticipationDocumentRequest> builder)
    {
        builder.ToTable("ParticipationDocumentRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
