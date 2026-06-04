using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 #3 — ExhibitorMembership EF config. <see cref="ExhibitorMembership.UserId"/>
/// is a logical FK to <c>SimfUser.Id</c> on the Identity database (D-157 keeps
/// the two physical databases separate) so there is NO HasOne navigation to
/// SimfUser and NO DB-level FK constraint — the link is by Guid only. Indexed
/// on ExhibitorId (list accounts under an exhibitor) and on UserId (resolve the
/// exhibitor a given account belongs to). Auto-discovered by
/// SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class ExhibitorMembershipConfiguration
    : IEntityTypeConfiguration<ExhibitorMembership>
{
    public void Configure(EntityTypeBuilder<ExhibitorMembership> builder)
    {
        builder.ToTable("ExhibitorMemberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.ContactName).HasMaxLength(256).IsRequired();
        builder.Property(membership => membership.RoleLabel).HasMaxLength(128);

        builder.HasIndex(membership => membership.ExhibitorId);
        builder.HasIndex(membership => membership.UserId);
    }
}
