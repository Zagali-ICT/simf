# SIMF Security Review: device-key (biometric) authentication subsystem

| Field | Value |
|-------|-------|
| Document | SIMF-Security-Review-DeviceKeys-2026-08-13 |
| Date | 2026-08-13 |
| Trigger | Owner request during the device-key label review |
| Subsystem | Device-key / biometric sign-in (D-172, #7a, D-738) |
| Method | Source review only |
| Findings | 10 (2 High, 1 Medium-High, 4 Medium, 3 Low) |
| Remediation plan | `docs/reviews/Mohaned-Review.md` Item 1 §13 |
| Best practice and strategy | §7 of this document |
| Companions | `SIMF-Security-Assessment-2026-06-20.md`, `SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md`, `SIMF-Threat-Model-2026-06-21.md` |

---

## 1. Executive summary

The device-key subsystem lets a user sign in with Face ID or Touch ID by signing
a server-issued challenge with a private key held on the device. Its **core
cryptographic ceremony is sound**: correct ES256 verification, a genuinely atomic
single-use challenge consumption, hashed and rate-capped enrolment codes, and an
anonymous surface that is pinned by a build-breaking test.

The defects are not in the cryptography. They are in **how this second sign-in
path rejoins the account lifecycle**. The password path accumulated a set of
gates over time (forced password change, maximum password age, lockout, account
state) and the device-key path was never wired into them. The result is a second
door into the same account that honours fewer rules than the first one.

Two findings are High and both are small to fix:

- **S1** lets any biometric user sign in forever after their password expires,
  which makes the NCA maximum-password-age control unenforceable for that
  population.
- **S2** means a password reset does not revoke device keys, so the standard
  advice given to a compromised user leaves a working attacker credential in
  place.

Neither needs a schema change. S1 is one guard in one method, S2 is one
repository call in one method.

## 2. Scope and method

**Read in full:** `DeviceKey.cs`, `DeviceKeyService.cs`, `DeviceKeyEndpoints.cs`,
`DeviceKeyConfiguration.cs`, `DeviceKeys.cs` (contracts), `DeviceKeyOptions.cs`,
`TokenIssuer.cs`, `ITokenIssuer.cs`, the device-key region of
`auth_controller.dart`, `biometric_auth.dart`, `biometric_sign_in.dart`,
`biometric_step_up_screen.dart`, `secure_storage.dart`.

**Read in the relevant part:** `SignInService.cs`, `PasswordService.cs`,
`JwtTokenService.cs`, `AccountState.cs`, `SimfUser.cs`,
`BusinessFlow13PermissionMatrixTests.cs`, `auth_controller_device_key_test.dart`.

**Explicitly NOT done.** No test suite was executed. No build was run. No runtime
exploitation was attempted, on any environment. Every finding below is a
source-verified reading with the evidence cited by file and line, and every
severity is a judgement about that source, not a measured result. Where a finding
is contained by something rather than open, that containment was checked in
source and is stated.

## 3. What the subsystem does well

Recorded so the findings are read in proportion.

**Replay protection is correct and non-obvious.** The challenge is consumed by a
conditional `ExecuteUpdateAsync` that matches only the row still holding that
exact challenge value (`DeviceKeyService.cs:308-324`). A concurrent replay inside
the validity window clears nothing, gets `affected == 0`, and is rejected before
any token is minted. This is the right pattern and it is rare to find it done
properly.

**The enrolment step-up is properly built.** The code is keyed-hashed and never
persisted in plaintext, compared with `CryptographicOperations.FixedTimeEquals`,
capped at five issues per hour and five attempts per code, with the code burned
once the attempt budget is spent. A prior unconsumed code is invalidated when a
new one is issued.

**Enrolment needs two factors.** An emailed code plus an OS device-credential
confirmation (`biometric_step_up_screen.dart:143-157`), so neither a borrowed
unlocked phone nor mailbox access alone is sufficient.

**Failure responses do not leak which step failed.** `SignInWithDeviceKeyAsync`
returns null for every failure mode and the endpoint maps all of them to one 401.

**The anonymous surface is pinned by test.** Both anonymous device-key routes
carry per-entry justifications in `BusinessFlow13PermissionMatrixTests.cs:115-118`,
so an eighteenth unauthenticated endpoint fails the build.

**Session parity is enforced.** All entry points mint through `TokenIssuer`, so
the claim set and the absolute session cap cannot drift, and
`TokenIssuerParityTests` holds that.

**Client key storage uses the right baseline:** `flutter_secure_storage` with
`encryptedSharedPreferences: true` (`secure_storage.dart:21-23`).

---

## 4. Findings

| Id | Severity | Title | Fix effort |
|----|----------|-------|-----------|
| S1 | **High** | Biometric sign-in bypasses the forced-password-change and max-password-age gate | Small |
| S2 | **High** | A password change or reset does not revoke device keys | Small |
| S3 | Medium-High | An administrator can hold a permanent 2FA-free admin session via a device key | Small |
| S4 | Medium | Account lockout is not honoured on the device-key path | Small |
| S5 | Medium | No cap on device keys per account and no lifecycle revocation | Medium |
| S6 | Medium | Audit-detail injection through the user-controlled label | Small |
| S7 | Medium (DoD) | The admin revoke endpoint sits outside the permission catalogue | Small |
| S8 | Low | Anonymous challenge issuance is an existence oracle and an unauthenticated-write vector | Small |
| S9 | Low | The private key is software-bound, not hardware-bound (already documented) | Large |
| S10 | Low (DoD) | The list endpoint ships with no consumer, no page doc and no E2E scenario | Medium |

---

### S1. High. Biometric sign-in bypasses the forced-password-change gate

**Evidence.** `SignInService.cs:140-147` sets `PasswordChangeRequired` once the
password ages past `IdentityLifecycle:PasswordMaxAgeDays`, under a comment that
names it an NCA control. `SignInService.cs:149-160` then refuses an app-audience
sign-in with a 403 `AUTH_PASSWORD_CHANGE_REQUIRED`. The comment at
`SignInService.cs:130-133` enumerates every later token-mint path that re-checks
the flag through `RequirePasswordChangeNotRequired`, and that helper is called at
lines 313, 373, 490 and 825.

`DeviceKeyService.SignInWithDeviceKeyAsync` (lines 264-336) calls none of them.
Its only account check is `user.AccountState == AccountState.Disabled` at line
327, and it mints at line 335.

**Impact.** Any user who has ever enabled Face ID keeps signing in indefinitely
after their password expires and is never driven to change it. The maximum
password age control cannot be enforced for that population. Because the same
flag is what an administrator sets to force a change, an administrator-forced
password change is equally bypassable.

**Why this reads as an oversight rather than a decision.** The comment at line
131 exists specifically to enumerate the mint paths that carry this gate, and the
device-key path is absent from a list that is otherwise complete.

**Fix.** Check `user.PasswordChangeRequired` in the device-key mint, after the
existing user lookup, and return the typed 401. See remediation W1.

---

### S2. High. A password change or reset does not revoke device keys

**Evidence.** `PasswordService.ClearChangeFlagAndEndSessionsAsync`
(lines 421-434) is the single point that every change, reset and forced-complete
path funnels through. It clears the flag, stamps `PasswordChangedAt`, and calls
`refreshTokenRepository.RevokeAllForUserAsync`. That is the entirety of what it
revokes. `PasswordService.cs` contains **zero** references to `DeviceKey`.
`ResetTwoFactorEndpoint.cs` contains zero. The only writes to the `DeviceKeys`
table anywhere in the solution are inside `DeviceKeyService`.

**Impact.** Take an account compromised while a session is live. The attacker
enrols a device key. This does require the emailed step-up, so it presumes
mailbox access or a live session on an unlocked device, which is exactly the
position an attacker with a hijacked session is in. The victim then does the one
thing every security notice instructs and resets their password. Every refresh
token is revoked. The device key is not. `POST /app/auth/sign-in-with-device-key`
is `AllowAnonymous`, so the attacker mints a brand-new full session from the key
they still hold. The remedy does not remedy, and the victim has been told it did.

**Secondary defect.** The XML doc on the method states that it "ends every
session", which is no longer accurate and will mislead the next reader.

**Fix.** Revoke the user's device keys in that same method, and dispatch a
notification so the owner sees which devices were removed. See W2.

---

### S3. Medium-High. An administrator can hold a 2FA-free admin session

**Evidence.** `RegisterDeviceKeyEndpoint` (`DeviceKeyEndpoints.cs:17-23`) gates on
`RequireApprovedAccount` alone, with no user-type check, no audience check, and
nothing excluding an administrator. `TokenIssuer.IssueAsync` (lines 33-37)
resolves roles and permissions for whichever user it is handed and stamps them
into the access token; it takes no audience parameter, so there is exactly one
token shape. The device-key mint passes `secondFactorCompleted: null`
(`DeviceKeyService.cs:417`).

**Impact.** This works against the enrolment-first decision recorded at
`SignInService.cs:188-194`, whose stated purpose is that the Control Panel "must
never mint a session on the password alone". A device key mints an
admin-permissioned bearer token with **no second factor at all**, and that bearer
is accepted by the `/admin/*` endpoints.

**Qualification, stated because it changes priority.** This requires an
administrator to enrol biometrics on the mobile app, and the Control Panel UI
itself authenticates by cookie through the BFF rather than by this bearer. So it
is a defence-in-depth failure and a policy gap, not a demonstrated live path. It
should still be closed: nothing in the code prevents it, and nothing warns an
administrator who is about to do it.

**Fix.** Refuse enrolment for an account that resolves any admin permission, or
restrict the register endpoint to the app audience. See W3.

---

### S4. Medium. Lockout is not honoured on the device-key path

**Evidence.** The device-key mint checks only `Disabled`
(`DeviceKeyService.cs:327`). The password path additionally blocks `Registered`
through `CheckAccountState` (lines 655-668) and enforces lockout through
`EnsureNotLockedOutAsync` (line 721).

**Containment that was checked, not assumed.** `JwtTokenService.cs:47` stamps
`account_state` into every token, including the device-key one, since all paths
share `TokenIssuer`. `SimfUser.cs:21-23` records the intent directly:
"PendingApproval and Rejected do sign in and are routed to their own screens by
the account_state claim, so this is not on its own an access decision", and
protected endpoints stack `RequireApprovedAccount`, documented as
`RequireClaim("account_state", "Approved")` in
`docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md:297`. A `Rejected` or
`PendingApproval` holder therefore gets a token with very little reach, by design.

**What is genuinely open.** Lockout. While any device key exists, locking an
account is not a reliable way to freeze it.

**Fix.** Mirror the lockout check, and the `Registered` state check for
completeness. See W4.

---

### S5. Medium. No cap and no lifecycle revocation

**Evidence.** `RegisterAsync` performs no count query. Nothing outside the two
revoke endpoints ever sets `RevokedAt`.

**Impact.** An account can accumulate unbounded permanent alternative
credentials, each an independent persistence foothold. Neither the user nor an
administrator has any surface on which to see them, because of S10. Combined with
S2, a key enrolled once survives every remediation short of an administrator
manually issuing `DELETE /admin/device-keys/{id}` for an id they currently have
no way to look up.

**Fix.** Cap active keys per account, revoke the oldest on overflow, and notify
the account owner on every enrolment so a silent one is visible. See W5.

---

### S6. Medium. Audit-detail injection through the label

**Evidence.** `DeviceKeyService.cs:137` builds
`Detail = $"deviceKeyId={deviceKey.Id}; label={label}"`. The label is
user-supplied and validated for length only, 1 to 64 characters.
`DeviceKey.cs:48` states that "the contents are never interpreted".

**Impact.** A label containing `;`, `=`, CR or LF forges fields inside a
`key=value; key=value` audit record. Audit-trail integrity is an NCA concern and
this record is the only evidence of who enrolled which credential. It is also a
stored-XSS candidate anywhere the detail is rendered into the Control Panel.

**Note.** This predates the label work. The hardcoded `SIMF mobile` default never
prevented it, because any caller holding a token can post any 64-character string
directly to the endpoint.

**Fix.** Reject the character set server-side in `RegisterAsync`, or encode the
label when composing the audit detail. Client-side stripping is a hygiene measure,
not a control. See W6.

---

### S7. Medium (Definition of Done). The admin revoke sits outside the catalogue

**Evidence.** `AuthorizationPolicies.AdministratorOnly` appears exactly **once**
in the entire `src/Backend/SIMF.Api/Endpoints` tree, at
`DeviceKeyEndpoints.cs:165`. No other endpoint uses that legacy policy. The
project CLAUDE.md D-207 / D-208 hard rule requires every admin API action to be
gated by `Policies(PermissionCatalog.PolicyFor(...))`.

**Impact.** The endpoint **is** gated, so this is not an open door. But device-key
revocation cannot be delegated to a non-Administrator role, it does not appear in
`PermissionCatalog` or in the permission catalogue document, and it is invisible
to the permission matrix the project uses to reason about its admin surface.

**Fix.** Add the permission code, seed it, and gate the endpoint with it. See W7.

---

### S8. Low. The anonymous challenge endpoint

**Evidence.** `IssueDeviceKeyChallengeEndpoint` is `AllowAnonymous` and returns
404 `DEVICE_KEY_NOT_FOUND`, 401 `DEVICE_KEY_REVOKED`, or 200, depending on state,
whereas the sign-in endpoint deliberately collapses everything into one 401. Each
call writes to the database, setting `CurrentChallenge` and `ChallengeExpiresAt`,
and issuing overwrites any challenge already in flight.

**Impact.** Anyone holding a device-key id can invalidate a legitimate in-flight
sign-in by requesting a new challenge, and can drive unauthenticated database
writes. Severity stays Low because the ids are 128-bit GUIDs, making enumeration
impractical, and the `auth` per-IP rate limiter caps the volume.

**Note on the existing comment.** The endpoint comment asserts that a leaked id
"does not enable sign-in", which is true, but it is not the whole risk statement.

**Fix.** Collapse the not-found and revoked responses into one, and consider
requiring the challenge request to be the same call as the signature submission.
See W8.

---

### S9. Low, already documented. Software-bound private key

`DeviceKey.cs:12-17` states plainly that the private key is software-bound and
that the biometric prompt gates the code path reaching the key rather than the key
material, with hardware binding listed as planned hardening.

**Impact.** On a rooted or jailbroken device the key can be extracted and then
used from anywhere with no biometric involved. Closing it means Android Keystore
or StrongBox with `setUserAuthenticationRequired`, or the iOS Secure Enclave. The
entity comment notes this needs no change to the server contract, which stays a
SubjectPublicKeyInfo in and an ES256 verify.

Listed so the position is explicit and dated rather than forgotten. See W9.

---

### S10. Low (Definition of Done). The list endpoint is undocumented and untested

**Evidence.** `GET /app/auth/device-keys` has no client consumer in the app, the
Control Panel or the ApiClient, no `PAGE-INDEX.md` row, and no E2E scenario.
D-246 requires documentation plus unit and integration tests plus an E2E
catalogue entry in the same changeset.

**Impact.** This is the gap that produced the original owner complaint: the label
was designed for a revoke list that was never built, so nothing ever surfaced it.
See W10.

---

## 5. Compliance summary

| Standard | Status | Finding |
|----------|--------|---------|
| NCA, maximum password age | **Fails** while any device key exists | S1 |
| NCA, credential revocation on compromise | **Fails** | S2 |
| NCA, multi-factor for privileged access | **Gap** | S3 |
| NCA, account lockout as a containment control | **Gap** | S4 |
| NCA, audit-trail integrity | **Gap** | S6 |
| NCA, session cap and claim parity across entry points | Passes, enforced by `TokenIssuer` and `TokenIssuerParityTests` | none |
| NCA, credentials at rest on the device | Partial, encrypted storage but no hardware binding | S9 |
| Anonymous-surface rule (project CLAUDE.md §4) | Passes, pinned by test with per-entry justifications | none |
| D-207 / D-208 per-action permissions | **Fails** for the admin revoke | S7 |
| D-157 data and identity separation | Passes, `DeviceKey` is Identity-only with a real in-database FK | none |
| D-246 documentation plus tests plus E2E | **Fails** for the list endpoint | S10 |
| D-110 Identity freeze | Respected, no finding requires a schema change | none |

**Reading this table.** Five NCA gaps sound worse than the subsystem is. Four of
them trace to the same root cause: the device-key path was never joined to the
account-lifecycle gates the password path accumulated. Fixing S1, S2 and S4
closes three of them and is a day of work, not a redesign.

## 6. Remediation

Every finding is carried as a numbered work item, W1 to W10, in
`docs/reviews/Mohaned-Review.md` Item 1 §13, with the files, per-file risk tags,
approach, tests and Definition of Done for each. That plan is the executable
half of this report.

**Status: wave A built 2026-08-13. Waves B, C and D unstarted.** The owner took
every recommended decision on that date, and wave A was implemented against them
the same day. S1, S2 and S4 are closed in code; the compliance table in §5 still
describes the position **before** that fix, so read it together with this table.

| Wave | Findings | Status | Rationale |
|------|----------|--------|-----------|
| A | S1, S2, S4 | **BUILT 2026-08-13** | The two High findings plus the lockout gap. Evidence in `Mohaned-Review.md` §13.1 |
| B | S3, S6, S7 | **BUILT 2026-08-13** | Admin enrolment refused, audit-detail injection closed at both boundary and sink, admin revoke moved into the permission catalogue. Evidence in §13.2 |
| C | S5, S8 | **BUILT 2026-08-13** | Active-key cap (default 5) and the challenge-endpoint oracle closed. The enrolment **notification** part of S5 is outstanding: it needs an additive `NotificationKind` plus bilingual resx |
| C | S10 | **Blocked** | Needs a Figma node for the "my devices" screen, which `simf_app/CLAUDE.md` §13.5 forbids inventing |
| D | S9 | **Deferred by decision** | Hardware key binding. Forces every enrolled user to re-enrol, so it runs after the event |

**Nine of the ten findings are closed in code.** S10 is blocked on an asset and
S9 on a scheduling decision, both recorded rather than forgotten.

Decisions taken on the remediation itself, with reasoning, are tabulated at the
end of `Mohaned-Review.md` §13. One item stays verify-first: S8 must not be built
until the Flutter error mapping is read, because collapsing the not-found and
revoked codes could strand users with a dead local key.

## 7. Best practice and strategic recommendation

**Sourcing note.** Where this section cites a SIMF file and line, or a platform
API, that was verified. The standards named below (W3C WebAuthn, FIDO2, NIST
SP 800-63B, OWASP MASVS-AUTH and MASVS-CRYPTO) are named as the relevant
frameworks; their control text is **not** reproduced verbatim here, because it
was not read in full during this review. Treat the framework names as pointers
for a formal compliance mapping, not as quotations.

### 7.1 The finding behind the findings

`docs/SIMF-Implementation-Gap-Report.md:142` already describes this subsystem as
the "device-key **passkey** ceremony". It is not a passkey ceremony. It is a
carefully hand-built approximation of one, and the ten findings above are mostly
the seams where a hand-built version differs from the standard.

The sharpest consequence is not in the finding list, because it is a property of
the design rather than a defect in it:

> **The server has no evidence that a biometric ever happened.**

`biometric_sign_in.dart:48-59` runs the OS prompt, and only then calls
`signInWithDeviceKey`, which reads the key and signs
(`auth_controller.dart:375-390`). Nothing about the prompt reaches the server.
The server sees a valid ES256 signature and infers a biometric from it. An
attacker holding the extracted private key produces exactly the same signature
without any prompt, and the server cannot tell the two apart.

Combined with S9 (the key is software-bound, so extraction is the realistic
attack), this means **the biometric in "biometric sign-in" is a client-side user
experience gate, not an authentication factor the server can verify**. The entity
comment at `DeviceKey.cs:12-17` states the software-binding half of this
honestly. The unverifiability half is the part worth naming explicitly, because
it is what users and auditors will assume is true and it is not.

WebAuthn solves precisely this with the User Verification flag, which is included
in the signed authenticator data, so the server verifies cryptographically that
the authenticator performed a user check.

### 7.2 What SIMF already does that matches best practice

Recorded so the recommendation is read as "finish this", not "replace this".

| Practice | SIMF |
|----------|------|
| Do not invent cryptography | Followed. ES256 on P-256, SubjectPublicKeyInfo, IEEE-P1363 signatures. All standard, all what the platforms sign with natively |
| Single-use, server-issued, short-lived challenge | Followed, 5 minutes, with an atomic consume that defeats concurrent replay |
| Two factors to bind a new credential | Followed, and better than most. An emailed code plus an OS device-credential confirmation |
| Never persist a secret in plaintext | Followed. Codes are keyed-hashed, compared in constant time |
| Rate limit the authentication surface | Followed, per IP plus a per-account issue cap |
| Algorithm agility | Followed. The `Algorithm` column and the 256-char key column exist so Ed25519 or ML-DSA can be added without a schema change |
| Pin the anonymous surface with a test | Followed, and rare. `BusinessFlow13PermissionMatrixTests` breaks the build on a new anonymous endpoint |
| One place mints sessions | Followed. `ITokenIssuer`, so claims and session caps cannot drift between entry points |

### 7.3 Practices not yet met, beyond the numbered findings

**Bind the key to the current biometric set.** Hardware binding alone is not
enough. On Android, `KeyGenParameterSpec.Builder.setInvalidatedByBiometricEnrollment`
invalidates the key when a new fingerprint or face is enrolled; on iOS the
equivalent is the `kSecAccessControlBiometryCurrentSet` access-control flag.
Without it, an attacker who has an unlocked phone for two minutes adds their own
fingerprint and can then use the victim's credential indefinitely. This belongs
in W9 and is currently not in it.

**Re-authenticate before sensitive actions.** A possession factor is a
reasonable way to resume a session. It is not a reasonable way to authorise a
privileged or destructive operation without a fresh check. This is the general
form of S3.

**Make credential creation visible to the owner.** Enrolment currently produces
an audit row and nothing the user ever sees. A notification on every enrolment is
the cheapest possible detection control for the S2 attack scenario. Carried in
W5.

**Attribute failed attempts.** `AuditFailureAsync` writes a null actor and only
the device-key id, so failed device-key sign-ins cannot be correlated to an
account for monitoring. The id resolves to a user, so this is a small fix.

**State the recovery path.** If the device is lost, recovery is the password
path. That is correct and needs no change, but it should be written down, because
"my Face ID stopped working" is a support question that will be asked.

### 7.4 Recommendation

Three horizons, matched to the programme's constraints (a hard event deadline and
handover on 2027-01-25).

