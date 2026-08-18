# Domain-model audit — findings deferred to a later wave

Date: 2026-08-16
Closed out: 2026-08-18
Source: the domain-model audit programme (PRs 352-361, decisions D-917..D-925)

Everything here was **found and verified** during the programme and deliberately
**not acted on** at the time, either because it sat outside the lane that found it
or because it needed a decision that was not an engineer's to take. On 2026-08-18
the owner asked for the list to be worked; what closed is marked **DONE**, and what
is still open says plainly what it is waiting for.

Nothing in this list is a guess. Where a claim names a file, a line or a count, it
was read during the wave that recorded it.

---

## 1. Schema

### 1.1 `SessionSummaries` carries an index nothing can use — **DONE**
`SessionSummaryConfiguration` declared `HasIndex(s => new { s.IsActive, s.PublishedAt })`.
Every query against the table filters `SessionId` first (the admin list's
correlated sub-select, the admin get, generate, save, `LoadSummaryAsync`, the app
read, the host read, `OwnerPointerSync`), and `SessionId` carries a **unique**
index, so the planner seeks that one and never reaches this. Its leading column was
constant-true besides: nothing in the backend writes `SessionSummary.IsActive =
false`.

Dropped, with the reasoning left on the surviving unique index so it is not
re-added without a reader that filters on the publish stamp *without* a session id.

### 1.2 The admission relocation — the READ path is now single-source
D-877 specified moving admission from `SimfUser.AccountState` onto
`UserProfile.AdmissionState` as a **relocation, not a copy**.

**Closed on the read path.** `QrResolver` used to resolve a `Disabled` account onto
admission (`userRow?.AccountState == Disabled ? Disabled : profileRow.AdmissionState`),
which made the account a second input to a profile-owned decision. That fallback
existed because, when it was written, nothing withdrew admission on the profile.
That is no longer true: every production path that disables an account now
withdraws profile admission in the same transaction —
`AdminAccountService.Bulk` calls `RevokeProfileAdmissionAsync` on both disable
paths, and `DormantAccountService` clears the profile **before** the Identity save
so a failure re-selects the user rather than stranding them admitted. The gate
therefore reads the profile alone, and the Identity round-trip is down to the one
thing it alone knows, the lockout flag.

**Still open on the write path.** Approve and reject still write both
`subject.AccountState` and `profile.AdmissionState`. Finishing it means **dropping
an Identity column**, which is a frozen surface, so it needs its own lift and its
own argument — and the two states are not obviously the same fact: an account can
be disabled for reasons that have nothing to do with attending, and a walk-in with
no account is admitted with no `SimfUser` row at all.

### 1.3 `UserProfile.QrId` was never widened — **open, deliberately**
D-880 designed an encrypted event badge in `QrId`; the column is still
`nvarchar(16)` holding the 12-character Crockford serial. Nothing writes an
encrypted badge into it, so widening it now would buy a wider column and no
behaviour. The widening that *did* land is `GateScan.QrIdAtScan` at 96, which is
the audit column recording what was physically presented at the gate. Do it when
D-880's encrypted badge is actually built, not before.

### 1.4 A zero-byte file has a DB-level guard again — **DONE**
D-926 dropped `CK_SpeakerPresentations_SizeBytes` (`[SizeBytes] > 0`) along with
the duplicated column it constrained. It could not follow the data to
`StoredFile.SizeBytes` as written, that column being NULL for every `ExternalLink`
row and nulled again when an upload is converted into one. Re-added as
`CK_StoredFiles_SizeBytes` (`[SizeBytes] IS NULL OR [SizeBytes] > 0`), which
tolerates NULL and so covers every file service at once rather than presentations
alone. The creation paths already refuse an empty upload with a 400; what this
closes is a seed or a repair script writing straight to the table.

---

## 2. Tests — **DONE**

- `tests/SIMF.Api.Tests/Files/FilesystemFileStorageProviderTests.cs` now asserts
  `KekVersion` beside each `CipherFormatVersion` case: null on the plaintext write,
  the active version on the encrypted one.
