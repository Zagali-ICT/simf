# Domain-model audit — findings deferred to a later wave

Date: 2026-08-16
Source: the domain-model audit programme (PRs 352-361, decisions D-917..D-925)

Everything here was **found and verified** during the programme and deliberately
**not acted on**, either because it sat outside the lane that found it or because
it needs a decision that is not an engineer's to take. Each entry says which.

Nothing in this list is a guess. Where a claim names a file, a line or a count, it
was read during the wave that recorded it.

---

## 1. Schema — needs a named lift (D-895)

### 1.1 `SessionSummaries` carries an index nothing can use
`SessionSummaryConfiguration` declares `HasIndex(s => new { s.IsActive, s.PublishedAt })`.
Every query against the table filters `SessionId` first (the admin list's
correlated sub-select, the admin get, generate, save, `LoadSummaryAsync`, the app
read, the host read, `OwnerPointerSync`), and `SessionId` carries a **unique**
index, so the planner seeks that one and never reaches this. Its leading column is
constant-true besides: nothing in the backend writes `SessionSummary.IsActive =
false`.

Same class of finding as the `SessionOutcomes` leg dropped in D-919. Dropping it
is a schema change, so it needs the lift.

### 1.2 The admission relocation is still a dual-write
D-877 specified moving admission from `SimfUser.AccountState` onto
`UserProfile.AdmissionState` as a **relocation, not a copy**. As built, both are
live: `AdminAccountService.Approval` writes `subject.AccountState` *and*
`profile.AdmissionState` on approve and on reject. The read path is already
correct - `QrResolver` resolves a `Disabled` account onto admission rather than
mirroring it - but the write path bends D-157's "no duplicated data" rule on this
one fact, with no transaction spanning the two databases to keep them honest.

Finishing it means **dropping an Identity column**, which is a frozen surface, so
it needs its own lift and its own argument. CLAUDE.md's D-895 section already
names this as not-built; this entry records what specifically remains.

### 1.3 `UserProfile.QrId` was never widened
D-880 designed an encrypted event badge in `QrId`; the column is still
`nvarchar(16)` holding the 12-character Crockford serial. The widening that *did*
land is `GateScan.QrIdAtScan` at 96, which is the audit column recording what was
physically presented at the gate. Already named in CLAUDE.md as not-built; repeated
here so the two `QrId`-shaped columns are not confused again.

---

## 2. Tests — no decision needed, just not this lane's file

- `tests/SIMF.Api.Tests/Files/FilesystemFileStorageProviderTests.cs` already
  asserts `CipherFormatVersion` 0 and 1 at lines 49 and 63, and is the natural
  home for a one-line `KekVersion` assertion on each.
- No test covers `StoredFileService.RestoreBytesAsync` refreshing a stale
  `KekVersion` stamp - the bug D-922 fixed. The safety property is that the save
  fires only when the stamps differ, which is exactly what a test should pin.
- `tests/SIMF.Api.Tests/SessionSummaryCommitteeTests.cs` lines 960-983 assign the
  three summary actor columns directly. If 1.1's sibling question - whether those
  columns survive - is ever reopened, this is the file that has to move with them.

---

## 3. Documentation — describes things that were never built

Found while verifying SIMF-DAT-001 against the generated migrations. Each is a
controlled document naming an entity with **no table and no domain class**:

| Document | Names | Reality |
|---|---|---|
| `SIMF-FDS-009-Notifications.md` | `NotificationDelivery` | Exists in no database |
| `SIMF-FDS-011-Statistics-and-Dashboards.md` | `StatisticSnapshot` | Never built; statistics are computed live |
| `SIMF-FDS-004-Forum-Programme.md` | `SubTopic` | No table, no entity |