**Now, before the production publish: wave A only.** S1, S2 and S4. They close
three of the five NCA gaps, they touch two methods, and none of them needs a
schema change. This is the highest ratio of compliance closed to risk taken in
the whole report.

**Before handover: S9, with the biometric-set binding from §7.3.** Move key
generation into Android Keystore or StrongBox with `setUserAuthenticationRequired`
and `setInvalidatedByBiometricEnrollment`, and into the iOS Secure Enclave with
`kSecAccessControlBiometryCurrentSet`. The server contract does not change, as
`DeviceKey.cs:12-17` already notes. This is what converts the biometric from a
user experience gate into something an attacker cannot skip. It forces every
enrolled user to re-enrol, which is why it is scheduled after the event.

**V2: evaluate replacing the bespoke ceremony with platform passkeys.**
WebAuthn subsumes S5, S8 and S9 outright and closes §7.1, because User
Verification, signature counters for clone detection, hardware binding and
credential lifecycle conventions all come with the standard rather than being
hand-maintained. The honest counter-argument is that the current implementation
works, is well built, and a migration invalidates every enrolled credential. So
this is a V2 candidate for `docs/SIMF-V2-Plan.md`, not a live proposal.

**What not to do:** do not keep incrementally hardening the bespoke ceremony
past wave A and S9. Each additional hand-built control (clone detection, richer
attestation, origin binding) is a re-implementation of something the standard
already specifies and the platforms already ship. Wave A plus S9 is the point of
diminishing returns; past it, the right move is the standard, not more custom
code.

## 8. Change log

| Date | Change |
|------|--------|
| 2026-08-13 | First issue. 10 findings, no remediation implemented |
| 2026-08-13 | Owner took every recommended remediation decision. Wave A approved to build. Still nothing implemented |
| 2026-08-13 | Added §7, best practice and strategic recommendation |
