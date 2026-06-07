# SIMF — Remediation & QA Plan

Last updated: 2026-06-08 · Owner-requested (a real development plan with
**testing / checking / validating / review** built in as gates, not after-thoughts).

This plan governs the remaining "finish + verify everything" push. It is a
**process**, not a feature list: nothing is "done" until it passes the gate
below, and every delivery leads with the gate's **evidence**.

---

## The gate — Definition of Done for EVERY change

Run in order; skip nothing (if a step is genuinely N/A, say so):

1. **Unit tests** written + green.
2. **Integration tests** green.
3. **Build clean** — `dotnet build -c Release` 0 warnings / 0 errors (or
   `flutter analyze` + `flutter test` clean for the app).
4. **LIVE DOM / browser verification** — open the *running* page and look:
   full-page **screenshot** + **console** (zero errors) + **network** (zero
   failed/broken assets — 404s, broken `<img>`) + **DOM** check
   (`scrollWidth == clientWidth` → no horizontal overflow; no broken images;
   layout matches the mockup). **Compiling ≠ rendering** — only a live render
   proves a page works.
5. **E2E** — run that page's catalogue scenarios under `docs/tests/e2e/`.
6. **Review** — review agents + the `simplify` skill; address findings.
7. **Docs** — PAGE-INDEX + per-page reference + E2E catalogue updated in the
   **same** changeset.

(Mirrors global `~/.claude/CLAUDE.md` §17 and the `delivery-verification-gate`
memory.)

---

## Phases

| # | Phase | Scope | Gate focus |
|---|-------|-------|-----------|
| 0 | **QA baseline** | This doc + the defect register below. No feature code. | Owner approval of the plan. |
| 1 | **Web remediation** | Fix visible web defects; validate **all 12 landing sections** (desktop + mobile + RTL) against the mockup; confirm each renders real CMS data. | Per section: live screenshot + console-clean + network-clean; run `web-landing.md`; design diff. |
| 2 | **Backend image pipelines** | Per entity (speaker, sponsor, booth, media-partner, news, archive): upload endpoint + out-of-row byte storage + anonymous serving endpoint + sample seed. | API tests + live `curl` returns bytes; CP upload works. |
| 3 | **App image rendering** | Wire speaker photos/flags + logos + covers (reuse the proven gallery pattern, unblocked by Phase 2). | `flutter analyze` + `test` + run app vs backend + per-screen screenshots. |
| 4 | **Full regression** | Drive **every** CP + Web + App page via the E2E catalogue. | Review agents + reality-check sign-off; defect register at zero. |

---

## Defect register

Live, evidence-based. `OPEN` until fixed **and** re-verified through the gate.

| ID | Surface | Defect | Found by | Status |
|----|---------|--------|----------|--------|
| WEB-001 | Website landing — About section | `.about-img` referenced `assets/about-image.png`, which was never added → **404**, empty box. | Live browser check 2026-06-08 (console 404 + network) | **FIXED + re-verified** (D-343) — repointed to the on-brand `assets/neom.avif` + a `--navy` fallback; live gate shows `neom.avif` 200, About renders the image. |
| WEB-003 | Website landing — `<head>` | No favicon link → browser fell back to `/favicon.ico` → **404**. | Live network check 2026-06-08 | **FIXED + re-verified** (D-343) — added `<link rel="icon" href="assets/logo-simf.png">`; live gate shows `logo-simf.png` 200, no `/favicon.ico` request, **console clean, zero failed requests**. |
| WEB-002 | Website landing — sessions / speakers / partners / news | Cards render **without images** (text-only / placeholder tiles). | Live DOM check 2026-06-08 | **OPEN — backend-blocked** (no image pipeline for these entities; see D-342). Tracked under Phase 2. |
| APP-001 | Flutter — speaker photos, country flags, sponsor/booth/partner logos, news/archive covers | No images render (vestigial `*RelativePath` fields, no upload/storage/serving). | Backend audit 2026-06-07/08 (D-342) | **OPEN — backend-blocked.** Tracked under Phase 2 → 3. |

(Gallery tile bitmaps — Page 030 — shipped D-342 and are not a defect.)
