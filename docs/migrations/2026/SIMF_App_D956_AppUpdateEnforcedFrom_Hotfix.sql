/*
  SIMF_App - the forced-update grace period, on a RUNNING database.

  WHAT CHANGED IN CODE
    `appUpdate.{android,ios}.minVersion` used to take effect the moment an admin
    typed it: the API emitted it, and every install below it was hard-blocked on
    its next launch. There was no grace period, so the one field could strand
    the whole fleet - including users still waiting on a Play staged rollout or
    iOS Phased Release, who physically cannot install the build they are being
    told to install, and including an App Store reviewer.

    The API now emits `minVersion` only once `appUpdate.{platform}.minVersionEnforcedFrom`
    has arrived, and null before it. Null has always meant "rule off" to the
    app, so this works on builds that shipped before the key existed - which is
    the whole reason the decision sits on the server and not in Dart.

  WHY THIS FILE EXISTS
    Two halves of the change do not reach a live database on their own.

      1. The two new keys. `DefaultContentSeeder` does create them on the next
         API boot, so this half is belt-and-braces - it lets an operator
         configure the grace period BEFORE the deploy, or recover if the row was
         deleted.

      2. The four descriptions. The seeder is keyed on `Key` alone and never
         overwrites an existing row, deliberately, so an admin edit survives a
         restart. The consequence is that production's existing four rows keep
         their OLD description for ever - text that now says the opposite of
         what the code does ("Older installs are blocked until they update").
         `/admin/configuration` is generic key/value CRUD with no help text of
         its own, so that description is the ONLY place an operator is told how
         the key behaves. Stale text there is not cosmetic.

  SAFE TO RE-RUN, AND SAFE OVER AN ADMIN EDIT. Inserts are guarded on NOT
  EXISTS. The updates rewrite only the Description column - never Value, never
  IsActive - and only where the description is still the ORIGINAL seeded text.
  An administrator who reworded a description through /admin/configuration
  keeps their wording, and a second run matches nothing and therefore does not
  even bump UpdatedAt.

  IF A GATE IS ALREADY LIVE WHEN YOU DEPLOY THIS
    Read this before running the script, not after. If production currently
    has a minVersion set AND enforcing, this change lifts it: the new
    enforced-from key arrives empty, and empty means no gate. Every install
    that was blocked is admitted again on its next launch, silently. To keep
    the existing block in place, set the enforced-from value to TODAY (or any
    past date) in the same deployment window.

  RUNBOOK - READ BEFORE SETTING A DATE
    Never set minVersionEnforcedFrom while the build is still in a Play staged
    rollout or iOS Phased Release. Allow at least 14 days. Format is
    yyyy-MM-dd and nothing else: any other format is ignored and the gate stays
    OFF (safe, but silent). A past date enforces immediately. Unticking IsActive
    on the enforced-from row lifts the gate.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Saudi wall-clock, which is what SimfClock stamps. Deliberately arithmetic
-- rather than AT TIME ZONE: the zone id differs by host OS ('Arab Standard
-- Time' on Windows, 'Asia/Riyadh' on Linux) and the wrong one aborts the whole
-- script with Msg 9820. KSA is a fixed +03:00 with no daylight saving, ever,
-- so the addition is exact and needs no zone table.
DECLARE @Now datetime2(7) = DATEADD(hour, 3, SYSUTCDATETIME());

/* ---------------------------------------------------------------------------
   1. The two new keys, empty (= no gate), so they appear on the CP grid ready
      to edit. Empty is the correct default: nobody should be blocked until an
      operator deliberately picks a date.
   --------------------------------------------------------------------------- */

INSERT INTO dbo.SystemSettings (Id, [Key], [Value], [Description], IsActive, CreatedAt, CreatedBy)
SELECT NEWID(),
       'appUpdate.android.minVersionEnforcedFrom',
       '',
       'Date from which appUpdate.android.minVersion is actually enforced, as yyyy-MM-dd (e.g. 2026-10-15). Empty, inactive or any other format = the minimum is NOT enforced and nobody is blocked; a past date enforces immediately. Until this date users only get the dismissible update prompt. Never set it while a Play staged rollout is still in progress, and allow at least 14 days.',
       1,
       @Now,
       '00000000-0000-0000-0000-000000000000'
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemSettings
    WHERE [Key] = 'appUpdate.android.minVersionEnforcedFrom');

INSERT INTO dbo.SystemSettings (Id, [Key], [Value], [Description], IsActive, CreatedAt, CreatedBy)
SELECT NEWID(),
       'appUpdate.ios.minVersionEnforcedFrom',
       '',
       'Date from which appUpdate.ios.minVersion is actually enforced, as yyyy-MM-dd (e.g. 2026-10-15). Empty, inactive or any other format = the minimum is NOT enforced and nobody is blocked; a past date enforces immediately. Until this date users only get the dismissible update prompt. Never set it while iOS Phased Release is still in progress, and allow at least 14 days.',
       1,
       @Now,
       '00000000-0000-0000-0000-000000000000'
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemSettings
    WHERE [Key] = 'appUpdate.ios.minVersionEnforcedFrom');

/* ---------------------------------------------------------------------------
   2. The four descriptions whose MEANING changed. Value and IsActive are left
      exactly as they are, and each UPDATE is matched on the ORIGINAL seeded
      wording so an administrator's own rewording is never overwritten.
   --------------------------------------------------------------------------- */

UPDATE dbo.SystemSettings
SET [Description] = 'Minimum supported Android app version (semver, e.g. 1.2.0). Blocks older installs, but ONLY from the date in appUpdate.android.minVersionEnforcedFrom. Empty = no forced-update gate.',
    UpdatedAt = @Now
WHERE [Key] = 'appUpdate.android.minVersion'
  AND ([Description] IS NULL OR [Description] LIKE '%blocked until they update%');

UPDATE dbo.SystemSettings
SET [Description] = 'Minimum supported iOS app version (semver, e.g. 1.2.0). Blocks older installs, but ONLY from the date in appUpdate.ios.minVersionEnforcedFrom. Empty = no forced-update gate.',
    UpdatedAt = @Now
WHERE [Key] = 'appUpdate.ios.minVersion'
  AND ([Description] IS NULL OR [Description] LIKE '%blocked until they update%');

UPDATE dbo.SystemSettings
SET [Description] = 'Latest released Android app version (semver, e.g. 1.4.0). Older installs get a dismissible update prompt. Empty falls back to the minimum version above, so setting only a minimum still warns users during the grace period.',
    UpdatedAt = @Now
WHERE [Key] = 'appUpdate.android.latestVersion'
  AND ([Description] IS NULL OR [Description] LIKE '%Empty = no prompt.%');

UPDATE dbo.SystemSettings
SET [Description] = 'Latest released iOS app version (semver, e.g. 1.4.0). Older installs get a dismissible update prompt. Empty falls back to the minimum version above, so setting only a minimum still warns users during the grace period.',
    UpdatedAt = @Now
WHERE [Key] = 'appUpdate.ios.latestVersion'
  AND ([Description] IS NULL OR [Description] LIKE '%Empty = no prompt.%');

COMMIT TRANSACTION;

/* Verify: eight rows, and no minimum enforced unless a date says so. */
SELECT [Key], [Value], IsActive
FROM dbo.SystemSettings
WHERE [Key] LIKE 'appUpdate.%'
ORDER BY [Key];
