# Mohaned Review: open fix plans

Owner-raised review items. Each one is a self-contained fix plan: findings
verified against source first, then a recommendation. **No code is written for
any item until that item is approved.**

| # | Item | Raised | Status |
|---|------|--------|--------|
| 1 | Device-key label and device identity | 2026-08-13 | Waiting for owner approval |
| 2 | File pointers become real foreign keys to the central file table | 2026-08-13 | Waiting for owner approval |
| 3 | `DisplayName` duplicates the profile name, and the greeting rule is not built | 2026-08-13 | Waiting for owner approval |

---

# Item 1: device-key label and device identity

| Field | Value |
|-------|-------|
| Status | **Waiting for owner approval.** No code written. |
| Raised | 2026-08-13 |
| Trigger | Owner observation: "in app no any way to add label or modify" |
| Scope | Flutter app enrolment path only. Backend unchanged in the recommended option. |
| Related | D-172 (device keys), D-441 / D-445 (Face-ID nudge + toggle), D-738 (OS device-credential confirm), #7a (emailed step-up), D-110 (Identity freeze) |

---

## 1. What was asked

Implement **Option A** (derive a real device label at enrolment instead of the
hardcoded constant) **plus a device serial number**.

---

## 2. Findings, all verified against source

### 2.1 Every device key in production is named `SIMF mobile`

`AuthController.enrolDeviceKey` declares a default and no caller ever overrides it:

```dart
// packages/simf_auth_pkg/lib/src/application/auth_controller.dart:341-353
Future<void> enrolDeviceKey({
  String label = 'SIMF mobile',
  String? stepUpCode,
}) async {
```

The single call site passes the step-up code only
(`biometric_step_up_screen.dart:159-161`), so the default is always taken.
The server insists on a non-empty label
(`DeviceKeyService.cs:76`, `label.Length is < 1 or > 64`), which is why a
constant had to exist at all.

**Consequence:** a user who enrols on a phone and a tablet gets two rows both
reading `SIMF mobile`. Every reinstall-then-re-enrol adds another identical
orphan row. Nobody can tell which row is which device, including an
administrator calling `DELETE /admin/device-keys/{id}`.

### 2.2 There is no rename path anywhere

`DeviceKeyEndpoints.cs` exposes exactly seven operations: register, step-up,
list, challenge, sign-in, self-revoke, admin-revoke. There is **no `PATCH` or
`PUT`**. The label is write-once at enrolment on the server as well as in the app.

### 2.3 The list endpoint has no consumer

`GET /app/auth/device-keys` (`ListMyDeviceKeysEndpoint` plus
`DeviceKeyService.ListMineAsync` plus the `DeviceKeyEntry.Label` field) is built
and unwired:

- `packages/simf_auth_pkg/lib/src/data/auth_api.dart` implements register,
  step-up, challenge, sign-in and revoke. There is no list method.
- `src/ControlPanel/` contains zero matches for `DeviceKey`.
- `src/Shared/SIMF.ApiClient/` contains zero matches for `DeviceKey`.

The intent is recorded on the entity itself (`DeviceKey.cs:46-49`): the label
exists "so the owner can tell one row from another in the revoke list". That
revoke list was never built.

### 2.4 A hardware serial number is not obtainable on either platform

This is the constraint that reshapes the request.

| Platform | Reality | Source |
|----------|---------|--------|
| Android | `device_info_plus` exposes a serial, but its README states the app must meet Android's official requirements or the plugin returns the literal string `unknown`. A normal store app does not meet them. | pub.dev README, device_info_plus 13.2.0 |
| iOS | `IosDeviceInfo` has **no** `serialNumber` property. Apple exposes no device serial to third-party apps. The nearest value is `identifierForVendor`, a UUID that resets once every app from the vendor is uninstalled. | `IosDeviceInfo` class documentation |

`AndroidDeviceInfo` also has **no unique per-device identifier** among its
public fields: `id` is a build changelist, `fingerprint` is a build fingerprint
shared by every device of that model and build, and `androidId` is not part of
this package.

