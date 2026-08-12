/* =====================================================================
   QA-ONLY — a RESTRICTED admin, so deny paths can actually be driven.

   THE PROBLEM THIS SOLVES. Every browser-driven QA pass signs in as the
   super-admin, whose permission claim resolves to the wildcard "*"
   (PermissionCatalog.Wildcard). That account satisfies every gate, so it can
   never be redirected to /not-permitted and can never produce a 403 — which
   means the entire DENY half of the permission system (D-207/D-208) is
   unreachable from a browser suite. E2E-BF-13-008 sat unexecuted for exactly
   this reason.

   Creating a restricted admin through the API does not fix it either:
   SignInService forces the TOTP second factor for any user holding a role
   (roles.Count > 0, SignInService.cs), and a freshly created admin has no
   enrolled authenticator key, so its CP sign-in can never be completed by a
   black-box runner.

   THE TECHNIQUE. Clone the super-admin's credential material — its
   PasswordHash and its "[AspNetUserStore]/AuthenticatorKey" token row — onto a
   new user, then give that user a role granting exactly ONE permission. The
   restricted admin therefore signs in with the SAME password and the SAME TOTP
   secret the harness already holds, while carrying a claim set that is
   deliberately almost empty.

   Grant: Sessions.View, and nothing else. So against this account
     /admin/sessions  -> reachable    (proves the fixture can sign in at all)
     /admin/themes    -> /not-permitted + 403  (proves the gate bites)

   SAFETY. Development / Testing only, against the throwaway SIMF_QA_Identity
   database. It mints no new secret: it copies whatever the QA super-admin
   already has, so it can never be more privileged than that account and there
   is nothing here to leak. It must NEVER be run against production — a second
   account sharing the super-admin's password would be a real vulnerability.
   Guarded below on the database name.

   Idempotent. Safe to re-run.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Refuse to run anywhere but a QA database. The whole script hangs on cloning
   a password hash; pointing it at production would create a second account
   holding the real super-admin's credentials. */
DECLARE @db sysname = DB_NAME();  -- RAISERROR takes variables, not function calls
IF @db NOT LIKE '%[_]QA[_]%'
BEGIN
    RAISERROR('seed-restricted-admin.sql refuses to run on "%s" - it is QA-only (database name must contain _QA_).', 16, 1, @db);
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @now              datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @restrictedEmail  nvarchar(256)    = N'qa-restricted@simf.test';
DECLARE @roleName         nvarchar(256)    = N'QA-Restricted';
DECLARE @grantCode        nvarchar(256)    = N'Sessions.View';

DECLARE @superId uniqueidentifier =
    (SELECT TOP 1 Id FROM dbo.Users WHERE NormalizedEmail = N'SUPERADMIN@SIMF.TEST');

IF @superId IS NULL
BEGIN
    RAISERROR('No QA super-admin found — start the QA stack once so the seeder runs, then re-run this script.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

/* ---- 1. The restricted role ------------------------------------------- */
DECLARE @roleId uniqueidentifier =
    (SELECT TOP 1 Id FROM dbo.Roles WHERE NormalizedName = UPPER(@roleName));

IF @roleId IS NULL
BEGIN
    SET @roleId = NEWID();
    INSERT INTO dbo.Roles (Id, Name, NormalizedName, ConcurrencyStamp, IsBaseline)
    VALUES (@roleId, @roleName, UPPER(@roleName), CONVERT(nvarchar(36), NEWID()), 0);
END;

/* ---- 2. Exactly one grant --------------------------------------------- */
DECLARE @permissionId uniqueidentifier =
    (SELECT TOP 1 Id FROM dbo.Permissions WHERE Code = @grantCode);

IF @permissionId IS NULL
BEGIN
    RAISERROR('Permission "%s" is not seeded — start the QA API once so PermissionCatalog is written, then re-run.', 16, 1, @grantCode);
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissions
               WHERE RoleId = @roleId AND PermissionId = @permissionId)
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    VALUES (@roleId, @permissionId);

/* Re-runs must not widen the fixture: strip anything else that crept in, so
   "granted exactly one permission" stays true and the deny assertions keep
   their meaning. */
