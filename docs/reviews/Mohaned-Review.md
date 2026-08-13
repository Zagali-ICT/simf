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

> **Issued as a standalone report:**
> [`docs/security/SIMF-Security-Review-DeviceKeys-2026-08-13.md`](../security/SIMF-Security-Review-DeviceKeys-2026-08-13.md).
> That file is the canonical issue and carries the executive summary, the full
> evidence and the compliance table. This section is the inline copy kept with
> the plan; **§13 below is the remediation plan**, which the report points back to.

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

---

## 13. Remediation plan: every finding as a work item

Each finding above becomes one numbered work item. **Nothing here is
implemented.** Each wave needs its own approval, separately from the label work
in §1 to §11, because these are pre-existing defects in shipped code rather than
anything this plan introduces.

| Wave | Items | Findings | Gate |
|------|-------|----------|------|
| **A** | W1, W2, W4 | S1, S2, S4 | Before the production publish |
| **B** | W3, W6, W7 | S3, S6, S7 | One backend changeset, after A |
| **C** | W5, W8, W10 | S5, S8, S10 | Needs the "my devices" surface |
| **D** | W9 | S9 | Its own piece of work, no server change |

W4 is pulled into wave A because it edits the same guard block as W1, so
splitting them would mean touching one method twice.

---

### W1 (S1). Refuse a device-key sign-in when a password change is required

| Field | Value |
|-------|-------|
| Files | `SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs` (**security**), `SIMF.Api.Tests/DeviceKeySignInTests.cs` (none) |
| Size | One guard in one method |

**Approach.** In `SignInWithDeviceKeyAsync`, immediately after the existing
`accounts.FindByIdAsync` at line 326, refuse when `user.PasswordChangeRequired`
is set. Audit the refusal with the existing `AuditFailureAsync` helper.

**Decision needed: opaque 401 or typed response.** Returning `null` maps to the
generic `DEVICE_KEY_SIGNATURE_INVALID` 401, which tells a legitimate user with an
expired password nothing about what to do, and the app cannot route them to the
change flow. **Recommended: a typed `AUTH_PASSWORD_CHANGE_REQUIRED` 403**,
matching what the password path returns for the app audience. There is no
account-existence oracle risk here, because reaching this line already required
a valid signature from the private key.

**Tests.** A user with `PasswordChangeRequired` and a valid key is refused; a
normal user still succeeds; the refusal is audited.

---

### W2 (S2). Revoke device keys on every password change and reset

| Field | Value |
|-------|-------|
| Files | `SIMF.Application/IdentityAccess/PasswordService.cs` (**security**), `SIMF.Application/IdentityAccess/Abstractions/IDeviceKeyService.cs` (none), `SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs` (none), tests (none) |
| Size | One new method plus one call |

**Approach.** Add `RevokeAllForUserAsync(Guid userId, ...)` to `IDeviceKeyService`
and implement it as a single `ExecuteUpdateAsync` setting `RevokedAt` and
clearing the challenge columns for every non-revoked key. Call it from
`ClearChangeFlagAndEndSessionsAsync` beside the existing
`refreshTokenRepository.RevokeAllForUserAsync`. Correct that method's XML doc,
which currently claims it "ends every session".

**Why the abstraction rather than a direct DbContext call.** `PasswordService`
lives in Application and must not reach into Infrastructure persistence.
`IDeviceKeyService` is already the Application-side abstraction, so this adds no
new layer.

**Decision needed: which paths revoke.** Options are reset and forced-change
only, or every change including a voluntary one. **Recommended: every change.** A
voluntary password change is frequently the user's own response to "I think
someone has access to my account", and that is precisely the case S2 describes.

**Follow-on.** Extend the existing `AccountPasswordChanged` notification body to
say how many devices were signed out, so the action is visible rather than
silent.

**Tests.** Reset the password, then a previously valid device key is refused at
`sign-in-with-device-key`. Same for the forced-change completion path and the
authenticated change path.

---