**Therefore:** "device serial number" is delivered below as a **device
fingerprint** that uses the real OS serial on the platforms and deployments that
grant it, and a stable substitute everywhere else. This is stated as an explicit
assumption, not a silent narrowing.

### 2.5 The label already reaches the audit trail

`DeviceKeyService.cs:137` writes it into the audit detail:

```csharp
Detail = $"deviceKeyId={deviceKey.Id}; label={label}",
```

So a real label improves the `DeviceKeyRegistered` audit row **immediately**,
with no new UI. That is the payoff that makes this worth doing before the
"my devices" screen exists.

---

## 3. The decision that shapes the build

**Where does the device fingerprint live?**

### Option 1 (recommended): pack it into the existing `Label`

Format: `{device name} · {fingerprint, first 8 hex}`, for example
`Galaxy S23 · 3e4f5a6b` or `iPhone 15 Pro · 91b2c07d`. Truncated client-side to
the 64-character column budget.

- No schema change, no migration, no D-110 Identity freeze lift.
- No backend change of any kind. The server already accepts and stores it.
- Lands in the audit trail on day one (see 2.5).
- Minimal personal-data footprint: 8 hex characters is ample to tell a handful
  of rows apart and is not a reversible hardware identifier.
- Accepted cost: the value is free text, so it is not queryable or indexable.

### Option 2: add a `DeviceFingerprint` column to `DeviceKeys`

- Queryable and indexable. Correct if the fingerprint ever becomes a **security
  control** (detecting the same physical device across accounts, blocking cloned
  installs) rather than a display aid.
- Requires an **explicit owner lift of the D-110 Identity freeze**. Every lift
  granted so far (D-186, D-199, D-211, D-217, D-219) states that the Identity
  schema stays frozen, and `DeviceKey` lives on `SimfIdentityDbContext`.
- Storing a full persistent hardware identifier server-side raises a PDPL and
  NCA question that Option 1 sidesteps.
- Global rule §20 requires looking for an indirect solution before asking to
  modify frozen code. Option 1 **is** that indirect solution, so Option 2 has to
  be justified by a requirement Option 1 cannot meet.

**Recommendation: Option 1.** The stated purpose is telling device rows apart,
which is a display and audit concern, not a security control. Option 1 delivers
it with zero backend risk and zero freeze exposure. If the fingerprint later
needs to be a security control, Option 2 becomes the right change and can be
argued for then on its own merits, with the PDPL question answered.

The rest of this plan assumes Option 1.

---

## 4. Design

### 4.1 Where the code goes

`device_info_plus` is a platform plugin. It stays **out of `simf_auth_pkg`**,
which is auth and transport logic. The label is resolved in the app layer and
passed into `enrolDeviceKey(label: ...)`, whose parameter already exists and is
currently never supplied. This keeps the package boundary that
`simf_app/CLAUDE.md` §0 protects and means `simf_auth_pkg` gains no dependency.

### 4.2 Resolution order for the device name

| Platform | Value |
|----------|-------|
| Android | `{manufacturer} {model}`, for example `samsung SM-S911B` |
| iOS | `modelName`, falling back to `utsname.machine` |
| Anything else or a plugin failure | `SIMF mobile`, today's constant |

The fallback matters: a plugin exception must never block enrolment, because
enrolment is already gated behind an emailed code and an OS confirmation that
the user has just completed. Losing that work to a device-name lookup would be a
worse defect than the one being fixed.

### 4.3 Resolution order for the fingerprint

1. The OS serial where the platform grants it (Android deployments meeting the
   official requirements). Used as-is when it is a real value.
2. iOS `identifierForVendor`.
3. A UUID generated once on first enrolment and persisted in secure storage
   under a new `StorageKeys` entry, reused on every later enrolment from that
   install.

In all three cases the value is hashed and the **first 8 hex characters** are
what reach the label. Step 3 guarantees the feature works identically on every
device rather than degrading to the literal string `unknown`, which is what a
naive serial read would store on most Android handsets.

### 4.4 Length safety

The name is truncated so that `name + separator + 8 chars` never exceeds 64. The
server returns a 400 (`DEVICE_KEY_INVALID`) above that, so the guard is
mandatory, not defensive.