Stale column names surviving in prose after the PR 356 renames:
`EquipmentNotes` in `SIMF-D134-Module-Build-Plan.md`, `docs/tests/SIMF-Business-Flows.md`
and `DECISIONS_LOG.md`; `ReminderSentUtc` in `docs/tests/e2e/bi-meeting-lifecycle.md`.
`DECISIONS_LOG.md` D-895 is doubly stale on the second one: it corrects
`ReminderSentUtc` to `ReminderSent`, and the built column is `ReminderSentAt`.

- **A zero-byte presentation has no DB-level guard any more.** D-926 dropped
  `CK_SpeakerPresentations_SizeBytes` (`[SizeBytes] > 0`) with the column it
  constrained. It cannot move to `StoredFile.SizeBytes`, which is nullable so an
  `ExternalLink` row can have no byte count. `AdminSpeakerPresentationService`
  still returns 400 `SPEAKER_PRESENTATION_INVALID` on an empty upload, so every
  real creation path is covered; a seed or repair script writing straight to the
  table is not. A filtered CHECK on the store (`SizeBytes > 0 WHERE SourceType
  <> ExternalLink`) would close it for every file at once, and is worth
  considering the next time the file schema is open.

Gaps rather than errors:

- **The two rotation keys have no declared home.** D-922 added
  `SIMF_API_FileStorage__PreviousEncryptionKey` and `__PreviousKekVersion` to
  `deploy/set-env-api.template.ps1`; PR 362 deleted every template in favour of
  five `set-env-*.ps1` that are tracked on no branch, so this merge dropped both
  declarations rather than re-homing them. Add them to `deploy/set-env-api.ps1`
  when that file lands. Until then the rotation window the column measures cannot
  be configured, which is consistent with the re-wrap job not existing either.
- `SIMF-OPS-001` section B.1's configuration matrix has **no `FileStorage` rows at
  all** - not `EncryptionKey`, not `RootPath`, not the new `KekVersion` pair. It
  predates the centralised file store.
- `docs/manuals/SIMF-File-Store-Dev-Guide.md` describes the cipher and the provider
  and does not mention `KekVersion`.
- `SIMF-DAT-001` does not document `StoredFile.KekVersion`, and its sections 5.6,
  5.7 and 5.10 remain **unverified** against the built schema, shielded by its own
  Amendment C.7. Every other section of 5 has now been checked.

---

## 4. Deliberately kept, recorded so the next sweep does not re-raise them

- **`SessionSummary` does not derive `BaseAuditEntity`.** Deriving would start
  force-stamping a row the admin desk stamps by hand, because
  `AuditStampingSaveChangesInterceptor` iterates `Entries<BaseAuditEntity>()`. It
  would also add `CreatedBy` and `DeletedAt`, which the entity does not have. The
  divergence is documented on the class.
- **`SessionSummary`'s three actor columns and its `IsActive`.** Redundant as
  history against `RowAudit`, unique as current state. See D-919.
- **The superseded mobile pair.** `SaudiMobile` and `InternationalMobile` are
  still written in lockstep with `MobileNumber`, because the shipped app decodes
  those JSON keys by name. They retire when the app does, not before. The three
  per-kind identity columns were the same case until PR 355 dropped them, so the
  child table's `NumberHash` index is now the only uniqueness guard.

---

## 5. Closed by this programme, recorded because it cost real time

**The regenerated migration used to break `main` every time two branches landed.**
It happened twice on 2026-08-16, three `InitialCreate` classes in one namespace
each time. Not a merge failure - a merge *success*: a timestamped migration id
gives every branch its own filename, so git had nothing to conflict on and both
files survived, with `mergeStatus: succeeded` on both pull requests.

**Fixed by D-925** - the id is pinned to `00000000000000_InitialCreate`,
regeneration goes through `tools/migrations/Regenerate-Migration.ps1`, and
`SchemaFreezeTests` fails the build on any other id. Two branches that regenerate
now write the same path and collide loudly.

The standing procedural rule survives the fix and is worth keeping: **land one
migration-bearing PR at a time, and re-sync the next branch onto `main` before
completing it.** The pin makes a missed re-sync noisy; it does not make it free.
