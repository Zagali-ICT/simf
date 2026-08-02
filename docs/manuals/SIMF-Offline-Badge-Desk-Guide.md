# Offline badge desk — provisioning and operating guide

**Applies to:** `src/Tools/SIMF.BadgeDesk` (D-809 / D-810 / D-811 / D-813)
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
sequence, two plain numbers, AES-256-GCM encrypted under the event badge key. A
scanner with the same key verifies it with no network. The server decrypts it
independently on every scan, so the audit trail records exactly what was
presented at the gate.

A printed badge is about **61 characters** (67 in the extreme case of a 10-digit
sequence). That is why `GateScans.QrIdAtScan` is `nvarchar(96)`.

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
| **F2** | Reprint the last badge |
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

Append-only, one **encrypted** record per line, flushed to disk on every write.
Until it is uploaded, **this file is the only record that a badge was handed
out.** Do not delete it, and back it up before wiping a desk machine.

**D-813 — the lines are encrypted with Windows DPAPI (machine scope).** A record
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

## What the scanner does with no network (D-811)

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
