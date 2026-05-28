# E2E test catalogue — Visitor profile (Web) (`/account/profile`)

| | |
|--|--|
| **Page** | [`web/account-profile.md`](../../pages/web/account-profile.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-WEB-PRF-001 | Fill complete profile + save → toast | P0 |
| E2E-WEB-PRF-002 | Approved visitor sees the QR card | P0 |
| E2E-WEB-PRF-003 | Pending visitor sees no QR card but can still fill | P0 |
| E2E-WEB-PRF-004 | Notifications link in header routes to /account/notifications (D-132 orphan fix) | P0 |
| E2E-WEB-PRF-005 | RTL render of the full page + form | P1 |

## Scenarios

### E2E-WEB-PRF-001 — Fill + save

```gherkin
Scenario: Visitor fills their profile and saves
  Given V1 is Approved + signed in
  When V1 opens /account/profile
  And fills Identity (Names + DOB + Place of birth)
  And fills Nationality + Saudi ID (regex-valid)
  And fills Mobile + Email
  And picks 3 interest chips
  And uploads an ID document (1 MB PNG)
  And clicks Save
  Then PUT /account/api/profile fires
  And the toast reads Account.Profile.Saved
  And the QR card stays visible (Approved + minted)
```

### E2E-WEB-PRF-002 — QR card on Approved

```gherkin
Scenario: Approved visitor sees the QR card
  Given V1 is Approved + QrId minted
  When V1 opens /account/profile
  Then the QR card renders at the top
  And shows the server-rendered SVG QR
  And the 12-char QR id below
```

### E2E-WEB-PRF-003 — Pending: no QR, still editable

```gherkin
Scenario: Pending visitor can fill the profile while waiting
  Given V2 is PendingApproval
  When V2 opens /account/profile
  Then the QR card is hidden
  And the form fields are editable
  And Save still works (PUT /account/api/profile)
  When admin approves V2
  And V2 reloads the page
  Then the QR card appears (D-046a minted on approval)
```

### E2E-WEB-PRF-004 — Notifications link

```gherkin
Scenario: D-132 orphan fix — Notifications link in header
  Given V1 is on /account/profile
  Then a Notifications anchor renders in the header next to Sign out
  When V1 clicks it
  Then they land on /account/notifications
```

### E2E-WEB-PRF-005 — RTL

```gherkin
Scenario: Arabic toggle mirrors the page
  Given V1 toggles "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And every form field label is Arabic
  And the QR card text + Notifications link mirror direction
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
