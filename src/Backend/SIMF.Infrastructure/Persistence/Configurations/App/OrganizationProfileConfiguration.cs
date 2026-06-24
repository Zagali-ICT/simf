using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.Organization;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-495 — the singleton Organization Profile + its two child lists.
/// One profile row (fixed <see cref="OrganizationProfile.SingletonId"/>) seeded via
/// EF model data so it exists from day-one; the about/detail lists and the social /
/// welcome values copied from the old SiteSettings keys are seeded in the migration
/// (they depend on existing data). Real DB FKs child → profile (same DbContext).</summary>
internal sealed class OrganizationProfileConfiguration
    : IEntityTypeConfiguration<OrganizationProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationProfile> builder)
    {
        builder.ToTable("OrganizationProfile");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(256).IsRequired();
        builder.Property(p => p.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(256).IsRequired();
        builder.Property(p => p.TitleArabic).HasMaxLength(256).IsRequired();
        builder.Property(p => p.Slogan).HasMaxLength(512);
        builder.Property(p => p.SloganArabic).HasMaxLength(512);
        builder.Property(p => p.Bio).HasMaxLength(4000);
        builder.Property(p => p.BioArabic).HasMaxLength(4000);

        builder.Property(p => p.Version).HasMaxLength(64);
        builder.Property(p => p.SysVersion).HasMaxLength(64);

        builder.Property(p => p.LocationText).HasMaxLength(512);
        builder.Property(p => p.LocationTextArabic).HasMaxLength(512);
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(10, 6);

        builder.Property(p => p.ContactPhone).HasMaxLength(64);
        builder.Property(p => p.ContactEmail).HasMaxLength(256);
        builder.Property(p => p.ContactWebsite).HasMaxLength(1024);
        builder.Property(p => p.LiveStreamUrl).HasMaxLength(1024);

        builder.Property(p => p.FacebookUrl).HasMaxLength(1024);
        builder.Property(p => p.XUrl).HasMaxLength(1024);
        builder.Property(p => p.InstagramUrl).HasMaxLength(1024);
        builder.Property(p => p.LinkedInUrl).HasMaxLength(1024);
        builder.Property(p => p.YouTubeUrl).HasMaxLength(1024);
        builder.Property(p => p.TikTokUrl).HasMaxLength(1024);
        builder.Property(p => p.SnapchatUrl).HasMaxLength(1024);

        builder.Property(p => p.RegistrationSuccessMessage).HasMaxLength(1024);
        builder.Property(p => p.RegistrationSuccessMessageArabic).HasMaxLength(1024);

        // Stored as int — append-only enum (ForumStatus).
        builder.Property(p => p.Status).HasConversion<int>();

        builder.HasMany(p => p.AboutItems)
            .WithOne(a => a.Profile)
            .HasForeignKey(a => a.OrganizationProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Details)
            .WithOne(d => d.Profile)
            .HasForeignKey(d => d.OrganizationProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed the singleton with the current (2026) edition skeleton. The list
        // content + the social / welcome values migrated from the SiteSettings keys
        // are inserted by the D495 migration (they depend on existing rows).
        builder.HasData(new OrganizationProfile
        {
            Id = OrganizationProfile.SingletonId,
            Name = "The International Maritime Forum",
            NameArabic = "الملتقى الدولي البحري",
            Title = "The Saudi International Maritime Forum",
            TitleArabic = "الملتقى البحري السعودي الدولي",
            Version = "1.0.0",
            CurrentYear = 2026,
            Status = ForumStatus.Open,
            EventStartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EventEndDate = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
            LocationText = "Saudi Arabia",
            LocationTextArabic = "السعودية",
            RegistrationSuccessMessage = SiteSettingKeys.DefaultRegistrationMessageEn,
            RegistrationSuccessMessageArabic = SiteSettingKeys.DefaultRegistrationMessageAr,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });
    }
}

internal sealed class OrganizationAboutItemConfiguration
    : IEntityTypeConfiguration<OrganizationAboutItem>
{
    public void Configure(EntityTypeBuilder<OrganizationAboutItem> builder)
    {
        builder.ToTable("OrganizationAboutItems");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).HasMaxLength(256).IsRequired();
        builder.Property(a => a.TitleArabic).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Text).HasMaxLength(4000).IsRequired();
        builder.Property(a => a.TextArabic).HasMaxLength(4000).IsRequired();

        builder.HasIndex(a => new { a.OrganizationProfileId, a.IsActive, a.DisplayOrder });
    }
}

internal sealed class OrganizationDetailConfiguration
    : IEntityTypeConfiguration<OrganizationDetail>
{
    public void Configure(EntityTypeBuilder<OrganizationDetail> builder)
    {
        builder.ToTable("OrganizationDetails");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(256).IsRequired();
        builder.Property(d => d.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Value).HasMaxLength(1024).IsRequired();

        builder.HasIndex(d => new { d.OrganizationProfileId, d.IsActive, d.DisplayOrder });
    }
}