### 4.5 Character safety (added after the security review, see S6)

The label is interpolated into the audit detail as `key=value; key=value`
(`DeviceKeyService.cs:137`). A device name is manufacturer-controlled on Android
and user-settable on some platforms, so it is untrusted input. `device_label.dart`
strips `;`, `=`, CR and LF and any C0 control character before building the label.

**Client-side stripping is not by itself a security control**, because the
endpoint already accepts any 64-character string from any caller holding a token,
and did so before this plan existed. The matching server-side rejection is
therefore listed as the fix for **S6** and needs its own approval, so that this
plan keeps its zero-backend-change property. What belongs here is only the
guarantee that the value **this app** sends is clean.

---

## 5. Files

| File | Change | Risk |
|------|--------|------|
| `src/Mobile/simf_app/pubspec.yaml` | Add `device_info_plus: ^13.2.0` | **breaking** (new platform dependency, needs owner sign-off per global §14) |
| `src/Mobile/simf_app/lib/features/account/device_label.dart` | **New.** Resolves name plus fingerprint, owns truncation and every fallback | none |
| `src/Mobile/simf_app/lib/features/account/biometric_step_up_screen.dart` | Pass the resolved label into `enrolDeviceKey` | none |
| `src/Mobile/simf_app/packages/simf_data_pkg/lib/src/storage/storage_keys.dart` | Add the fingerprint storage key | none |
| `src/Mobile/simf_app/test/features/account/device_label_test.dart` | **New.** Unit tests, see §6 | none |
| `src/Mobile/simf_app/test/features/account/biometric_step_up_screen_test.dart` | Extend the fake to record the label; assert it is not the constant | none |
| `docs/tests/e2e/mobile-biometric-step-up.md` | Add scenarios `E2E-MBSU-016..018` | none |
| `docs/tests/e2e/README.md` | Re-derive **Total scenarios** from 3068 to 3071. Pages catalogued stays 193 (no new file) | none |
| `docs/pages/mobile/biometric-step-up/README.md` | Document what the label now contains and why | none |
| `docs/decisions/DECISIONS_LOG.md` | One `D-NEXT` entry, see §8 | none |

**No backend file changes.** `RegisterDeviceKeyRequest.Label` is already on the
wire and already validated; only the value the client sends changes, so the
D-219 append-only wire contract is untouched.

---

## 6. Tests

Unit, `device_label_test.dart`:

1. Android info produces `{manufacturer} {model}` plus an 8-character suffix.
2. iOS info produces `modelName` plus an 8-character suffix.
3. A plugin throw falls back to `SIMF mobile` and still yields a valid label.
4. A very long manufacturer plus model is truncated to 64 characters or fewer.
5. The generated fingerprint is persisted on first call and **reused** on the
   second, rather than regenerated.
6. A serial reading `unknown` is rejected and falls through to the next source.

Widget, `biometric_step_up_screen_test.dart`:

7. A successful enrol sends a label that is not the literal `SIMF mobile`.

Existing tests reviewed: `auth_controller_device_key_test.dart` stubs and
verifies with `any(named: 'label')` at lines 95 and 118, so it does **not** pin
the constant and needs no change. Confirmed by reading it, not assumed.

---

## 7. E2E scenarios to author

Namespace `MBSU` is in use with ids 001 to 015 plus `ELS-001/002`, so the next
free ids are:

| Id | Scenario |
|----|----------|
| E2E-MBSU-016 | Enrol on a physical device, then read the row: `Label` holds the real device name and an 8-character suffix, not `SIMF mobile` |
| E2E-MBSU-017 | Enrol on two different devices under one account: both rows are distinguishable by label |
| E2E-MBSU-018 | Re-enrol on the same device after disabling: the fingerprint suffix is unchanged, proving it is stable per install |

---

## 8. Decisions log entry to add

