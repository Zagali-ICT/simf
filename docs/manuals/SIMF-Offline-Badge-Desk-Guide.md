# Offline badge desk — provisioning and operating guide

**Applies to:** `src/Tools/SIMF.BadgeDesk` (D-819 / D-820 / D-821 / D-823 / D-824)
**Audience:** whoever sets the desks up, and the operators who run them
**Last updated:** 2026-08-01

The badge desk registers visitors and prints working badges **with no network at
all**. It is for the case this whole capability exists for: a large crowd arrives
who never installed the app, never registered online, and the venue link is down
or saturated.

---

## What it does and does not do

| | |
|---|---|
| Registers a visitor | Yes, offline |
| Prints a working badge | Yes, offline |
| Opens a gate | Yes, once the badge is uploaded **and approved** |
| Needs the network | Only to upload, once a shift |
| Holds any password | **No.** See "Uploading" below |

The badge carries an **encrypted** QR: the attendee's profile id, the edition
year and the profile-type code, AES-256-GCM encrypted under the event badge key.
A scanner with the same key verifies it with no network. The server decrypts it
independently on every scan, so the audit trail records exactly what was
presented at the gate.

The three fields are packed as **raw bytes**, not as text: a 16-byte profile id,
a 2-byte year and a 2-byte type code. That is a size decision, not a taste one.
The same profile id written as 32 hexadecimal characters would push the printed
code past 96 characters on its own, and anything longer than 96 is refused at the
gate as "not recognised" *before* it is ever decrypted — which at a desk with a
queue is undiagnosable.

The **edition year** is what stops last year's badge opening this year's gate. A
badge whose year is not the open edition is refused exactly as an unknown code
is, and deliberately not with a distinct message: a scan must never tell the
holder which half of the check failed.

A printed badge is **78 characters** (80 in the extreme case of a three-digit
profile-type code). That is why `GateScans.QrIdAtScan` is `nvarchar(96)`.

---

## Before the event — provisioning (not an operator task)

### 1. Arm the capability on the API

```
SIMF_API_WalkInMode__Enabled            = true
SIMF_API_WalkInMode__OfflineUpload      = true
SIMF_API_WalkInMode__AcceptOfflineBadges= true
SIMF_API_WalkInMode__AutoApprove        = true
SIMF_API_WalkInMode__BadgeKey           = <base64 AES-256 key>
SIMF_API_WalkInMode__BadgeKeyVersion    = 1
```

`AutoApprove` matters here: without it every uploaded badge lands in the pending
queue and **is refused at the gate** until an administrator approves it. The
upload response says so per row, but the badges are already in people's hands by
then.

