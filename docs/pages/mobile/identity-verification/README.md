# Identity verification (التحقق من الهوية) — mobile `/my-area/verify-identity`

| Field | Value |
|---|---|
| Route | `/my-area/verify-identity` (`RouteNames.identityVerification`, page #103) · **universal auth** — in `_authenticatedRoutes`, so **any** signed-in account reaches it, including a pending one that resolves to `AppRole.guest` (D-694) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/myarea/identity_verification_screen.dart` (`IdentityVerificationScreen`, 397 lines, plain `StatefulWidget` — no Riverpod, because nothing here touches the network) |
| Widgets | `features/myarea/widgets/identity_capture_view.dart` (`LiveCaptureView`) · `identity_fallback_view.dart` (`IdentityFallbackView`) · `identity_preview_view.dart` (`IdentityPreviewView`) |
| Pure logic | `features/myarea/data/liveness.dart` — `LivenessStep`, `livenessPromptDirection`, `livenessInvertYaw`, `livenessStepSatisfied`, `kSmileProbability = 0.7`, `kTurnYawDegrees = 20`, and the `CapturedSelfie` record. Re-exported from the screen so existing imports keep resolving. |
| Figma node | `758:4180` / `758:4248` / `758:4316` — full-bleed camera (D-611) |
| Shell | **Not** `SimfPageShell` — a bare `Scaffold` + `AppBar` with `SimfBackButton`, so the camera can run full-bleed behind a transparent chrome |
| API | **None.** The screen is on-device only (`camera` + `google_mlkit_face_detection`). It returns bytes to its caller, which uploads them via `POST /app/account/avatar` (`MyAreaEndpoints.avatar`). |
| Providers | None |
| Tests | `test/features/myarea/identity_verification_screen_test.dart` (8 — the only file exercising `livenessStepSatisfied` / `livenessInvertYaw`); goldens `test/golden/identity_verification_golden_test.dart` → `goldens/identity_capture.png`, `identity_capture_prompt.png`, `identity_fallback.png`. E2E [`mobile-identity-verification.md`](../../../tests/e2e/mobile-identity-verification.md) |
| Status | ✅ Real — built D-404; **clean-code decompose (D-610**, 489 → 304 + 3 files**)**; **full-bleed exact-Figma redesign (D-611)** — the owner chose full-bleed camera and removed the framed box, the prompt chrome and the progress bar; liveness gating unchanged |

## 1. Purpose

A guided liveness check that returns a **live** face photo. It is a capture
utility, not a page with data: it verifies a human is present, grabs one forward
selfie, and pops it back to whoever pushed it.

> **Contract (owner 2026-07-06, D-662).** Capture MUST verify a live human via the
> liveness challenge and MUST use only a live camera image. There is **no gallery
> path and no manual shutter**, so a static "studio" photo can never be submitted.
> Where the camera or ML Kit is unavailable the screen shows a "camera required"
> retry — never a gallery fallback.

## 2. Audience & access

Any signed-in account. It sits in `_authenticatedRoutes` rather than the attendee
role set on purpose: since D-666 a pending sign-up account presents as
`AppRole.guest`, so an attendee-gated route #103 bounced **every** sign-up user
home the moment they tapped "capture face photo" — sign-up was functionally
broken. The screen touches no network and `POST /app/account/avatar` is not
role-gated, so any signed-in account may safely reach it. The same fix cleared the
staff/moderator My-Area avatar-change dead-bounce.

## 3. Entry points and the return value

The screen pops a `CapturedSelfie` = `({Uint8List bytes, String filename})`.

| Caller | `extra` | What it does with the result |
|---|---|---|
| My Area (#14) `_changeAvatar` — `my_area_screen.dart:69` | `true` (show the confirmation preview) | `MyAreaRepository.uploadAvatar` → `POST /app/account/avatar` (multipart, jpeg/png/webp, server-gated to ≤ 2 MB), then bumps `avatarBustProvider` and invalidates the dashboard |
| Sign-up visitor (#7) `_pickFacePhoto` — `sign_up_visitor_screen.dart:240` | none (straight pop) | Holds the bytes in the draft (`_fields.faceImageBytes` / `faceImageName`) and uploads them with the profile save. Mandatory for men, optional for women. |

My Area reads the repository and the cache-bust **before** the async gap on
purpose: the guided capture takes several seconds, during which a token
proactive-refresh can churn go_router and dispose the My-Area `State`. Gating the
upload on `mounted` after the capture (as it once did) silently dropped the new
avatar — it never reached the server. Only the on-screen feedback stays
`mounted`-gated.

## 4. The liveness flow

Three steps — `LivenessStep.smile` · `turnRight` · `turnLeft` — **shuffled per
session** (D-422), so the sequence is not predictable and a pre-recorded clip is
harder to replay. The forward selfie is grabbed on the smile step wherever it
lands in the order.

1. `_initCamera` picks the **front** lens (falling back to the first camera),
   opens a `CameraController` at `ResolutionPreset.medium` with audio disabled and
   `nv21` on Android / `bgra8888` on iOS, builds a `FaceDetector` with
   classification enabled, and starts the image stream.
2. `_onFrame` is single-flighted by `_processing`. It converts the frame
   (`_toInputImage`), runs detection, takes the first face, and calls
   `livenessStepSatisfied` for the current step. Frames that fail the gate are
   simply ignored; a transient frame error is swallowed and streaming continues.
3. `_advance` grabs the forward selfie on the smile step (stop stream → take
   picture → restart stream), then either increments the step or calls `_finish`.
4. `_finish` returns the smile frame; if it was never grabbed it takes one final
   live shot (the human is already verified). With `showConfirmation` it shows
   `IdentityPreviewView` (حفظ / إعادة الالتقاط) instead of popping. With no bytes
   at all it falls through to the "camera required" state.

### The yaw-sign rule (do not "fix" this in the prompt)

ML Kit's `headEulerAngleY` arrives with a sign that depends on the platform **and**
the front camera's sensor orientation, because `_toInputImage` feeds a different
rotation per platform (raw sensor on iOS; device-orientation-compensated on
Android) and the front preview is mirrored.

`livenessInvertYaw(platform, frontCameraSensorOrientation)` normalises it so a
**positive yaw is always a physical RIGHT turn**: iOS never inverts; Android
inverts when the front sensor orientation is `>= 180` (270 on the RSNF tablet).
Liveness is portrait-only, so the sensor orientation alone decides it.

`livenessPromptDirection` is a **pure function of the step, with no platform
branch** — the prompt and the gold arrow always match the step name. Earlier
fixes (D-684, PR-103) compensated in the prompt instead, which mislabels the
step; that is what the split into `data/liveness.dart` pins against.

Thresholds: smile at `smilingProbability >= 0.7`; a turn at
`|normalised yaw| >= 20°`.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `SimfBackButton` | Pops with `null`; the caller treats that as "cancelled" |
| (implicit) step pass | `_onFrame` → `_advance` | Advances, or finishes on the last step |
| حفظ (preview) | `Navigator.pop<CapturedSelfie>` | Returns the bytes |
| إعادة الالتقاط (preview) | `_retake` | Clears the frame, resets the index, **re-shuffles** the sequence, restarts the camera |
| إعادة المحاولة (fallback) | `_retry` | Clears `_cameraFailed` and re-initialises — the path back after granting the camera permission |

## 6. States

| State | Render |
|---|---|
| Initialising | `LiveCaptureView(ready: false)` — chrome without a preview |
| Capturing | `LiveCaptureView` full-bleed: the live `CameraPreview`, the human-check label, the step prompt (😊 for the smile step, a gold `Icons.east` / `Icons.west` arrow for the turns), and `stepIndex` / `stepCount` |
| Camera unavailable | `IdentityFallbackView` — "camera required" + retry. Reached on web (`kIsWeb`, no live preview), on a camera/ML-Kit failure or denied permission, and when `_finish` ends with no bytes |
| Confirmation | `IdentityPreviewView` — only when the caller passed `extra: true` |

## 7. Resource lifecycle

`_stop` nulls the controller first, stops the stream if it is running, disposes the
controller and closes the detector, swallowing an already-disposed throw. It runs
from `_finish` and from `dispose`, so backing out mid-challenge releases the camera.

## 8. i18n / RTL

`AppL10n`: `identityVerificationTitle` (التحقق من الهوية) ·
`livenessHumanCheckTitle` · `livenessSmilePrompt` · `livenessTurnRightPrompt` ·
`livenessTurnLeftPrompt`, plus the fallback and preview strings on their widgets.
The AppBar is `centerTitle`; the prompt row mirrors with the app's directionality,
and the arrow glyph is chosen by physical direction, not by reading order — so it
does **not** flip in RTL.

## 9. Findings (recorded, not changed)

1. **Two raw sizes live in `tokens.dart` as screen-specific constants** —
   `identityVerificationScreenFontSize = 30` and
   `identityVerificationScreenSize = 32`. They are tokens by location rather than
   by meaning; a token named for one screen cannot be reused by another.
2. **`_advance`'s selfie grab swallows its failure silently.** That is deliberate
   (the flow keeps verifying liveness and `_finish` retakes), but it means a
   camera that can stream yet cannot `takePicture` walks the user through all
   three challenges before landing on "camera required".