> **D-NEXT | 2026-08-13 | Device keys carry a real device label plus a stable
> per-install fingerprint, packed into the existing `Label` column.**
> `AuthController.enrolDeviceKey` defaulted `label` to the constant
> `'SIMF mobile'` and its only caller never overrode it, so every row in
> `DeviceKeys` was identically named and no operator could tell one enrolled
> device from another, including in the `DeviceKeyRegistered` audit detail which
> interpolates the label. A literal hardware serial was requested but is not
> obtainable: `device_info_plus` returns `unknown` on Android unless the app
> meets Android's privileged requirements, and `IosDeviceInfo` exposes no serial
> at all. **Fix:** the app layer resolves `{manufacturer} {model}` on Android or
> `modelName` on iOS, plus an 8-character fingerprint taken from the OS serial
> where granted, else `identifierForVendor`, else a UUID minted once into secure
> storage, and passes the pair as the existing `label` parameter. **No schema
> change and no D-110 Identity freeze lift**, per global §20's indirect-solution
> rule: the `Label` column is already `nvarchar(64)`, already validated 1 to 64,
> and already reaches the audit trail. A queryable `DeviceFingerprint` column
> was considered and rejected for now because the purpose is display and audit,
> not a security control; it would need an Identity freeze lift and a PDPL answer
> on storing a persistent hardware identifier. `device_info_plus` is a new
> plugin dependency, kept out of `simf_auth_pkg` so the auth package stays free
> of platform plugins. The label remains invisible to end users until the unwired
> `GET /app/auth/device-keys` gets a "my devices" screen, tracked separately.

---

## 9. Verification gate before this is called done

Per global §17 and the project delivery gate:

1. `flutter test` from `simf_app`, green, output pasted.
2. `flutter analyze` on the touched files, zero new issues.
3. Live on-device enrol on the tablet, then read the resulting `DeviceKeys` row
   and the `DeviceKeyRegistered` audit row, and show both.
4. Scenarios `E2E-MBSU-016..018` driven on the device.
5. Review agents plus `simplify`.
6. Docs in the same changeset.

Item 3 is the one that actually proves this, because it is the only step that
shows a real device name coming from real hardware rather than a test fake.

---

## 10. Not touching

Backend endpoints and services, the `DeviceKeys` schema, the D-219 wire
contract, the enrolment security ceremony (emailed step-up plus OS
device-credential confirm), the Face-ID toggle and nudge behaviour, and the
sign-in paths.

---

## 11. Open questions for the owner

1. **Approve the new `device_info_plus` dependency?** Global §14 requires a
   confirmation before installing a package. Without it the plan degrades to
   `dart:io`, which yields only "Android 14" or "iOS 17.4" and cannot tell two
   Android phones apart. That would leave most of the original problem in place.
2. **Option 1 or Option 2?** Recommended: Option 1, for the reasons in §3.
   Option 2 needs an explicit D-110 Identity freeze lift.
3. **Is the fingerprint ever meant to be a security control** (recognising the
   same physical device across accounts), or only a way to tell rows apart? A
   "yes" moves the recommendation to Option 2 and changes the PDPL position.
4. **Should the "my devices" screen be scheduled now?** The label is invisible to
   end users until `GET /app/auth/device-keys` is wired to a screen. It has no
   Figma node, and `simf_app/CLAUDE.md` §13.5 requires asking rather than
   inventing one. Recommended: keep it out of this change, schedule separately.
5. **S1, S2 and S3 below are separate from this plan and need their own
   approval.** They are pre-existing defects in shipped code, not regressions
   introduced here. Recommended: fix S1 and S2 before the production publish.

---

## 12. Security review of the device-key subsystem

Requested 2026-08-13. Scope: `DeviceKey.cs`, `DeviceKeyService.cs`,
`DeviceKeyEndpoints.cs`, `DeviceKeyConfiguration.cs`, `DeviceKeys.cs` contracts,
`TokenIssuer.cs`, the device-key portion of `auth_controller.dart`,
`secure_storage.dart`, and the interaction with `SignInService.cs` and
`PasswordService.cs`. Method: source reading. **No tests were executed and no
runtime exploitation was attempted**, so every item below is a source-verified
finding with the evidence cited, not a demonstrated exploit.

### What is done well, so the findings are read in context