### W3 (S3). Refuse device-key enrolment for administrator accounts

| Field | Value |
|-------|-------|
| Files | `SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs` (**security**), tests (none) |
| Size | One guard in `RegisterAsync` |

**Approach.** Refuse enrolment in `RegisterAsync` before any key is persisted,
with a typed 403.

**Decision needed: block on what.** Blocking on `UserType.Admin` is simple and
uses the enum whose documented job is deciding the sign-in surface
(`SimfUser.cs:26`). Blocking on the resolved permission set is more precise but
means resolving permissions on an enrolment call that does not otherwise need
them. **Recommended: `UserType.Admin`**, because it is the existing surface
decision and needs no new resolution.

**Tests.** An admin account is refused at enrolment; a visitor account is not.

---

### W4 (S4). Honour lockout and the `Registered` state

| Field | Value |
|-------|-------|
| Files | `SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs` (**security**), tests (none) |
| Size | Two conditions in the guard block W1 creates |

**Approach.** In the same guard block, call the existing
`accounts.IsLockedOutAsync(user)` and refuse when locked, and refuse
`AccountState.Registered` for parity with `CheckAccountState`. Leave
`PendingApproval` and `Rejected` alone: `SimfUser.cs:21-23` documents that these
deliberately do sign in and are contained by the `account_state` claim plus
`RequireApprovedAccount`, so refusing them here would diverge from the password
path rather than align with it.

**Tests.** A locked-out account with a valid key is refused; the lockout expiring
restores it.

---

### W5 (S5). Cap active keys per account and notify on enrolment

| Field | Value |
|-------|-------|
| Files | `DeviceKeyService.cs` (**security**), `SIMF.Common/Options/DeviceKeyOptions.cs` (none), `SIMF.Api/appsettings.json` plus the environment override (none), `SIMF.Common/Enums/NotificationKind.cs` (none, additive value only), tests (none) |
| Size | Medium |

**Approach.** Add `MaxActiveKeysPerUser` to `DeviceKeyOptions`, default 5. In
`RegisterAsync`, count the caller's non-revoked keys and revoke the oldest by
`CreatedAt` when the new one would exceed the cap. Dispatch a notification on
every enrolment so a silent one is visible to the account owner.

**Freeze note.** The notification needs a new `NotificationKind` value. D-110
permits **additive** enum values that shadow no existing name or integer, which is
the same allowance used by D-111 and D-217, so this needs no freeze lift and no
migration. Per global §18, the new options key is added to `appsettings.json`
**and** the environment override together so the two cannot drift.

**Tests.** The sixth enrolment revokes the first; the notification is dispatched;
the cap is configurable.

---

### W6 (S6). Reject separator and control characters in the label

| Field | Value |
|-------|-------|
| Files | `DeviceKeyService.cs` (**security**), tests (none) |
| Size | One validation clause, plus one encode at the sink |

**Approach.** Two layers, because they fail differently. Reject `;`, `=`, CR, LF
and C0 control characters in `RegisterAsync` alongside the existing length check,
returning `DEVICE_KEY_INVALID`. Separately, encode the label where the audit
detail is composed at line 137, so the sink is safe regardless of what any future
caller sends.

**Relationship to the label work.** §4.5 of this plan strips the same set
client-side. That is hygiene for the value this app sends and is **not** the
control; W6 is the control, because any token holder can post any 64-character
string straight to the endpoint today.

**Tests.** A label containing `;` or a newline is rejected; the audit detail
remains parseable.

---

### W7 (S7). Bring the admin revoke into the permission catalogue

| Field | Value |
|-------|-------|
| Files | `SIMF.Common/PermissionCatalog.cs` (none), `SIMF.Api/Endpoints/Auth/DeviceKeyEndpoints.cs` (**breaking**), `SIMF.Api.Tests/PermissionEnforcementTests.cs` (none), `docs/SIMF-Permission-Catalogue.md` (none) |
| Size | Small, but it changes an authorization gate |

