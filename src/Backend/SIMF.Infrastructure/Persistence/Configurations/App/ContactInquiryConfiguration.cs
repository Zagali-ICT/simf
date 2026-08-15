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
        // Handling is one fact spread over three columns: the flag, the stamp
        // and the actor. ContactInquiryService is the only writer and moves all
        // three in one statement (created unhandled with neither, then set or
        // cleared together), so this puts that rule on the table too — the same
        // state-pin shape as CK_GateAssignments_RevocationPin. It is also what
        // keeps IsHandled honest against HandledAt while the redundant flag
        // still exists.
        builder.ToTable("ContactInquiries", table => table.HasCheckConstraint(
            "CK_ContactInquiries_HandledPin",
            "([IsHandled] = 0 AND [HandledAt] IS NULL AND [HandledByUserId] IS NULL) OR "
            + "([IsHandled] = 1 AND [HandledAt] IS NOT NULL AND [HandledByUserId] IS NOT NULL)"));
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Message).HasMaxLength(4000).IsRequired();

        builder.HasIndex(c => new { c.IsHandled, c.CreatedAt });
    }
}