Replay protection is genuinely solid: the challenge is consumed by an atomic
conditional `ExecuteUpdateAsync` that matches only the row still holding that
exact challenge, so a concurrent replay clears nothing and is rejected before any
token mint (`DeviceKeyService.cs:308-324`). The step-up code is keyed-hashed,
never persisted in plaintext, compared in constant time
(`CryptographicOperations.FixedTimeEquals`), capped at 5 issues per hour and 5
attempts per code. The signature verification is a correct ES256 check with the
IEEE-P1363 format. The two anonymous endpoints are pinned with per-entry
justifications in `BusinessFlow13PermissionMatrixTests.cs:115-118`, so an 18th
anonymous endpoint breaks the build. The sign-in endpoint deliberately collapses
every failure into one 401 so it leaks no step detail.

### Findings

| Id | Severity | Finding |
|----|----------|---------|
| S1 | **High** | Biometric sign-in bypasses the forced-password-change and max-password-age gate (NCA control) |
| S2 | **High** | A password change or reset does not revoke device keys, so the standard compromise remedy does not evict an attacker |
| S3 | **Medium-High** | An administrator can hold a permanent 2FA-free admin-permissioned session through a device key |
| S4 | Medium | Account lockout and the non-Approved account states are not honoured on the device-key path |
| S5 | Medium | No cap on device keys per account and no lifecycle revocation anywhere |
| S6 | Medium | Audit-detail injection through the user-controlled label |
| S7 | Medium (DoD) | The admin revoke endpoint is the only endpoint in the tree outside the permission catalogue |
| S8 | Low | Anonymous challenge issuance is an existence oracle and an unauthenticated-write vector |
| S9 | Low (documented) | The private key is software-bound, not hardware-bound |
| S10 | Low (DoD) | The list endpoint is shipped with no consumer, no page doc and no E2E scenario |

---

#### S1 (High). Biometric sign-in bypasses the forced-password-change gate

**Evidence.** `SignInService.cs:140-147` sets `PasswordChangeRequired` once the
password ages past `IdentityLifecycle:PasswordMaxAgeDays`, under a comment that
names it an NCA control. `SignInService.cs:149-160` then refuses an app-audience
sign-in with a 403 `AUTH_PASSWORD_CHANGE_REQUIRED`. The comment at
`SignInService.cs:130-133` enumerates every later mint path that re-checks the
flag through `RequirePasswordChangeNotRequired`, and that helper is called at
lines 313, 373, 490 and 825.

`DeviceKeyService.SignInWithDeviceKeyAsync` (lines 264-336) calls none of them.
Its only account check is `user.AccountState == AccountState.Disabled` at line
327, then it mints at line 335.

**Impact.** Any user who has ever enabled Face ID keeps signing in indefinitely
after their password expires, and never changes it. The maximum-password-age
control is unenforceable for that population. The enumeration in the comment at
line 131 omits the device-key path, which suggests the omission was an oversight
rather than a decision.

**Fix.** Check the flag in the device-key mint and answer 401 with the typed
code. One guard, in one method.

---

#### S2 (High). A password reset does not revoke device keys

**Evidence.** `PasswordService.ClearChangeFlagAndEndSessionsAsync`
(lines 421-434) is the single point every change, reset and forced-complete path
passes through. It clears the flag, stamps `PasswordChangedAt`, and calls
`refreshTokenRepository.RevokeAllForUserAsync`. That is all it revokes.
`PasswordService.cs` contains **zero** references to `DeviceKey`.
`ResetTwoFactorEndpoint.cs` likewise contains zero. The only writes to the
`DeviceKeys` table anywhere in the solution are inside `DeviceKeyService`.

**Impact.** Consider an account compromised while a session is live. The attacker
enrols a device key (which does require the emailed step-up, so they need mailbox
access or a live session on an unlocked device). The victim then does the one
thing every security notice tells them to do and resets their password. Every
refresh token is revoked. The device key is not, and
`POST /app/auth/sign-in-with-device-key` is `AllowAnonymous`, so the attacker
mints a brand-new full session from it. The remedy does not remedy.

The doc comment on the method reads "ends every session", which is no longer
accurate.