**Approach.** Follow the project CLAUDE.md five-step playbook: add the code to the
right nested class in `PermissionCatalog`, add the `new(...)` entry to
`PermissionCatalog.All` with `BaselineRoles = AdminOnly`, then swap
`nameof(AuthorizationPolicies.AdministratorOnly)` for
`PermissionCatalog.PolicyFor(...)` on `AdminRevokeDeviceKeyEndpoint`. The seeder
is idempotent and the `Permission` and `RolePermission` tables pre-exist, so
there is **no migration**.

**Risk tagged breaking, and why it is contained.** This changes what an existing
role may do. `Administrator` is the `"*"` wildcard, so a super-admin keeps access
with no action. Any narrower role that somehow held this ability would lose it
until the permission is granted.

**Tests.** `PermissionEnforcementTests` already fails the build on an ungated
admin endpoint; add the positive and negative cases for the new code.

---

### W8 (S8). Collapse the challenge-endpoint responses

| Field | Value |
|-------|-------|
| Files | `DeviceKeyService.cs` (none), `DeviceKeyEndpoints.cs` (**breaking**), tests (none) |
| Size | Small |

**Approach.** Return one 401 for both not-found and revoked, matching how
`sign-in-with-device-key` already collapses its failures.

**Verify before changing.** The Flutter error mapping in
`auth_repository_impl.dart` and `device_key_client.dart` may distinguish these two
codes to decide whether to clear the local key. Read that path first: if the app
clears its stored key on `DEVICE_KEY_NOT_FOUND`, collapsing the codes would strand
a user with a dead key and no automatic recovery. That check decides whether W8
is worth its cost at Low severity.

---

### W9 (S9). Hardware-bind the private key

| Field | Value |
|-------|-------|
| Files | Flutter `device_key_client.dart` plus new platform channels (**breaking**), `DeviceKey.cs` doc comment (none) |
| Size | Large |

**Approach.** Generate and hold the key in the Android Keystore or StrongBox with
`setUserAuthenticationRequired`, and in the iOS Secure Enclave, so the biometric
gates the key material rather than the code path that reaches it. The server
contract is unchanged: still a SubjectPublicKeyInfo in and an ES256 verify, as
`DeviceKey.cs:12-17` already notes.

**Rollout decision needed.** Existing software-held keys cannot be migrated into
hardware. Every enrolled user must re-enrol, which means a forced revocation plus
a prompt. That is a user-visible event and needs its own owner decision on timing.

---

### W10 (S10). Wire the list endpoint and complete its Definition of Done

| Field | Value |
|-------|-------|
| Files | `auth_api.dart`, `auth_repository_impl.dart`, `auth_controller.dart` (none), a new "my devices" screen (none), `docs/pages/PAGE-INDEX.md`, the per-page doc, `docs/tests/e2e/` catalogue plus README totals (none) |
| Size | Medium, screen-sized |

**Approach.** Add the missing list call, then a screen showing label, created and
last-used per row with a per-row revoke. This is what makes the label from §1 to
§11 visible to a user, and what makes W5's cap observable.

**Blocked on a decision.** The screen has no Figma node, and `simf_app/CLAUDE.md`
§13.5 requires asking rather than inventing one. This is the same item as open
question 4 in §11.

---

### Summary of decisions this remediation needs

| # | Decision | Recommendation |
|---|----------|----------------|
| 1 | W1 returns an opaque 401 or a typed `AUTH_PASSWORD_CHANGE_REQUIRED` 403 | Typed 403 |
| 2 | W2 revokes on every password change or only on reset and forced change | Every change |
| 3 | W3 blocks on `UserType.Admin` or on the resolved permission set | `UserType.Admin` |
| 4 | W5's cap value | 5 active keys |
| 5 | W9's re-enrolment rollout timing | After the event, not before |
| 6 | W10 needs a Figma node for the "my devices" screen | Owner to supply or approve a deviation |

---

# Item 2: file pointers become real foreign keys to the central file table