### 2. Generate the badge key, once, for the whole event

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }))
```

The same string goes into the API configuration **and** every desk. A desk with
a different key prints badges no gate will open.

### 3. Provision each desk

Edit `appsettings.json` beside `SIMF.BadgeDesk.exe`:

```json
{
  "DeskNumber": 3,
  "DeskLabel": "Desk 3 — north entrance",
  "BadgeKey": "<the same base64 key>",
  "BadgeKeyVersion": 1,
  "ApiBaseUrl": "https://api.simf.example",
  "ProfileTypes": [
    { "Code": 1, "Name": "Visitor", "NameArabic": "زائر" },
    { "Code": 2, "Name": "VIP", "NameArabic": "شخصية مهمة" }
  ]
}
```

**`DeskNumber` must be unique across every desk.** It is what keeps sequences
from colliding: desk 3 issues 3,000,001 upward, desk 4 issues 4,000,001 upward,
and the desks never have to talk to each other. Two desks sharing a number would
print two different visitors onto the same badge id.

`ProfileTypes` is copied from `GET /api/v1/admin/profile-types` while online.
`Code` is `ProfileType.Code`, the small number, **not** the Guid.

The app refuses to open and names every problem if any of this is wrong, so
provision it on the bench, not at the venue.

### 4. Build it

Deliberately outside `SIMF.sln`, because it is Windows-only and the solution has
to keep building anywhere:

```
dotnet publish src/Tools/SIMF.BadgeDesk -c Release -r win-x64 -p:SelfContained=true
```

---

## Operating the desk

Keyboard only. An operator facing a queue never touches the mouse.

| Key | Does |
|---|---|
| **Enter** | Next field; from the last field, register and print |
| **Esc** | Clear the form |
| **F2** | Reprint the last badge |
| **F3** | Correct a registration the server rejected |
| **F5** | Upload everything not yet uploaded |

Fields: name, Arabic name (optional), badge type, **ID / Iqama / passport**,
mobile (optional).

**The identity document is required.** It is the only thing preventing one
person collecting several badges, and it cannot be reconstructed after the
event — those columns are encrypted with a random nonce.

### If the printer fails

The visitor is still registered. The registration is written to disk **before**
anything is printed, precisely so a paper badge can never exist that no record
knows about. Press **F2** to reprint and carry on.

F2 reprints from the stored record, so the reissued badge carries the **same
sequence** as the one that jammed. Printing a new number would put two badge ids
on one visitor and break the reconciliation the upload depends on.

### If the app is closed and reopened

It resumes at the next unused sequence, read from the file rather than from a
counter held anywhere else, so numbers already on paper are never reissued.

---

## Uploading

Press **F5**, paste a Control Panel bearer token, and the shift goes up.

**The desk stores no credentials.** It is an unattended machine on a folding
table in a public hall — an administrator's password on it would be the worst
credential exposure in the system, and pasting a token once a shift costs
nothing. The token lives in memory for that upload only.

The response is the reconciliation report:

| Status | Means |
|---|---|
| `Created` | Account created and approved. The badge works. |
| `CreatedPendingApproval` | Created, but auto-approve is not armed. **That badge will be refused at the gate** until an administrator approves it. |
| `AlreadyUploaded` | Seen before. Nothing changed. |
| `Rejected` | Not written. The message says why — a duplicate identity document, or a number that failed its check digit. **Fix it with F3 and upload again.** |

Only accounted-for rows are marked done locally, so a rejected row stays pending
until it is corrected. **Reconciliation is complete when "waiting to upload"
reads 0.**

### Correcting a rejected row (F3)

1. The upload report **names every rejected badge number and why**. Read it off
   the screen.
2. Type the corrected details into the form. **A box you leave blank is left
   alone** — only what you retype is changed, so fixing an ID does not wipe the
   mobile number.
3. Press **F3**. The dialog offers the most recently rejected number and shows
   whose record it is — check that name before confirming.
4. Press **F5** to upload again.

**The badge number never changes**, so the paper in the visitor's hand keeps
working: the QR encodes the profile id, the edition year and the badge type, and
a correction touches none of the three. Issuing a new number would put two badge
ids on one person and break the reconciliation.

**Reprint only if you corrected the NAME.** The name is printed on the badge as
well as encoded in the QR, so a corrected name makes the paper wrong even though
the code still scans. The desk tells you when this applies; press **F2** and the
same badge number reprints with the right name.

A row that has already uploaded successfully cannot be corrected here — the
account exists by then and the Control Panel owns it.

Uploading twice is safe. Uploading a batch that was half-accepted is safe. An
interrupted upload is resumed by pressing F5 again.

Batches cap at 500 rows per request; a larger backlog uploads in several passes
automatically.

---

## Where the data lives

```
%ProgramData%\SIMF\BadgeDesk\registrations.jsonl
```

Append-only, one **encrypted** record per line, flushed to disk on every write.
Until it is uploaded, **this file is the only record that a badge was handed
out.** Do not delete it, and back it up before wiping a desk machine.

**D-823 — the lines are encrypted with Windows DPAPI (machine scope).** A record
carries the holder's name, mobile and identity-document number; the server keeps
those same columns encrypted at rest, so a plaintext copy here was the softest
target for them in the whole system. The file therefore reads **only on the desk
that wrote it** — a copy taken on a USB stick, or the disk read outside Windows,
yields nothing.

Two consequences worth knowing before the event:

- **Do not move the file between desks.** It will not decrypt, and the desk
  refuses to open rather than start empty and reissue numbers already on paper.
- **This is not a substitute for disk encryption** on the desk machine. It
  removes the file-copy exposure, which is the risk a folding table in a public
  hall actually creates.

The format is append-only because the realistic failure at a venue is losing
power mid-shift: a truncated final line is the only damage possible, and the app
skips it and keeps the rest of the shift.

---

## What the scanner does with no network (D-821)

An operator's scanner caches its rules from `GET /app/gates/offline-config`
whenever the gate console loads, so a device that boots into a dead network
still has the last known rules rather than nothing.

With no network it can:

- **Admit** a badge whose type its gate allows.
- **Refuse** a badge that does not decrypt under a key it holds — forged or
  damaged.
- **Refuse** a type its gate does not admit. This rule is never relaxed.

It deliberately **abstains** — shows "queued, decision pending" rather than a
denial — whenever the answer needs live data: a hall-door booking, or an account
that was approved this morning and disabled since. Denying on stale data would
turn every offline hall door into a wall.

**Every offline verdict is advisory.** The scan is still queued and uploaded,
and the server re-decides it against live data. A device can therefore let
someone through who a live check would have refused; that is the accepted cost
of a gate that keeps working through an outage, and it is exactly why every scan
is reconciled afterwards.

The badge key reaches a device **only while `AcceptOfflineBadges` is armed**.
Disarming it is therefore also what stops handing the key out — the lever to
pull if a device goes missing, alongside rotating `BadgeKeyVersion`.

---

## Pre-event checklist

- [ ] Badge key generated and identical on the API and every desk
- [ ] **`BadgeKeyVersion` bumped, and rolled out in this order: API first, then
      every scanner confirmed updated, then the desks.** The order is not
      housekeeping. A scanner running an older build decrypts a new-format badge
      successfully and only then fails to read the fields, and it reports that as
      a *forged badge* rather than abstaining — so an un-updated handset would
      tell an operator that a genuine visitor's badge is fake, offline, where no
      server can overrule it. Bumping the version instead makes the old scanner
      see a key version it does not hold, which it already treats as "cannot
      judge, queue it". Confirm the fleet before any desk prints.
- [ ] The **open edition year** set on the API, and checked against the year the
      desks will print. A desk stamping a year that is not the open edition
      prints badges every gate refuses.
- [ ] `DeskNumber` unique per desk, and each desk opened once to prove it starts
- [ ] `WalkInMode` armed on the API, including `AutoApprove`
- [ ] `Gate.HallId` set on each hall-door gate (otherwise no session attendance is recorded)
- [ ] **Print and scan a real badge** — real printer, real stock, real handset,
      real venue lighting, including a creased one. A previous SIMF badge style
      was undecodable by the scanner; square modules and a quiet zone fixed it,
      and this payload is longer, so re-test at true print size.
- [ ] Full rehearsal: unplug the network, register and print 50, scan at two
      gates (including one refused profile type and one hall door), restore the
      network, upload, reconcile to zero
