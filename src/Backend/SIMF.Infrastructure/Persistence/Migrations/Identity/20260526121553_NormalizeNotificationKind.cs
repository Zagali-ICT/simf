using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class NormalizeNotificationKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-108: Notifications.Kind was a free-form string column
            // (e.g. "Account.Approved"); it now persists the NotificationKind
            // enum name via HasConversion<string>(). Rewrite existing data so
            // the column matches the converter's contract (e.g. "AccountApproved").
            // Column type / length / index are unchanged — DDL is a no-op.
            migrationBuilder.Sql(@"
                UPDATE [Notifications]
                SET [Kind] = CASE [Kind]
                    WHEN 'Credential.PasswordResetRequested' THEN 'CredentialPasswordResetRequested'
                    WHEN 'Credential.EmailVerificationSent'  THEN 'CredentialEmailVerificationSent'
                    WHEN 'Credential.EmailVerificationResent' THEN 'CredentialEmailVerificationResent'
                    WHEN 'Credential.SignInOtpSent'          THEN 'CredentialSignInOtpSent'
                    WHEN 'Account.ProfileSubmitted'          THEN 'AccountProfileSubmitted'
                    WHEN 'Admin.PendingVisitor'              THEN 'AdminPendingVisitor'
                    WHEN 'Account.TwoFactorReset'            THEN 'AccountTwoFactorReset'
                    WHEN 'Account.Approved'                  THEN 'AccountApproved'
                    WHEN 'Account.Rejected'                  THEN 'AccountRejected'
                    ELSE [Kind]
                END
                WHERE [Kind] IN (
                    'Credential.PasswordResetRequested',
                    'Credential.EmailVerificationSent',
                    'Credential.EmailVerificationResent',
                    'Credential.SignInOtpSent',
                    'Account.ProfileSubmitted',
                    'Admin.PendingVisitor',
                    'Account.TwoFactorReset',
                    'Account.Approved',
                    'Account.Rejected');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symmetric reverse — restore the dot-form so a downgrade
            // brings the column back to its pre-D-108 contract.
            migrationBuilder.Sql(@"
                UPDATE [Notifications]
                SET [Kind] = CASE [Kind]
                    WHEN 'CredentialPasswordResetRequested'  THEN 'Credential.PasswordResetRequested'
                    WHEN 'CredentialEmailVerificationSent'   THEN 'Credential.EmailVerificationSent'
                    WHEN 'CredentialEmailVerificationResent' THEN 'Credential.EmailVerificationResent'
                    WHEN 'CredentialSignInOtpSent'           THEN 'Credential.SignInOtpSent'
                    WHEN 'AccountProfileSubmitted'           THEN 'Account.ProfileSubmitted'
                    WHEN 'AdminPendingVisitor'               THEN 'Admin.PendingVisitor'
                    WHEN 'AccountTwoFactorReset'             THEN 'Account.TwoFactorReset'
                    WHEN 'AccountApproved'                   THEN 'Account.Approved'
                    WHEN 'AccountRejected'                   THEN 'Account.Rejected'
                    ELSE [Kind]
                END
                WHERE [Kind] IN (
                    'CredentialPasswordResetRequested',
                    'CredentialEmailVerificationSent',
                    'CredentialEmailVerificationResent',
                    'CredentialSignInOtpSent',
                    'AccountProfileSubmitted',
                    'AdminPendingVisitor',
                    'AccountTwoFactorReset',
                    'AccountApproved',
                    'AccountRejected');");
        }
    }
}