| Field | Value |
|-------|-------|
| Status | **Waiting for owner approval.** No code written. |
| Raised | 2026-08-13 |
| Trigger | Owner observation: "`public string? AvatarRelativePath` ... we have centralized file management for all documents, so why on the first table do we find a path, and break the rule?" |
| Scope | Persistence, the services that read and write these pointers, the Control Panel upload and profile screens, seeders, tests, docs. Public JSON field names are explicitly OUT of scope. |
| Related | D-157 (data and identity separation, permanent), D-568 (one `StoredFile` table), D-877 to D-881 (profile-owned admission, both migration histories regenerated), D-219 (wire contract stays append-only), D-110 (freeze) |

## 2.1 What was asked

Two things, in the owner's own words:

1. Why does a table carry a **file path** when the system has centralised file
   management for every document.
2. The fix: *"no cross db reference, you can simply change instead of save path
   to saving Guid or real record."*

## 2.2 Finding: not one of these columns holds a path, and every name says it does

Nine columns are named `*RelativePath`. The name is a leftover from the era
before D-568 unified the file store. What each one actually holds today:

| Column | Database | What it really holds | Verified at |
|--------|----------|----------------------|-------------|
| `SimfUser.AvatarRelativePath` | **Identity** | `StoredFile` Guid, as text | `AccountService.cs:150` writes `result.Id.ToString()`; `ParseFileId` at `:243` is `Guid.TryParse` |
| `UserProfile.VipPhotoRelativePath` | App | `StoredFile` Guid, as text | `UserProfileService.cs:842` |
| `UserProfile.IdImageRelativePath` | App | `StoredFile` Guid, as text | `UserProfileService.cs:665`, `:743`, `IdentitySeeder.cs:1525` |
| `Sponsor.LogoRelativePath` | App | whatever an admin typed into the legacy text box | `AdminSponsorService.cs:244` |
| `News.ImageRelativePath` | App | as above | `AdminNewsService.cs:246` |
| `MediaPartner.LogoRelativePath` | App | as above | `AdminMediaPartnerService.cs:197` |
| `ArchiveEdition.CoverImageRelativePath` | App | as above | `AdminArchiveService.cs:235` |
| `ArchivePastSpeaker.PhotoRelativePath` | App | as above, plus a spreadsheet cell | `AdminArchiveService.cs:554` (length-guarded to 256 from admin input) |
| `Speaker.PhotoRelativePath` | App | no writer found | `Speaker.cs:88-91` documents it as vestigial, kept only as a contract fallback. See 2.9 |

So the centralised file store is **not** bypassed for the pointers that matter:
the bytes go through `IFileService` and land in `StoredFiles`, encrypted at rest,
with the malware scan, the magic-byte allow-list and the SHA-256 that pipeline
performs. The owner's instinct was right about the **name**, and the name has
been actively misleading every reader of the schema.

Two doc comments are worse than the names, because they assert the false thing
rather than merely implying it:

- `UserProfile.cs:264-267` still describes "the relative path of the ID-image
  file inside the unified store rooted at `FileStorage:RootPath`, under its
  `IdDocument` folder". The column holds a Guid.
- `Sponsor.cs:28-29` still says "path to the logo asset, resolved against the
  static asset root".

`SimfUser.cs:51-57` is the honest one: it opens with "**Not a path.**"

## 2.3 The real defect: the link is recorded twice and enforced nowhere

This is the finding that justifies the change, rather than a rename.

The relationship between an owning row and its file exists in **two independent
places, in two different shapes, and the database enforces neither**:

| Half | Where | Enforcement |
|------|-------|-------------|
| `OwnerEntityType` + `OwnerEntityId` | on `StoredFile` | none. `StoredFileConfiguration.cs:9-11` states the pair is "polymorphic bare Guids and carry NO FK" |
| `*RelativePath` | on the owning row | none. It is an `nvarchar(256)` |

