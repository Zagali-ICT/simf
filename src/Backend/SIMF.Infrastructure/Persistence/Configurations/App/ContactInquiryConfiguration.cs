using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Support;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Contact-us inquiry configuration. Lengths mirror the
/// <c>SubmitContactInquiryValidator</c> + the app form caps (Name 120 / Email
/// 256 / Message 4000). Indexed by (IsHandled, CreatedAt) for the inbox's
/// "open first, newest first" ordering.</summary>
internal sealed class ContactInquiryConfiguration
    : IEntityTypeConfiguration<ContactInquiry>
{
    public void Configure(EntityTypeBuilder<ContactInquiry> builder)
    {
        builder.ToTable("ContactInquiries");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Message).HasMaxLength(4000).IsRequired();

        builder.HasIndex(c => new { c.IsHandled, c.CreatedAt });
    }
}
