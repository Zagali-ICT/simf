# SIMF - Session Handoff (continue here)

Written: 2026-07-20 by the prior AI session. This file is the single place to
read the remaining work and continue. It is grounded in verified git state, not
memory. This file is UNTRACKED on purpose (do not commit it).

Repo: `D:\SIMF\System\V1.0.0`  |  main branch: `main`  |  `origin/main` tip when
written: `9dad156f` ("Merged PR 118: pr").

Read `CLAUDE.md` (project) and `~/.claude/CLAUDE.md` (global rules) BEFORE editing
anything. The HARD RULES section at the bottom of this file is the short version.

---

## 1. STATUS: all recent code is MERGED. Only deploy + 2 decisions remain.

Verified with `git merge-base --is-ancestor <sha> origin/main` after `git fetch`:

| Work | Commit(s) | On origin/main? |
|------|-----------|-----------------|
| Slice D - AI transparency (AI-draft snapshot + raw subtitle in محضر editor) | `25b16c31` | YES |
| JobTitle bilingual backend (contact cards, VIP Excel, vCard, profile API) | `6b61caa5` | YES |
| Speaker-rank bilingual + `UserProfile.JobTitleArabic` EF migration `20260719180333_AddUserProfileJobTitleArabic` | `d1774363` | YES |
| Swagger secure-by-default (`appsettings.json` AllowSwagger:false) | `288e2a7b` | YES |
| CP `monitor` SimfIcon fix (was crashing the CP circuit on the Session Live Hall nav) | `f2c92a77` | YES |
| Bugs & updates working tracker doc | PR 117 | YES |

There is NO pending merge / PR work. Do not re-open PRs for the above.

---

## 2. PRODUCTION: code is on main, the box just needs a redeploy (OWNER action)

The two live incidents from 2026-07-20 are FIXED IN CODE and merged. They take
effect only after a redeploy on the server:

1. **CP circuit crash** (`System.ArgumentException: Unknown SimfIcon name 'monitor'`).
   Fix `f2c92a77` is compiled into `SIMF.Components`. ACTION: rebuild + redeploy
   the Control Panel from `origin/main`.

2. **API Swagger boot crash** (`Swagger:Username and Password must be configured`).
   Root cause: default hosting env is Production + `AllowSwagger:true` + empty
   creds = the one combination `Program.cs` rejects at boot. Source default is now
   `false` (`288e2a7b`); the box overrides via env. ACTION: redeploy the API from
   `origin/main`.

3. **Swagger credentials updated** (done 2026-07-20). `deploy/set-env-api.ps1`
   (GIT-IGNORED, real-secret overlay, lives ONLY in the primary dir) now sets:
   - `SIMF_Swagger__AllowSwagger = true`
   - `SIMF_Swagger__Username = simfadmin`
   - `SIMF_Swagger__Password = <redacted — real value only in git-ignored deploy/set-env-api.ps1>`
   NOTE: that password originated as a throwaway example typed in chat, so it has
   appeared in a transcript. Low risk for a /swagger docs gate, but the owner may
   want to swap it (one-line edit at `deploy/set-env-api.ps1:181`).
   ACTION (owner, as Administrator): run `deploy\set-env-api.ps1`, then restart the
   API IIS app pool so `w3wp` re-reads the Machine-scope vars. Verify `/swagger`
   prompts for Basic auth with `simfadmin` + the new password.

4. **Deploy the additive migrations** (owner). Both are additive/nullable, safe:
   - `App/20260719180333_AddUserProfileJobTitleArabic` (UserProfile.JobTitleArabic)
   - Slice D `AddSessionSummaryAiDraftSnapshot` (2 nullable columns on SessionSummary)
   They apply on the App DB (`SimfAppDbContext`). Identity DB is frozen/untouched.

---

## 3. REMAINING WORK ITEMS

### Blocked on an OWNER DECISION (do not start without a yes)

- **Slice 3 FORM - app Arabic job-title input.** Backend is done + merged. What is
  left is adding the Arabic-title field to TWO Flutter screens: visitor sign-up and
  the staff "register visitor" screen. Both are GOLDEN-LOCKED; adding the field
  re-baselines 2 approved golden tests. Needs an explicit nod because it changes
  approved goldens. Files: `src/Mobile/simf_app/lib/features/.../sign_up_visitor*`
  and the staff register-visitor screen (search for the register-visitor form).
  Pattern to follow: the bilingual `localizedJobTitle(isArabic)` getter already in
  `src/Mobile/simf_app/lib/features/contacts/data/contact_models.dart`.

