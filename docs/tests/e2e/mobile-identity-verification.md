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
| **Role/gate** | **Signed-in, any role (universal-auth, D-694)** — the screen is on-device only (camera + ML Kit, no network) and `POST /app/account/avatar` is not role-gated. Two entry points: the sign-up face capture (a **pending** account — this is why 103 must NOT be attendee-gated) and the My-Area avatar change (visitor / exhibitor / staff / moderator). Signed-out is still redirected to sign-in. |
| **Test runner** | Flutter widget/unit test (screen) + device manual (live camera) |

> **Camera security rules (owner 2026-07-06, D-662) — MUST hold:**
> 1. **Human-verified** — the identity photo is only accepted after the liveness
>    challenge (smile → turn → turn) passes; a single still is never trusted.
> 2. **Live image only** — the source is the live front camera. There is **no
>    gallery/upload path and no manual shutter**, so a static "studio" photo can
>    never be submitted.
> 3. **No live camera → no photo** — where the camera or the ML Kit face detector
>    is unavailable (web / no camera / permission denied / a device without
>    Google Play Services) the screen shows a "camera required" message with a
>    **retry**, never a gallery fallback. On a no-Google-Play-Services device the
>    ML Kit liveness cannot run, so identity capture is unavailable there by
>    design (needs a Play-Services device or a Huawei-ML-Kit / provider swap).

---

### E2E-MOBIDV-001 — Golden liveness path (device only)

```gherkin
Scenario: The user passes the three liveness steps and sets the avatar
  Given an approved user opens منطقتي and taps their avatar (camera badge)
  And the device has a working front camera with permission granted
  When the التحقق من الهوية screen opens
  Then the full-bleed live front-camera preview shows with the step prompt over it
  And the prompt reads "ابتسم"
  When the user smiles (ML Kit smilingProbability ≥ 0.7)
  Then the forward selfie is captured and the prompt advances to "أدر رأسك يمينًا"
  When the user turns right (headEulerAngleY ≥ +20°)
  Then the prompt advances to "أدر رأسك يسارًا"
  When the user turns left (headEulerAngleY ≤ -20°)
  Then the screen pops, the captured selfie uploads to POST /app/account/avatar
  And the My-Area dashboard reloads showing the new photo
  # Device-only — the AVD virtual camera cannot perform a real smile/turn.
```

### E2E-MOBIDV-002 — Camera unavailable → "camera required" retry (no gallery)

```gherkin
Scenario: No live camera shows the camera-required message (never a gallery)
  Given the camera or the ML Kit detector is unavailable (web / no camera /
        permission denied / no Google Play Services)
  When the التحقق من الهوية screen opens
  Then it shows "الكاميرا مطلوبة للتحقق من الهوية بصورة حية …" and a
       "إعادة المحاولة" (retry) button
  And there is NO gallery / upload option (live image only, D-662)
  When the user grants the camera permission and taps إعادة المحاولة
  Then the live preview initialises and the liveness challenge starts
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

_Last reviewed:_ `2026-07-06` by `SIMF Team`.
