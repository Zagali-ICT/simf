# SIMF Round-1 — Held Items: Remediation Plans (owner decisions)

These 5 audit findings were **not** auto-fixed on `fix/round1-defects` — each needs an owner
decision (security policy, product behaviour, or a change that touches the frozen schema). For each:
the defect, the decision needed, options with trade-offs, a recommendation, the change scope, the
risk, and the test that proves it. Nothing here is implemented yet.

Companion: `docs/tests/SIMF-Round1-Run-Log.md` (full audit + the 23 committed fixes + the
base-vs-branch regression certification). Findings detail: `scratchpad/audit_confirmed.json`.

> Security note: a plaintext super-admin credential currently lives in a repo scratch file
> (`txt.txt`). Independent of the items below, remove it from the working tree and rotate it —
> plaintext creds in the repo are exactly the class of exposure #1 is about.

---

## #1 — BLOCKER — demo accounts (incl. Administrator) seeded in every environment
**Defect** — `IdentitySeeder.EnsureDemoAccountsAsync` (IdentitySeeder.cs:510) runs on every non-Testing
boot including production. `DemoSeedOptions.DemoPassword` has a **hardcoded non-empty default**
(`"Simf@Demo2026#"`, DemoSeedOptions.cs) and no environment gate, so `admin@simf.local` is created
with the Administrator role, `PasswordChangeRequired=false`, `TwoFactorEnabled=false` — a pre-known,
source-committed admin credential. (Compounds with #2.)

**Decision needed** — how to guarantee demo accounts never exist in production.

**Options**
- **A (recommended)** — gate the whole `EnsureDemoAccountsAsync` call behind `IsDevelopment()` (or an
  explicit `Seed:EnableDemoAccounts` flag defaulting **false**). Prod is clean by construction; Dev
  keeps its logins.
- B — remove the hardcoded `DemoPassword` default so the existing `IsNullOrWhiteSpace` guard skips
  seeding when prod config doesn't set it. Weaker: relies on prod config *not* setting a value.
- C — keep seeding but force `PasswordChangeRequired=true`, no default password, `TwoFactorEnabled=true`.
  Still leaves known usernames + a seeding path in prod.

**Recommendation** — **A**, plus owner ops: rotate any already-deployed demo/super-admin credentials
and delete the committed password from source/config (and `txt.txt`).
**Scope** — `IdentitySeeder.SeedAsync` (wrap the call), `DemoSeedOptions` (drop the default).
**Risk** — *breaking for local dev if mis-scoped* — keep demo accounts in Development only.
**Test** — `EnsureDemoAccountsAsync` is a no-op when environment ≠ Development / flag off; demo login
works in Dev.

---

## #2 — HIGH — Control-Panel sign-in not TOTP-enforced when 2FA is not enrolled
**Defect** — `SignInService.cs:178` `if (!user.TwoFactorEnabled)` mints a full token on the password
alone, including CP-audience admins. A default prod deploy leaves the super-admin single-factor
(`SuperAdmin:TotpSecret` defaults `""`; no boot guard requires it). Contradicts SIMF-API-001 §12.3.

**Decision needed** — enforce a second factor for every Control-Panel sign-in.

**Options**
- A — for `audience==Cp`, if `!TwoFactorEnabled`, return a **mandatory-2FA-enrolment** challenge and
  withhold the token until enrolled (needs an enrolment page + comms).
- B — boot-fail-fast in production when `SuperAdmin:TotpSecret` is empty (mirrors the existing
  fail-fast guards), and force `TwoFactorEnabled=true` on admin account creation.
- **C (recommended)** — **B + A**: bootstrap admin is always 2FA (B), CP-provisioned admins get a
  forced enrolment step (A), and `JwtTokenService` emits an `amr`/`mfa` claim so CP policies can
  distinguish a TOTP-completed token from a password-only one.

**Recommendation** — **C**, rolled out **enrolment-first** so no admin is locked out.
**Scope** — `SignInService` (CP branch), `AdminAccountService.CreateAccountAsync` (force 2FA),
`Program.cs` (prod boot guard), `JwtTokenService` (mfa claim), a CP 2FA-enrolment page.
**Risk** — *breaking* — locks out unenrolled admins; ship the enrolment flow + notify admins first.
**Test** — CP sign-in without 2FA → enrolment challenge, no token; with 2FA → token carries the mfa claim.

---

## #20 — booking on a started/ended session (product-behaviour call)
**Defect / context** — reserve paths have no session-timing guard, so a visitor can create an
un-cancellable hold on a session that already started/ended. But the current design **intentionally**
allows booking a started session (`BookingApprovalTests` comment: *"the start-guard only applies to
cancellation, not booking"*). My audit fix blocked it → reverted as a behaviour change.

**Decision needed** — should booking a started or ended session be allowed?

**Options**
- A — block once **started** (`now ≥ StartUtc`). Strictest; disallows live walk-in booking.
- B — allow booking any time (status quo). The "un-cancellable hold" is harmless (seat still valid).
- **C (recommended)** — block only **ended** sessions (`now ≥ EndUtc`); a walk-in may still book a
  live, in-progress session but not a finished one.

**Recommendation** — **C** (matches the likely intent). Then update the cancel test to reserve
before start and advance the clock.
**Scope** — `SeatReservationService` create paths (guard on the chosen boundary) + `BookingApprovalTests`.
**Risk** — behaviour change; the exact rule is the owner's call.
**Test** — book a live session → allowed; book an ended session → refused; cancel-after-start → refused.

---

## #21 — concurrency-safe seat-capacity backstop
**Defect** — the post-insert capacity backstop can reject *both* racers (a free seat goes unfilled).
My deterministic rank rewrite **oversold** (3 through a capacity-2 session) → reverted.

**Decision needed** — enforce capacity correctly under concurrency (never oversell, never over-reject).

**Options**
- **A (recommended)** — wrap the reserve-random / open-seating **count-check + insert** in a
  **serializable transaction** (with retry on serialization failure). Atomic, no schema change,
  contained to `SeatReservationService`.
- B — a DB counter/constraint (per-session held-count) — open seating has no per-seat key, so this
  needs a counter table or trigger → **frozen-schema** change.
- C — in-process `SemaphoreSlim` per session — single-instance only; **breaks under scale-out**.
- D — optimistic concurrency on a session held-count with retry-on-conflict.

**Recommendation** — **A**. No schema change (stays within the freeze); correct under concurrency.
**Scope** — `SeatReservationService` reserve paths (serializable txn + bounded retry).
**Risk** — contention/perf under load; must pass the concurrency test that caught the oversell
(`Concurrent_reserve_random_never_exceeds_capacity_override`) *and* not over-reject.
**Test** — N concurrent reservers on a capacity-K session → exactly K succeed, K−N refused, no oversell.

---

## #22 — concurrent speaker double-book
**Defect** — the app-level overlap guard exists (`SpeakerHasOverlappingMeetingAsync`, half-open
interval, both accept paths → 409) and already blocks the *sequential* case (a regression test was
added). The residual is a **concurrent TOCTOU**: two accepts checked before either commits; the DB
backstop unique index is keyed on `SlotStartUtc` only, so overlapping-but-different-start slots slip through.

**Decision needed** — close the concurrent race.

**Options**
- **A (recommended)** — wrap the accept path's overlap-check + status flip in a **serializable
  transaction** (same pattern as #21-A). No schema change → within the freeze.
- B — an overlap-aware DB constraint (exclusion on `[SpeakerId, [SlotStartUtc,SlotEndUtc)]`) — SQL
  Server has no native exclusion constraint; needs a trigger or a redesigned key → **frozen-schema** + complex.

**Recommendation** — **A**. Shares the serializable-txn helper with #21.
**Scope** — `SpeakerMeetingRequestService` accept/respond path.
**Risk** — contention; add a concurrent-accept test.
**Test** — two concurrent accepts of overlapping different-start slots for one speaker → exactly one 200, one 409.

---

### Suggested sequencing
1. **#1** (blocker, small, Dev-safe) — do first; unblocks the prod-safety story.
2. **#2** (security) — needs the 2FA-enrolment flow; plan the rollout.
3. **#21 + #22** — share one serializable-transaction helper; do together.
4. **#20** — pure product decision (A/B/C); trivial once decided.

---

## Status update — 4 of 5 held items IMPLEMENTED (branch `fix/round1-held-items`, commit `c0dd1bd9`)

Owner approved the recommended options ("go"). Implemented on a **separate branch** off the
certified tip `3fbb57a3` so the 23-fix PR stays clean:

- **#1 DONE** — demo accounts gated to Development / `Seed:EnableDemoAccounts` (default false);
  `DemoPassword` default dropped. Super-admin bootstrap untouched. *(Owner ops still needed: rotate
  deployed creds + delete the plaintext cred in `txt.txt`.)*
- **#20 DONE** (option C) — booking refused only once a session has **ended**; a live session stays bookable.
- **#21 DONE** (option A) — reserve-random / open-seating capacity now inside a **serializable
  transaction** (via `CreateExecutionStrategy().ExecuteAsync`, matching the `BusinessMeetingService`
  precedent). Concurrency tests strengthened to `== cap` (no oversell AND no over-reject) — pass.
- **#22 DONE** (option A) — speaker accept overlap-check + flip inside one serializable transaction;
  concurrent double-book race closed.
- **#2 STILL DEFERRED** — enforcing CP TOTP needs the 2FA **enrolment flow** shipped first, else it
  locks out unenrolled admins. Not safe to auto-implement piecemeal.

**Verification:** build 0/0; affected classes 67/67; **full-suite regression cert vs the 48
pre-existing failures = 48/48 shared, 0 new regressions**. Branch pushed, **not merged**.
