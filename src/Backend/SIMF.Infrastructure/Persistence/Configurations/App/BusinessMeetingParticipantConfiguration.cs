using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-013 — D-248: BusinessMeetingParticipant EF config. The
/// parent meeting FK is configured on <see cref="BusinessMeetingConfiguration"/>
/// (cascade). <c>CompanyId</c> is an optional real FK to Company (Restrict);
/// <c>VisitorUserId</c> is a bare logical Guid to the Identity DB — no navigation,
/// no FK (D-157 / D-246). Lookup indexes back the participant-overlap check.</summary>
internal sealed class BusinessMeetingParticipantConfiguration
    : IEntityTypeConfiguration<BusinessMeetingParticipant>
{
    public void Configure(EntityTypeBuilder<BusinessMeetingParticipant> builder)
    {
        builder.ToTable("BusinessMeetingParticipants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayNameSnapshot).HasMaxLength(256).IsRequired();

        builder.HasOne(p => p.Company)
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.BusinessMeetingId);
        builder.HasIndex(p => p.CompanyId);
        builder.HasIndex(p => p.VisitorUserId);
    }
}
