using Microsoft.AspNetCore.Identity;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// R5a — D-090: the Infrastructure-owned persistence shape for the SIMF user row.
/// Mirrors <see cref="SimfUser"/> field-for-field and is now the type the
/// EF Identity store (<see cref="UserManager{T}"/>, <see cref="IUserStore{T}"/>)
/// is generically parameterised on. Carves the seam that lets the Domain
/// <see cref="SimfUser"/> drop its <see cref="IdentityUser{TKey}"/> inheritance
/// in R5f without touching the database schema.
///
/// <para>R5a does not change Domain types: <see cref="SimfUser"/> still
/// inherits <see cref="IdentityUser{TKey}"/>, the public
/// <c>IUserAccountRepository</c> contract is unchanged, and the database
/// columns / table name stay identical (<c>AspNetUsers</c>). The only
/// runtime difference is the CLR type EF tracks for that table.</para>
///
/// <para><c>public sealed</c> on purpose, not internal: the public
/// <see cref="SimfIdentityDbContext"/> exposes
/// <c>IdentityDbContext&lt;IdentitySimfUser, SimfRole, Guid&gt;</c> as its base
/// type, and C# accessibility rules require the generic parameter to be at
/// least as accessible as the deriving class. Application and Endpoint code
/// has no reason to construct an <see cref="IdentitySimfUser"/> — it should
/// continue to talk to <see cref="SimfUser"/> through
/// <c>IUserAccountRepository</c>. The exception is the test fixtures, which
/// seed rows directly via <see cref="UserManager{T}"/>.</para>
/// </summary>
public sealed class IdentitySimfUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public AccountState AccountState { get; set; } = AccountState.Registered;

    public UserType UserType { get; set; } = UserType.Visitor;

    public bool PasswordChangeRequired { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public long? LastUsedTotpTimestep { get; set; }

    public string? AvatarRelativePath { get; set; }

    public string? QrId { get; set; }

    public string? RejectionReason { get; set; }

    public string? RejectionReasonArabic { get; set; }

    public DateTimeOffset? StateChangedAt { get; set; }

    public Guid? StateChangedByUserId { get; set; }
}