Nothing prevents the two halves from disagreeing. Nothing prevents a pointer
outliving the row it points at. The only reason a dangling pointer is survivable
today is that `IdentitySeeder` was taught to **self-heal** one (D-860): it
re-uploads when the pointer no longer resolves to content, because testing the
pointer for emptiness alone left demo accounts permanently broken after a
database restore. That repair exists precisely because there is no key.

By contrast, `DeviceKey` lives entirely inside the Identity database and has "a
real in-database FK" (item 1, section 12 compliance table). Single-database
relationships in this codebase **do** use real keys. The file store is the
exception, and only because it began life as a filesystem path.

## 2.4 The fix, as directed by the owner

No column stores a path. Nothing crosses the database boundary. The shape is
decided by which database the owning row sits in.

| Owning row | Link | Why |
|------------|------|-----|
| The 8 App-database owners (`UserProfile` x2, `Sponsor`, `News`, `MediaPartner`, `ArchiveEdition`, `ArchivePastSpeaker`, `Speaker`) | **Real record.** `Guid? XFileId` plus a `StoredFile` navigation, `HasForeignKey`, an index, and an explicit `OnDelete` | Both sides are in `SIMF_App`, so the database can enforce it. This is the "real record" the owner asked for |
| `SimfUser.Avatar...` | **Bare Guid only.** `Guid?`, no navigation, no FK | It would cross into `SIMF_Identity`, which D-157 forbids permanently. Identical in shape to `UserProfile.UserId`, which is a bare Guid for the same reason and documents why |

What disappears when the strings go, which is the payoff:

- Every `Guid.TryParse` defensive parse of a pointer (`AccountService.ParseFileId`
  at `:243`, and the equivalent inside `IdentitySeeder.NeedsReseedAsync`).
- Every `string.IsNullOrEmpty(pointer)` presence sentinel becomes
  `FileId is not null`. There are at least six, including the `HasAvatar` flag on
  three admin grids and the male-face registration gate.
- Every "a legacy non-Guid path may still be on this row" fallback branch.
- `nvarchar(256)` columns storing a 36-character Guid.

## 2.5 The avatar is misplaced, not only misnamed

Raised by the owner as a follow-up: "why do we save the user avatar here while it
already exists on the profile table?"

**It does not exist on the profile table.** `UserProfile` carries two other
images and no face photo:

| Column | What it is |
|--------|------------|
| `UserProfile.VipPhotoRelativePath` | "a separate high-resolution VIP photo, **distinct from the account avatar**" (`UserProfile.cs:164-165`) |
| `UserProfile.IdImageRelativePath` | the ID document |
| *(none)* | the face photo, which lives on `SimfUser` in the **other** database |

So this is not duplication. It is worse: one person's three images are split
across two databases, and the one that is most clearly an **attendee** attribute
sits on the **identity** row. Three consequences, all verified:

1. **A profile rule reaches across the database boundary to enforce itself.** The
   male-registrant face-photo gate is a profile rule, but must load the Identity
   row to read it: `UserProfileService.cs:229`,
   `&& string.IsNullOrEmpty(user.AvatarRelativePath)`. The comment at `:203`
   calls it "the FACE photo" while pointing at `SimfUser`.
2. **It can never have a real key**, purely because of where it sits.
3. **An attendee with no account has nowhere to hold a face photo.** D-877 made
   that the ordinary row: `UserProfile.cs:31-38` describes "a walk-in, or a badge
   minted into a bulk order" as the normal `UserId == null` case. Those attendees
   get badges. A badge wants a face.

### The catch

It cannot simply move. Administrators genuinely use that avatar
(`CpShellLayout.razor`, `Account/Profile.razor`, `AdminProfilePhotoBlock.razor`),
and `UserProfile.cs:9-10` states flatly: "Admin-typed users carry no profile."
Move the column and every administrator loses their photo.

### Options

