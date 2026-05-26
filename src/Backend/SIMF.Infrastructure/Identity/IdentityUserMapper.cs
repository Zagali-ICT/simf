using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// R5b — D-091: bidirectional mapper between the Domain
/// <see cref="SimfUser"/> and the Infrastructure-owned EF entity
/// <see cref="IdentitySimfUser"/>. Extracted from
/// <see cref="UserAccountRepository"/>'s R5a proto-mapper so the repository
/// stays focused on UserManager orchestration.
///
/// <para>Three operations:
/// <list type="bullet">
/// <item><see cref="ToIdentity"/> — Domain → Infrastructure, used when the
/// caller is creating a brand-new row and there is nothing to merge
/// into.</item>
/// <item><see cref="ToDomain"/> — Infrastructure → Domain, used by repository
/// reads (<c>FindBy*</c>) so Application sees a Domain shape.</item>
/// <item><see cref="ApplyDomainMutations"/> — copies caller mutations from a
/// SimfUser onto the EF-tracked IdentitySimfUser. The Identity-owned
/// columns (<c>ConcurrencyStamp</c>, <c>SecurityStamp</c>,
/// <c>PasswordHash</c>) are deliberately NOT copied — UserManager owns
/// those.</item>
/// <item><see cref="SyncBack"/> — copies server-side mutations
/// (security stamp, lockout state, password hash, etc.) back onto the
/// caller's SimfUser so a mutating method visibly mutates the
/// passed-in instance, preserving the pre-R5 contract.</item>
/// </list></para>
/// </summary>
internal static class IdentityUserMapper
{
    public static IdentitySimfUser ToIdentity(SimfUser source) => new()
    {
        Id = source.Id,
        UserName = source.UserName,
        NormalizedUserName = source.NormalizedUserName,
        Email = source.Email,
        NormalizedEmail = source.NormalizedEmail,
        EmailConfirmed = source.EmailConfirmed,
        PasswordHash = source.PasswordHash,
        SecurityStamp = source.SecurityStamp,
        ConcurrencyStamp = source.ConcurrencyStamp,
        PhoneNumber = source.PhoneNumber,
        PhoneNumberConfirmed = source.PhoneNumberConfirmed,
        TwoFactorEnabled = source.TwoFactorEnabled,
        LockoutEnd = source.LockoutEnd,
        LockoutEnabled = source.LockoutEnabled,
        AccessFailedCount = source.AccessFailedCount,
        DisplayName = source.DisplayName,
        AccountState = source.AccountState,
        UserType = source.UserType,
        PasswordChangeRequired = source.PasswordChangeRequired,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        LastUsedTotpTimestep = source.LastUsedTotpTimestep,
        AvatarRelativePath = source.AvatarRelativePath,
        QrId = source.QrId,
        RejectionReason = source.RejectionReason,
        RejectionReasonArabic = source.RejectionReasonArabic,
        StateChangedAt = source.StateChangedAt,
        StateChangedByUserId = source.StateChangedByUserId,
    };

    public static SimfUser ToDomain(IdentitySimfUser source) => new()
    {
        Id = source.Id,
        UserName = source.UserName,
        NormalizedUserName = source.NormalizedUserName,
        Email = source.Email,
        NormalizedEmail = source.NormalizedEmail,
        EmailConfirmed = source.EmailConfirmed,
        PasswordHash = source.PasswordHash,
        SecurityStamp = source.SecurityStamp,
        ConcurrencyStamp = source.ConcurrencyStamp,
        PhoneNumber = source.PhoneNumber,
        PhoneNumberConfirmed = source.PhoneNumberConfirmed,
        TwoFactorEnabled = source.TwoFactorEnabled,
        LockoutEnd = source.LockoutEnd,
        LockoutEnabled = source.LockoutEnabled,
        AccessFailedCount = source.AccessFailedCount,
        DisplayName = source.DisplayName,
        AccountState = source.AccountState,
        UserType = source.UserType,
        PasswordChangeRequired = source.PasswordChangeRequired,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        LastUsedTotpTimestep = source.LastUsedTotpTimestep,
        AvatarRelativePath = source.AvatarRelativePath,
        QrId = source.QrId,
        RejectionReason = source.RejectionReason,
        RejectionReasonArabic = source.RejectionReasonArabic,
        StateChangedAt = source.StateChangedAt,
        StateChangedByUserId = source.StateChangedByUserId,
    };

    public static void ApplyDomainMutations(SimfUser source, IdentitySimfUser target)
    {
        target.UserName = source.UserName;
        target.Email = source.Email;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PhoneNumber = source.PhoneNumber;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.LockoutEnd = source.LockoutEnd;
        target.LockoutEnabled = source.LockoutEnabled;
        target.AccessFailedCount = source.AccessFailedCount;
        target.DisplayName = source.DisplayName;
        target.AccountState = source.AccountState;
        target.UserType = source.UserType;
        target.PasswordChangeRequired = source.PasswordChangeRequired;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
        target.LastUsedTotpTimestep = source.LastUsedTotpTimestep;
        target.AvatarRelativePath = source.AvatarRelativePath;
        target.QrId = source.QrId;
        target.RejectionReason = source.RejectionReason;
        target.RejectionReasonArabic = source.RejectionReasonArabic;
        target.StateChangedAt = source.StateChangedAt;
        target.StateChangedByUserId = source.StateChangedByUserId;
    }

    public static void SyncBack(IdentitySimfUser source, SimfUser target)
    {
        target.Id = source.Id;
        target.UserName = source.UserName;
        target.NormalizedUserName = source.NormalizedUserName;
        target.NormalizedEmail = source.NormalizedEmail;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PasswordHash = source.PasswordHash;
        target.SecurityStamp = source.SecurityStamp;
        target.ConcurrencyStamp = source.ConcurrencyStamp;
        target.PhoneNumber = source.PhoneNumber;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.LockoutEnd = source.LockoutEnd;
        target.LockoutEnabled = source.LockoutEnabled;
        target.AccessFailedCount = source.AccessFailedCount;
    }
}
