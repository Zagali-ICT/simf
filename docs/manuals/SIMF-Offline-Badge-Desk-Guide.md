# Offline badge desk — provisioning and operating guide

**Applies to:** `src/Tools/SIMF.BadgeDesk` (D-809 / D-810)
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

The badge carries an **encrypted** QR: the profile-type code and the desk
sequence, two plain numbers, AES-GCM encrypted under the event badge key. A
scanner with the same key can verify it with no network. The server decrypts it
independently on every scan, so the audit trail records exactly what was
presented at the gate.

---

## Before the event — provisioning (not an operator task)

### 1. Arm the capability on the API

```
SIMF_WalkInMode__Enabled            = true
SIMF_WalkInMode__OfflineUpload      = true
SIMF_WalkInMode__AcceptOfflineBadges= true
SIMF_WalkInMode__AutoApprove        = true
SIMF_WalkInMode__BadgeKey           = <base64 AES-256 key>
SIMF_WalkInMode__BadgeKeyVersion    = 1
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
| **F5** | Upload everything not yet uploaded |

Fields: name, Arabic name (optional), badge type, **ID / Iqama / passport**,
mobile (optional).

**The identity document is required.** It is the only thing preventing one
person collecting several badges, and it cannot be reconstructed after the
event — those columns are encrypted with a random nonce.

### If the printer fails

The visitor is still registered. The registration is written to disk **before**
anything is printed, precisely so a paper badge can never exist that no record
knows about. Reprint and carry on.

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
| `Rejected` | Not written. The error code says why — usually a duplicate identity document. |

Only accounted-for rows are marked done locally, so a rejected row stays pending
and is retried or chased by hand. **Reconciliation is complete when "waiting to
upload" reads 0.**

Uploading twice is safe. Uploading a batch that was half-accepted is safe. An
interrupted upload is resumed by pressing F5 again.

Batches cap at 500 rows per request; a larger backlog uploads in several passes
automatically.

---

## Where the data lives

```
%ProgramData%\SIMF\BadgeDesk\registrations.jsonl
```

Append-only, one JSON object per line, flushed to disk on every write. Until it
is uploaded, **this file is the only record that a badge was handed out.** Do
not delete it, and back it up before wiping a desk machine.

The format is append-only because the realistic failure at a venue is losing
power mid-shift: a truncated final line is the only damage possible, and the app
skips it and keeps the rest of the shift.

---

## Pre-event checklist

- [ ] Badge key generated and identical on the API and every desk
- [ ] `DeskNumber` unique per desk, and each desk opened once to prove it starts
- [ ] `WalkInMode` armed on the API, including `AutoApprove`
- [ ] `Gate.HallId` set on each hall-door gate (otherwise no session attendance is recorded)
- [ ] **Print and scan a real badge** — real printer, real stock, real handset,
      real venue lighting, including a creased one. A previous SIMF badge style
      was undecodable by the scanner; square modules and a quiet zone fixed it,
      and this payload is longer, so re-test at true print size.
- [ ] Full rehearsal: unplug the network, register and print 50, scan at two
      gates, restore the network, upload, reconcile to zero