| Option | What it does | Cost |
|--------|--------------|------|
| A. Rename only | `SimfUser.AvatarFileId`, bare `Guid?`. Nothing moves | Cheapest. Leaves all three consequences above in place. Account-less attendees still cannot have a face photo |
| B. Move wholesale to `UserProfile` | One column, real FK, cross-boundary read gone | **Breaks administrator avatars outright.** Rejected on that evidence |
| C. Split by meaning **(recommended)** | `UserProfile.FaceFileId` (real FK, App DB) for the attendee's face photo, and `SimfUser.AvatarFileId` (bare Guid, Identity DB) for the Control Panel account photo | Two columns, but they were never one thing. The visitor path stops touching Identity for images at all, and the cross-database read at `UserProfileService.cs:229` is deleted rather than renamed |

**Recommendation: C.** The two photos have different owners, different
populations, different lifecycles and different consumers. They were merged into
one column by history, not by design. Option C is also the only one that makes a
badge-only attendee capable of having a face photo, which is the case D-877 just
made ordinary.

## 2.6 Second finding: two uncentralised routes still set these images

### The Control Panel offers two ways to set the same image

Four Add/Edit pages present a modern upload on the central store **and**, above
it, a legacy free-text box bound to the `*RelativePath` column:

| Page | Legacy text field | Modern upload |
|------|-------------------|---------------|
| `SponsorsAddEdit.razor` | `:73-76` | `:100`, `<SimfImageUpload Category="SponsorLogo">` |
| `NewsAddEdit.razor` | `:66-69` | `:100`, `Category="NewsImage"` |
| `MediaPartnerAddEdit.razor` | `:26` | `:48`, `Category="MediaPartnerLogo"` |
| `ArchiveAddEdit.razor` | `:66-67` | `:124`, `Category="ArchiveCover"` |

Two competing ways to set one image is the duplication the North Star rule exists
to remove, and it is also how the two halves in 2.3 come to disagree in the first
place. The legacy text field and its resx label keys go with this change.

## 2.7 Files

**This list is partial.** The exhaustive call-site inventory was still running
when this section was written, and the file table must be completed from it
before the plan is executed. What is listed here is verified; what is missing is
breadth, not accuracy.

| Area | Change | Risk |
|------|--------|------|
| `src/Backend/SIMF.Domain/` (9 entities) | Replace each `string? *RelativePath` with `Guid? *FileId`; add the `StoredFile` navigation on the 8 App-side entities; correct the two false doc comments | **breaking** |
| `src/Backend/SIMF.Infrastructure/Persistence/Configurations/` | `HasForeignKey`, `OnDelete`, index per owner; drop the `HasMaxLength(256)` | **breaking** |
| `Persistence/Migrations/` (both contexts) | Fold into the regenerated `InitialCreate` per D-881, not a stacked migration | **breaking** |
| `Identity/AccountService.cs` | Avatar upload and remove; delete `ParseFileId`; check the delete-then-repoint ordering against the new FK | breaking |
| `Application/IdentityAccess/UserProfileService.cs` | ID image, VIP photo, and the face-photo gate at `:229` | breaking |
| `Identity/IdentitySeeder.cs` | Demo assets and the D-860 self-heal | none |
| The four `Admin*Service.cs` writers | Sponsor, News, MediaPartner, Archive | none |
| Read and projection sites | `HasAvatar` on three admin grids, profile completeness, URL builders | breaking |
| `src/Shared/SIMF.Contracts/` | Internal types only. **Public JSON field names must not change** | **breaking** |
| `src/ControlPanel/` | Delete the four legacy text fields and their resx keys; the avatar and VIP photo screens | none |
| Seeders, SQL content seed | A literal path in a seed script would break a typed FK on first run. Go or no-go item | **breaking** |
| `tests/` | Tests writing the fake sentinel `"storedfile:" + Guid` will not compile against a typed Guid | none |
| `docs/` | LLD-001 `:399` and `:411` name the column; page docs; E2E catalogue files | none |

## 2.8 Sequencing

Each increment must build and test on its own.

1. Domain plus EF configuration plus the regenerated `InitialCreate`.
2. Write paths, including the delete or repoint ordering, which a real FK
   constrains in a way a string never did.
