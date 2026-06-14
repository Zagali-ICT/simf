# E2E test catalogue — `Identity verification` (`identityVerification`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the guided
> face-capture / liveness flow for the avatar (D-404, Figma 758:4180 →
> 758:4248 → 758:4316). Reached from the My-Area identity-card avatar tap; the
> captured selfie uploads via the existing `POST /app/account/avatar`.
> Widget/unit tests:
> `src/Mobile/simf_app/test/features/myarea/identity_verification_screen_test.dart`.

| | |
|--|--|
| **Page** | [`mobile/identity-verification`](../../pages/mobile/identity-verification.md) |
| **Route** | app screen #103 `/my-area/verify-identity` · `POST /app/account/avatar` |
| **Surface** | Mobile (Flutter) |
| **Role/gate** | Signed-in + Approved (launched only from the My-Area approved dashboard) |
| **Test runner** | Flutter widget/unit test (screen) + device manual (live camera) |

---

### E2E-MOBIDV-001 — Golden liveness path (device only)

```gherkin
Scenario: The user passes the three liveness steps and sets the avatar
  Given an approved user opens منطقتي and taps their avatar (camera badge)
  And the device has a working front camera with permission granted
  When the التحقق من الهوية screen opens
  Then the live front-camera preview shows in the gold-bordered frame
  And the prompt reads "ابتسم" with the first progress dot active
  When the user smiles (ML Kit smilingProbability ≥ 0.7)
  Then the forward selfie is captured and the prompt advances to "ادر راسك لليمين"
  When the user turns right (headEulerAngleY ≥ +20°)
  Then the prompt advances to "ادر راسك لليسار"
  When the user turns left (headEulerAngleY ≤ -20°)
  Then the screen pops, the captured selfie uploads to POST /app/account/avatar
  And the My-Area dashboard reloads showing the new photo
  # Device-only — the AVD virtual camera cannot perform a real smile/turn.
```

### E2E-MOBIDV-002 — Camera unavailable → gallery fallback

```gherkin
Scenario: No live camera falls back to the gallery (no liveness)
  Given the camera or the ML Kit plugin is unavailable (web / no camera /
        permission denied)
  When the التحقق من الهوية screen opens
  Then it shows "الكاميرا غير متاحة …" and a "اختر من المعرض" button
  When the user picks a gallery photo
  Then it uploads to POST /app/account/avatar and the dashboard reloads
  And the liveness check is skipped (documented limitation)
```

### E2E-MOBIDV-003 — Step gate thresholds (unit)

```gherkin
Scenario: livenessStepSatisfied enforces the thresholds
  Then smile passes only when smilingProbability ≥ 0.7 (never on a null value)
  And turn-right passes only when headEulerAngleY ≥ +20°
  And turn-left passes only when headEulerAngleY ≤ -20°
```

### E2E-MOBIDV-004 — Cancel / upload failure / RTL

```gherkin
Scenario: Cancelling mid-flow leaves the avatar unchanged
  Given the verification screen is open
  When the user taps back before finishing
  Then no upload happens and the avatar is unchanged

Scenario: Upload failure surfaces a toast
  Given a selfie was captured/picked
  When POST /app/account/avatar fails
  Then the avatarUploadFailed toast shows and the avatar is unchanged

Scenario: RTL
  Given the app language is Arabic
  Then the header التحقق من الهوية and the step prompts render right-to-left
```

---

_Last reviewed:_ `2026-06-14` by `SIMF Team`.