- **Slice 4 (#6) - subtitle source store.** New CP-internal column
  `Session.SourceTranscript` (own branch + additive migration), wire it into the
  summary Generate step, and show it in the CP editor panel. DESIGN DECIDED: it is a
  NEW internal column - do NOT widen the public `LiveCaptions` wire field (append-only
  contract, D-219). Awaiting the owner's go to build.

### Needs WIP-handling INPUT before touching

- **worker-ops monitor + local-dir sync.** The bugs-tracker DOC is merged (PR 117),
  but the CP background-services monitor CODE (`/admin/ops/services`, `/health`
  worker checks, `SIMF.Workers` logging, `deploy/ops.ps1`) may still be uncommitted
  WIP intermixed with the owner's own SessionLiveHall work. DO NOT `git add -A`.
  Ask the owner how to split it, then stage ONLY the intended files. See memory
  `simf-worker-ops-monitor-2026-07-18.md`.

### OWNER-ONLY (an AI cannot do these)

- Run the redeploys + migrations in section 2.
- CP data task: re-enter the head-of-delegation (delegate) titles so the new
  bilingual JobTitle shows for existing delegates.
- iOS liveness device-test: branch `fix/ios-liveness-direction` (`55bd5806`,
  worktree `D:/SIMF/wt-liveness`) is pushed; needs a physical iPhone.

---

## 4. CONTEXT MAP (worktrees + where things live)

Active feature worktrees (each is its own branch off main):

| Worktree | Branch | Purpose |
|----------|--------|---------|
| `D:/SIMF/System/V1.0.0` | `feat/worker-ops-monitor` | PRIMARY dir. Holds the git-ignored `deploy/set-env-api.ps1`. |
| `D:/SIMF/wt-jobtitle-i18n` | `feat/jobtitle-bilingual` | JobTitle bilingual program (merged). |
| `D:/SIMF/wt-speaker-rank` | `feat/speaker-rank-bilingual` | Delegation title + migration (merged). |
| `D:/SIMF/wt-summary-ai-transparency` | `feat/summary-ai-transparency` | Slice D (merged). |
| `D:/SIMF/wt-swagger-fix` | `fix/swagger-prod-boot` | Swagger default + monitor icon (merged). |
| `D:/SIMF/wt-liveness` | `fix/ios-liveness-direction` | iOS liveness (pushed, needs device test). |

Key files for the remaining work:
- Two-DB entities: App on `SimfAppDbContext`, Identity on `SimfIdentityDbContext`.
- Session summary service: `src/Backend/SIMF.Application/.../AdminSessionSummaryService.cs`.
- Profile contract: `src/Shared/SIMF.Contracts/UserProfile/UserProfile.cs`.
- Deploy overlay (secrets, git-ignored): `deploy/set-env-api.ps1` (primary dir only).

---

## 5. HARD RULES the next session MUST obey (short form; full rules in CLAUDE.md)

- **Plan first, then STOP for approval** before ANY code edit (global §11 format).
  Never suggest + implement in one step. When in doubt, ASK - never guess.
- **Never push to `main`.** Feature branches only; merge via PR (Azure DevOps,
  `dev.azure.com/Zagali-KSA/SIMF`). No `gh`/`az` CLI on this box.
- **Targeted staging only.** NEVER `git add -A`. Stage only your files; verify with
  `git diff --cached`. Serialize EF migrations.
- **Review agents + `simplify` BEFORE every commit/push** (global §17), not just at
  sprint boundaries. A "small fix" is not an exemption.
- **Verify "done".** Run build + tests and paste real output; for UI, a live render,
  not "it should work".
- **Two-DB split (D-157) is permanent.** No cross-DB FK/JOIN/duplicated data. A
  cross-DB reference is a bare `Guid` resolved with a second query.
- **Append-only mobile wire contract (D-219).** New DTO fields are appended; never
  rename/reorder/remove a field the shipped app decodes. Bilingual pattern = add a
  `*Arabic` twin field.
- **Identity schema + enums are FROZEN (D-110).** Additive App tables/columns are
  allowed under the named freeze-lifts; Identity changes need explicit owner approval.
- **No em-dash / en-dash / ellipsis in deliverable docs.** Use hyphens; `→`/`×` are ok.
- **Secrets:** never print/commit/exfiltrate. `deploy/set-env-api.ps1` is git-ignored;
  inspect before overwriting any existing credential (global §14).
- **Every new page/action needs:** its permission (PermissionCatalog) gating API+CP,
  its E2E catalogue file under `docs/tests/e2e/`, and docs + unit/integration tests
  in the SAME changeset (D-246).

---

## 6. Verify the current state yourself (paste-ready)

```bash
cd "D:/SIMF/System/V1.0.0" && git fetch origin
git log --oneline -3 origin/main
# confirm the merges:
for s in 25b16c31 6b61caa5 d1774363 288e2a7b f2c92a77; do \
  git merge-base --is-ancestor $s origin/main && echo "$s merged" || echo "$s UNMERGED"; done
git worktree list
```
