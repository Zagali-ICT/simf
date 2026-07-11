using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Email;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-735 — one override row per transactional email. Body columns are
/// left without a max length so EF maps them to <c>nvarchar(max)</c> (an admin
/// may write a long HTML body). The type is persisted by name.</summary>
internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(x => x.Subject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.BodyEn).IsRequired();
        builder.Property(x => x.BodyAr).IsRequired();

        builder.HasIndex(x => x.Type).IsUnique();
    }
}