**Fix.** Revoke the user's device keys in that same method, and dispatch the
existing notification pattern so the owner sees it happen.

---

#### S3 (Medium-High). An administrator can hold a 2FA-free session

**Evidence.** `RegisterDeviceKeyEndpoint` (`DeviceKeyEndpoints.cs:17-23`) gates
on `RequireApprovedAccount` alone. There is no user-type check, no audience
check, and nothing that excludes an administrator. `TokenIssuer.IssueAsync`
(lines 33-37) resolves roles and permissions for whichever user it is handed and
stamps them into the access token; it takes no audience parameter, so there is
one token shape. The device-key mint passes `secondFactorCompleted: null`
(`DeviceKeyService.cs:417`).

**Impact.** This works against the enrolment-first decision recorded at
`SignInService.cs:188-194`, whose stated purpose is that the Control Panel "must
never mint a session on the password alone". A device key mints an
admin-permissioned bearer token with no second factor at all, and that bearer is
accepted by the `/admin/*` endpoints.

**Qualification, stated because it changes the priority.** This requires an
administrator to enrol biometrics on the mobile app, and the Control Panel UI
itself authenticates by cookie through the BFF rather than by this bearer. So
this is a policy gap and a defence-in-depth failure rather than a demonstrated
live path. It should still be closed, because nothing in the code prevents it and
nothing warns an administrator that they are doing it.

**Fix.** Refuse enrolment for accounts that resolve any admin permission, or
restrict the register endpoint to the app audience.

---

#### S4 (Medium). Lockout and account states are not honoured

**Evidence.** The device-key mint checks only `Disabled`
(`DeviceKeyService.cs:327`). The password path additionally blocks `Registered`
via `CheckAccountState` (lines 655-668) and enforces lockout via
`EnsureNotLockedOutAsync` (line 721).

**Impact, and why it is Medium rather than High.** `JwtTokenService.cs:47` stamps
`account_state` into **every** token, including the device-key one, since all
paths share `TokenIssuer`. `SimfUser.cs:21-23` records the design intent
directly: "PendingApproval and Rejected do sign in and are routed to their own
screens by the account_state claim, so this is not on its own an access
decision", and protected endpoints stack `RequireApprovedAccount`, documented as
`RequireClaim("account_state", "Approved")` in
`docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md:297`. So a `Rejected` or
`PendingApproval` holder gets a token with very little reach, and that
containment is by design rather than by luck.

Lockout is the part that is genuinely not enforced on this path, so lockout is
not a reliable "freeze this account now" control while any device key exists.

---

#### S5 (Medium). No cap and no lifecycle revocation

**Evidence.** `RegisterAsync` performs no count query (verified: no `Count` call
exists in the file). Nothing outside the two revoke endpoints ever sets
`RevokedAt`.

**Impact.** An account can accumulate unbounded permanent alternative
credentials, each an independent persistence foothold, and neither the user nor
an administrator has any surface on which to see them (see S10). Combined with
S2, a key enrolled once survives every remediation short of someone manually
issuing `DELETE /admin/device-keys/{id}` for an id they have no way to look up.

**Fix.** Cap active keys per account, revoke the oldest on overflow, and notify
the account owner on every enrolment.

---

#### S6 (Medium). Audit-detail injection through the label

**Evidence.** `DeviceKeyService.cs:137` builds
`Detail = $"deviceKeyId={deviceKey.Id}; label={label}"`. The label is
user-supplied and validated for length only (1 to 64); `DeviceKey.cs:48` states
"the contents are never interpreted".

**Impact.** A label containing `;` or `=` or a newline forges fields inside a
`key=value; key=value` audit record. Audit-trail integrity is an NCA concern, and
the record is the only evidence of who enrolled what. It is also a stored-XSS
candidate anywhere the detail is rendered into the Control Panel.

**Partly in scope for the present plan**, because the plan changes what fills the
label. §4.5 strips the separator and control-character set client-side, which is
all this plan can do without a backend change. **Fix for S6 proper:** reject the
same character set in `RegisterAsync`, or encode the label when composing the
audit detail. That is a backend change and needs its own approval, since the hole
predates this plan and the `SIMF mobile` default never closed it.

