# Badge sign-in / activation — `/api/v1/app/auth/badge-*`

| | |
|--|--|
| **Routes** | `POST .../resolve-badge` · `POST .../badge-sign-in` · `POST .../badge-activation/start` · `POST .../badge-activation/complete` |
| **Surface** | Public auth API |
| **Auth** | `AllowAnonymous` — all four run **before** a bearer token can exist, and each carries its own credential (the badge QR, the account password, an emailed code) |
| **Source** | `src/Backend/SIMF.Api/Endpoints/Auth/BadgeAuthEndpoints.cs` · `src/Backend/SIMF.Application/IdentityAccess/BadgeAuthService.cs` · `src/Shared/SIMF.Contracts/Authentication/BadgeAuth.cs` |
| **Tests** | `tests/SIMF.Api.Tests/BadgeAuthTests.cs`, `BadgeSelfClaimProfileTests.cs` · E2E [`api-badge-self-claim-profile.md`](../../tests/e2e/api-badge-self-claim-profile.md), [`mobile-badge-activation.md`](../../tests/e2e/mobile-badge-activation.md) |
| **Last reviewed** | 2026-07-31 |

## Purpose

A printed badge is handed to a VIP or a walk-in who may have no SIMF account yet.
Scanning its QR must either drop the holder into the normal password sign-in (if the
account already has a password) or let them claim the badge — verify an email, set a
first password, and now **tell us who they are**.

## Security model

The badge QR (physical possession) plus control of an email inbox are the two factors
for setting a first password. When the resolved account already has a real email the
code goes **there** and any client-supplied address is ignored, which defeats a
badge-photo takeover. When the account has only a placeholder `@simf.local` address,
the holder supplies one; it is stashed as *pending* and promoted to the account only
after the code is verified (verify-then-attach), so a mistyped or hostile address can
never brick the badge. Only an **Approved**, active account resolves at all, and an
unknown QR is indistinguishable from a wrong password.

## Self-claim profile capture (`#10` phase 4)

A bulk badge run mints a placeholder profile: a generated display name such as
"VIP #3", `NationalityId = 0`, no interests. Self-claim is the only moment the real
holder is at the keyboard, so `BadgeActivationCompleteRequest` also carries:

| Field | Type | Notes |
|---|---|---|
| `englishName` | `string?` | Replaces the generated placeholder name and the account `DisplayName` |
| `arabicName` | `string?` | Same rules |
| `nationalityCode` | `string?` | ISO alpha code; unknown/inactive → 400 `PROFILE_NATIONALITY_UNKNOWN` |
| `interestIds` | `Guid[]` | Up to 10; unknown/deactivated → 400 `INTEREST_INVALID`. **Added**, never removed |

Every field is optional and appended with a default, so the shipped wire contract
stays append-only (D-219): a client that sends none activates exactly as before.

**Two rules that are easy to get wrong and are pinned by tests:**

1. **Validate before any write.** The country code and interest ids are resolved
   against the live App-DB lookups up front, so a bad payload cannot half-activate a
   badge.
2. **Profile first, password second.** If the password step then fails, the badge is
   still unactivated and the holder retries — the profile write is idempotent. The
   reverse order would leave an activated account whose retry is refused by
   `EnsureNotAlreadyActivated`, with the placeholder never filled.

The profile lives on `SIMF_App` and the account on `SIMF_Identity`, so the two are
separate units of work — D-157 forbids a transaction spanning both databases. There is
**no Identity schema change**.

## Error codes

| Code | HTTP | When |
|---|---|---|
| `AUTH_ACCOUNT_NOT_FOUND` | 404 / 400 | Badge not recognised; or activation needs an email and none was supplied/stashed |
| `AUTH_EMAIL_ALREADY_REGISTERED` | 409 | The supplied email belongs to another account |
| `BADGE_ALREADY_ACTIVATED` | 409 | The account already has a password |
| `AUTH_RESET_CODE_INVALID` | 400 | Wrong code, or the 5-attempt cap was hit |
| `AUTH_RESET_CODE_EXPIRED` | 400 | Past the 10-minute lifetime |
| `AUTH_PASSWORD_POLICY` | 400 | The chosen first password fails policy |
| `PROFILE_NATIONALITY_UNKNOWN` | 400 | Unknown/inactive ISO country code |
| `INTEREST_INVALID` | 400 | Unknown/deactivated interest id |

## Follow-up

The app screen `badge_activation_screen.dart` still routes straight to `signIn` after
activation, so today it sends none of the profile fields. Adding the capture step is
Track D's half of `#10-phase4`.