3. Read paths, presence sentinels, URL builders.
4. The avatar split (2.5 option C), if approved. This is the only increment that
   moves data rather than retyping a column.
5. Control Panel, including deleting the legacy text fields.
6. Seeders and tests.
7. Docs and E2E catalogue, same changeset, per D-246.

## 2.9 Still to verify before execution

Recorded openly rather than assumed:

- `ArchivePastSpeaker.PhotoRelativePath`: no writer was found by the
  property-assignment sweep, which does not cover object-initializer syntax. Its
  writer must be found, or its absence confirmed, before it is retyped.
- `OnDelete` behaviour: whether `IFileService.DeleteAsync` performs a hard row
  delete or a soft `IsActive` flag decides between `Restrict`, `SetNull` and
  `NoAction`, and decides whether existing delete paths start throwing.
- Whether the polymorphic `OwnerEntityType` / `OwnerEntityId` pair is consumed by
  the owner-or-administrator download authorisation check. If it is, it cannot be
  dropped even once a real FK exists, and the invariant between the two halves
  needs a test instead.

## 2.10 Verification gate

Unit and integration green with real output; clean Release build; a **live**
upload, replace and remove performed on the Control Panel profile screen and on
one content page, with the row inspected afterwards to prove the FK holds and no
orphan `StoredFile` is left; the app's profile screen rendered on a device to
prove the images still resolve; review agents and `simplify`; docs in the same
changeset.

## 2.11 Not touching

Public JSON field names on any endpoint the shipped Flutter application decodes,
the D-157 separation itself, the file pipeline (scan, allow-list, encryption,
audit), and the `StoredFile` table's own columns.

## 2.12 Open questions for the owner

1. **Option C for the avatar?** It is the only option that lets a badge-only
   attendee have a face photo, and the only one that deletes the cross-database
   read instead of renaming it. Option B is rejected on evidence: it breaks
   administrator avatars.
2. **Delete the four legacy Control Panel text fields outright**, or leave them
   read-only for one release? Recommendation: delete. They are the mechanism by
   which the two halves diverge.
3. **`Speaker.PhotoRelativePath` is documented as vestigial.** Drop the column, or
   keep it because a public contract still emits it as a fallback? Recommendation:
   keep the contract field, drop the column, and have the contract emit null.

---

# Item 3: `DisplayName` duplicates the profile name, and the greeting rule is not built

| Field | Value |
|-------|-------|
| Status | **Waiting for owner approval.** No code written. |
| Raised | 2026-08-13 |
| Trigger | Owner: the display name should be the **first name**, or the **full name** for a company, and it is shown in the app greeting top bar. Plus: check whether the real app actually does this. |
| Scope | `SimfUser.DisplayName`, the two services that sync it, and the app home greeting. |
| Related | D-157 rule 2 (no duplicated data across the two databases), D-219 (wire contract) |

## 3.1 The rule as stated

The display name shown in the application greeting top bar should be:

- for a person: the **first name**
- for a company: the **full name**

## 3.2 Finding: the real application does NOT do this

Verified in the Flutter source, not assumed.

The greeting name is resolved in `home_screen.dart:161-172`:

Its own doc comment says the greeting takes the App profile name when known,
otherwise a name-less salute, and never the email, "the auth display name is the
email for accounts created without a separate display name":

```dart
String _greetingName(String? profileName, String? authName) {
  final profile = profileName?.trim() ?? '';
  if (profile.isNotEmpty) {
    return profile;
  }
  final auth = authName?.trim() ?? '';
  return auth.contains('@') ? '' : auth;
}
```

It is rendered whole by `GreetingHeader` (`greeting_header.dart:74-79`) as a
single line with `maxLines: 1` and `TextOverflow.ellipsis`.

Three things follow:

1. **The app shows the FULL name, not the first name.** It takes
   `UserProfile.Name` verbatim.
2. **There is no person-versus-company distinction anywhere.** A search of the
   whole `lib/` tree for `split(' ')`, `firstName` and `first_name` returns
   **zero** matches. The rule is not implemented, partially or otherwise.