---

#### S7 (Medium, Definition of Done). The admin revoke sits outside the catalogue

**Evidence.** `AuthorizationPolicies.AdministratorOnly` appears exactly **once**
in the entire `src/Backend/SIMF.Api/Endpoints` tree, at
`DeviceKeyEndpoints.cs:165`. No other endpoint uses that legacy policy. The
project CLAUDE.md D-207 / D-208 hard rule requires every admin API action to be
gated by `Policies(PermissionCatalog.PolicyFor(...))`.

**Impact.** The endpoint **is** gated, so this is not an open door. But device-key
revocation cannot be delegated to a non-Administrator role, it does not appear in
`PermissionCatalog` or the permission catalogue document, and it is invisible to
the permission matrix the project uses to reason about admin surface.

---

#### S8 (Low). The anonymous challenge endpoint

**Evidence.** `IssueDeviceKeyChallengeEndpoint` is `AllowAnonymous` and returns
404 `DEVICE_KEY_NOT_FOUND`, 401 `DEVICE_KEY_REVOKED` or 200 depending on state,
whereas the sign-in endpoint deliberately collapses everything into one 401. Each
call writes to the database (`CurrentChallenge` and `ChallengeExpiresAt`), and
issuing overwrites any challenge already in flight.

**Impact.** Anyone holding a device-key id can invalidate a legitimate in-flight
sign-in by requesting a new challenge, and can drive unauthenticated database
writes. Severity stays Low because the ids are 128-bit GUIDs, so enumeration is
impractical, and the `auth` per-IP rate limiter caps the volume. Noted for
completeness and because the endpoint comment asserts only that a leaked id "does
not enable sign-in", which is true but is not the whole risk.

---

#### S9 (Low, already documented). Software-bound private key

`DeviceKey.cs:12-17` states plainly that the private key is software-bound and
that the biometric prompt gates the code path reaching the key, not the key
material, with hardware binding listed as planned hardening. Client storage is
`flutter_secure_storage` with `encryptedSharedPreferences: true`
(`secure_storage.dart:21-23`), which is the right baseline.

Listed so the position is explicit rather than forgotten: on a rooted or
jailbroken device the key can be extracted and then used from anywhere with **no**
biometric involved. Closing it means Android Keystore or StrongBox with
`setUserAuthenticationRequired`, or the iOS Secure Enclave. The entity comment
notes this needs no server contract change.

---

#### S10 (Low, Definition of Done). The list endpoint is undocumented and untested

`GET /app/auth/device-keys` has no client consumer, no `PAGE-INDEX.md` row and no
E2E scenario. D-246 requires docs plus unit and integration tests plus an E2E
catalogue entry in the same changeset. This is the same gap that produced the
original label complaint.

---

### Compliance summary

| Standard | Status |
|----------|--------|
| NCA, maximum password age | **Fails** while any device key exists (S1) |
| NCA, credential revocation on compromise | **Fails** (S2) |
| NCA, multi-factor for privileged access | **Gap** (S3) |
| NCA, audit-trail integrity | **Gap** (S6) |
| NCA, session cap and claim parity across entry points | Passes, enforced by `TokenIssuer` and `TokenIssuerParityTests` |
| Anonymous-surface rule (project CLAUDE.md §4) | Passes, pinned by test with justifications |
| D-207 / D-208 per-action permissions | **Fails** for the admin revoke (S7) |
| D-157 data and identity separation | Passes, `DeviceKey` is Identity-only with a real in-database FK |
| D-246 docs plus tests plus E2E | **Fails** for the list endpoint (S10) |
| D-110 Identity freeze | Respected by the recommended option |

### Recommended sequencing

S1 and S2 are small, self-contained and independent of everything in this plan.
S1 is one guard in one method. S2 is one repository call in one method. Both are
worth doing before the production publish and neither needs a schema change.
S3 through S5 are a second, larger piece of work that belongs with the "my
devices" screen, since that screen is also what makes S5 observable. S6 rides
along with this plan. S7 and S10 are Definition-of-Done cleanups.
