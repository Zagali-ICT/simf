using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Companies;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 #3 — CompanyMembership EF config. <see cref="CompanyMembership.UserId"/>
/// is a logical FK to <c>SimfUser.Id</c> on the Identity database (D-157 keeps
/// the two physical databases separate) so there is NO HasOne navigation to
/// SimfUser and NO DB-level FK constraint — the link is by Guid only. Indexed
/// on CompanyId (list accounts under a company) and on UserId (resolve the
/// company a given account belongs to). Auto-discovered by
/// SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class CompanyMembershipConfiguration
    : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("CompanyMemberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.ContactName).HasMaxLength(256).IsRequired();
        builder.Property(membership => membership.RoleLabel).HasMaxLength(128);

        builder.HasIndex(membership => membership.CompanyId);
        builder.HasIndex(membership => membership.UserId);
    }
}