3. A long name is not shortened, it is **ellipsised**, so today a long full name
   is simply cut off mid-word in the top bar.

**Answer to the owner's question: no, this is not done in the real app.**

## 3.3 Finding: `DisplayName` is a hand-synced copy of the profile name

This is the deeper issue behind the same observation, and it is a rule breach of
the same family as item 2.

`SimfUser.DisplayName` lives in `SIMF_Identity`. The person's real name lives on
`UserProfile.Name` / `NameArabic` in `SIMF_App`. The first is kept in step with
the second **by hand, from two different services**:

| Site | What it does |
|------|--------------|
| `RegistrationService.cs:91` | Seeds `DisplayName = request.Email`. Until the profile is submitted, the display name **is the email address** |
| `UserProfileService.cs:366-402` | Overwrites it with the profile's real name (English preferred, Arabic fallback), but **only while it still equals the email**, so an admin-customised name survives |
| `BadgeAuthService.cs:331-338` | Same again for bulk badge accounts. Its comment states the reason plainly: "otherwise the app greets them by the placeholder forever" |

Project CLAUDE.md, D-157 rule 2: "**No duplicated data.** Never persist a copy of
Identity-owned data inside `SIMF_App` (or vice versa); resolve it on read. The
**only** allowed copies are the existing immutable audit snapshots."

`DisplayName` is not an audit snapshot. It is live, mutable, and copied in the
"vice versa" direction. The app's own greeting helper is written to **work around
it**: the comment "never the email" and the `auth.contains('@')` guard exist only
because this copy can hold a placeholder.

The blast radius is wider than the greeting. `DisplayName` is carried on many
admin contracts (`Admin/Attendees.cs`, `Admin/Gates.cs`, `Admin/Invitations.cs`,
`Admin/PendingProfileResponse.cs`, `Admin/SessionModerators.cs` among others), so
this is a real piece of work, not a one-line deletion.

## 3.4 What the fix has to decide

1. **Where the greeting rule is computed.** Server-side, so the app, the Control
   Panel and the badge all agree, or client-side in the app only.
   Recommendation: server-side, exposed as its own field, because a display rule
   duplicated per surface is how the surfaces drift apart.
2. **What marks a "company".** Two candidates exist on the row and neither is
   self-evidently the right one: `UserProfile.OrganisationId` (`:106`, nullable)
   and `UserProfileType.IsForVisitor` (`:33`, which splits audience from partner
   kinds such as Exhibitor and Sponsor). This needs the owner's answer, see 3.6.
3. **Whether `DisplayName` survives at all.** Once the name is resolved from the
   profile, the Identity copy has no remaining job except for administrators, who
   have no profile row. That is the same split as item 2 section 2.5, and the two
   should be decided together.
4. **First name from what.** `UserProfile.Name` is documented as "full name in
   English, exactly as printed in the passport". Taking the first whitespace
   token is a guess about human names that is wrong often enough to matter for
   Arabic naming conventions. A stored given-name field is the correct answer if
   the greeting rule is to be dependable.

## 3.5 Verification gate

The app greeting rendered on a device for four cases: a person with a short name,
a person with a long name, a company attendee, and an account whose profile is
not yet submitted. Screenshots of each, in Arabic and English, since the top bar
is RTL-first.

## 3.6 Open questions for the owner

1. **What marks a company attendee**: a non-null `OrganisationId`, or a profile
   type with `IsForVisitor = false`? They are not the same set, and the answer
   changes who gets a full name.
2. **First name derived by splitting the passport name, or a new stored
   given-name field?** Recommendation: a stored field. Splitting a passport name
   on the first space is unreliable for Arabic names, and the greeting is the most
   visible string in the application.
3. **Should item 3 be executed together with item 2 section 2.5?** Both come down
   to the same question: what still belongs on the Identity row once `UserProfile`
   is the attendee record. Recommendation: yes, decide them together, execute them
   as one programme.