- `tests/SIMF.Api.Tests/Files/StoredFileRestoreStampTests.cs` pins the bug D-922
  fixed — `RestoreBytesAsync` refreshing a stale `KekVersion`, in both directions.
  A stale stamp is corrected; a current one is left alone, proved with a sentinel
  `UpdatedAt` that survives the call, because a guard that always fires is a
  different bug from one that never does.
- `tests/SIMF.Api.Tests/SessionSummaryCommitteeTests.cs` lines 960-983 assign the
  three summary actor columns directly. If 1.1's sibling question — whether those
  columns survive — is ever reopened, this is the file that has to move with them.

---

## 3. Documentation — **DONE**

Found while verifying SIMF-DAT-001 against the generated migrations. Each was a
controlled document naming an entity with **no table and no domain class**; each
now says so rather than describing it as built:

| Document | Named | Reality |
|---|---|---|
| `SIMF-FDS-009-Notifications.md` | `NotificationDelivery` | Exists in no database |
| `SIMF-FDS-011-Statistics-and-Dashboards.md` | `StatisticSnapshot` | Never built; statistics are computed live |
| `SIMF-FDS-004-Forum-Programme.md` | `SubTopic` | No table, no entity |

Stale column names surviving in prose after the PR 356 renames, now corrected:
`EquipmentNotes` → `FacilityNotes` in `SIMF-D134-Module-Build-Plan.md` and
`docs/tests/SIMF-Business-Flows.md`; `ReminderSentUtc` → `ReminderSentAt` and
`SlotStartUtc` → `SlotStart` in `docs/tests/e2e/bi-meeting-lifecycle.md`.

`DECISIONS_LOG.md` is **deliberately not edited**. Its `EquipmentNotes` and
`ReminderSent` mentions are inside decision rows that were accurate on the day they
were written — D-895 predates the PR 356 renames by two days — and D-924 records
the renames as the current state. Correcting a historical row to match today's
schema would make the log lie about what was decided when, which is the one thing
it is for.

The configuration gaps are closed:

- `SIMF-OPS-001` section B.1's configuration matrix had **no `FileStorage` rows at
  all**. It now carries `RootPath`, `EncryptionKey`, `KekVersion`,
  `PreviousEncryptionKey` and `PreviousKekVersion`, each with its failure mode, and
  says in the same section that key rotation is **not operational** — the two
  previous-key settings and `StoredFile.KekVersion` exist, but the re-wrap job does
  not. That is where the two rotation settings D-922 added are now declared: the
  `deploy/set-env-*.ps1` scripts are tracked on no branch and hold live values, so
  they are not a documentation surface and were left untouched.
- `docs/manuals/SIMF-File-Store-Dev-Guide.md` now documents `KekVersion` alongside
  the cipher and the provider.
- `SIMF-DAT-001` now documents `StoredFile.KekVersion`, in a corrected §5.12
  `Asset` row - the four attributes it listed named no built column, and the
  `StoragePath` among them was the last place a document still implied a second
  physical path. Its sections 5.6, 5.7 and 5.10, the three Amendment C.7 had
  shielded as **unverified**, were read column by column against the generated
  migrations in the same pass (Amendment C.10) and corrected: four entities in them
  were never built, `MeetingRequest` is two entities and not one, and OI-2 closes
  because match suggestions are ranked per request and never stored. Every section
  of §5 has now been checked.

---

## 4. Deliberately kept, recorded so the next sweep does not re-raise them

- **`SessionSummary` does not derive `BaseAuditEntity`.** Deriving would start
  force-stamping a row the admin desk stamps by hand, because
  `AuditStampingSaveChangesInterceptor` iterates `Entries<BaseAuditEntity>()`. It
  would also add `CreatedBy` and `DeletedAt`, which the entity does not have. The
  divergence is documented on the class.
- **`SessionSummary`'s three actor columns and its `IsActive`.** Redundant as
  history against `RowAudit`, unique as current state. See D-919.