DELETE FROM dbo.RolePermissions
WHERE RoleId = @roleId AND PermissionId <> @permissionId;

/* ---- 3. The user, cloning the super-admin's credential material -------- */
DECLARE @restrictedId uniqueidentifier =
    (SELECT TOP 1 Id FROM dbo.Users WHERE NormalizedEmail = UPPER(@restrictedEmail));

IF @restrictedId IS NULL
BEGIN
    SET @restrictedId = NEWID();
    INSERT INTO dbo.Users
        (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
         PasswordHash, SecurityStamp, ConcurrencyStamp,
         PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
         DisplayName, AccountState, UserType, PasswordChangeRequired, CreatedAt)
    SELECT
         @restrictedId, @restrictedEmail, UPPER(@restrictedEmail), @restrictedEmail, UPPER(@restrictedEmail), 1,
         u.PasswordHash,                       -- same password as the QA super-admin
         CONVERT(nvarchar(36), NEWID()),       -- its own stamp: sign-out must not cascade
         CONVERT(nvarchar(36), NEWID()),
         0, u.TwoFactorEnabled, 1, 0,
         N'QA Restricted Admin',
         u.AccountState,   -- COPIED, not hardcoded. A literal 2 was written here
         u.UserType,       -- first: AccountState.Approved is 3, and 2 is
                           -- PendingApproval, so the fixture signed in and was
                           -- routed to /auth/pending on every route -- which reads
                           -- exactly like a permission denial. Copying the
                           -- super-admin's own values cannot drift from the enum.
         0,   -- never force a password change: it would block the sign-in this fixture exists for
         @now
    FROM dbo.Users u
    WHERE u.Id = @superId;
END
ELSE
BEGIN
    /* Keep it usable if the super-admin's password was re-seeded since. */
    UPDATE r
       SET r.PasswordHash            = s.PasswordHash,
           r.AccountState            = s.AccountState,
           r.UserType                = s.UserType,
           r.PasswordChangeRequired  = 0,
           r.LockoutEnd              = NULL,
           r.AccessFailedCount       = 0
      FROM dbo.Users r
      JOIN dbo.Users s ON s.Id = @superId
     WHERE r.Id = @restrictedId;
END;

/* ---- 4. The TOTP secret — the part that makes CP sign-in possible ------ */
/* SignInService forces TOTP for any user with a role, so without this row the
   fixture can authenticate its password and then never get past /login/totp. */
IF NOT EXISTS (SELECT 1 FROM dbo.UserTokens
               WHERE UserId = @restrictedId
                 AND LoginProvider = N'[AspNetUserStore]'
                 AND Name = N'AuthenticatorKey')
    INSERT INTO dbo.UserTokens (UserId, LoginProvider, Name, Value)
    SELECT @restrictedId, t.LoginProvider, t.Name, t.Value
      FROM dbo.UserTokens t
     WHERE t.UserId = @superId
       AND t.LoginProvider = N'[AspNetUserStore]'
       AND t.Name = N'AuthenticatorKey';
ELSE
    UPDATE t
       SET t.Value = s.Value
      FROM dbo.UserTokens t
      JOIN dbo.UserTokens s
        ON s.UserId = @superId
       AND s.LoginProvider = N'[AspNetUserStore]'
       AND s.Name = N'AuthenticatorKey'
     WHERE t.UserId = @restrictedId
       AND t.LoginProvider = N'[AspNetUserStore]'
       AND t.Name = N'AuthenticatorKey';

/* ---- 5. Role membership — and ONLY this role -------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles
               WHERE UserId = @restrictedId AND RoleId = @roleId)
    INSERT INTO dbo.UserRoles (UserId, RoleId)
    VALUES (@restrictedId, @roleId);

/* An extra role would hand it extra permissions and silently turn every deny
   assertion green. */
DELETE FROM dbo.UserRoles
WHERE UserId = @restrictedId AND RoleId <> @roleId;

COMMIT TRANSACTION;

SELECT
    N'qa-restricted@simf.test' AS RestrictedAdmin,
    (SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId = @roleId) AS GrantCount,
    (SELECT COUNT(*) FROM dbo.UserTokens
      WHERE UserId = @restrictedId AND Name = N'AuthenticatorKey')    AS HasTotpSecret;
