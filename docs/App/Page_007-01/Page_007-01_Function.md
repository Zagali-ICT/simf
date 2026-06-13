# Page 007‑01 — Function (اهتماماتي · Sign up — interests)

*Last updated: 2026-06-13 — conformance pass against the as-built KSA-Project screen (D-365).*

What this screen does, the user steps, and the auth gate. Business rules are in
[Page_007-01_Logic.md](Page_007-01_Logic.md); the contract is in
[Page_007-01_API.md](Page_007-01_API.md).

> **New (D-332), rebuilt to the KSA-Project frame 505:1083 (D-365).** The interests
> step (mockup 5‑01), split out of Page 007. It **owns the single profile save** —
> the upsert carries the Page-007 draft **+** the picked interests.

## Purpose
Let a signed-in visitor pick their **interests (1–10)** and then **save the whole
profile** (the `SignUpProfileDraft` carried from Page 007 + the interests) in one
request, completing registration and moving to wait-for-approval.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **AUTH-only** — any signed-in account. **No role, no permission code** (D7). Router route 701 is in the authenticated set. |
| Token | Bearer JWT from sign-up/verify; actor from `sub` (D7) — body carries no user id. |
| Approval | Reachable before approval — this save is what completes the profile. |
| Draft gate | A direct deep-link with **no carried draft** shows a recover state (تعذر تحميل النموذج + a button back to the Page-007 form) — no lookup call is made. |

## How the user reaches it
- From **Page 007 (profile data)** → tap **التالي (Next)** (the `SignUpProfileDraft`
  is carried in memory as the route `extra`).

## Elements
| # | Element | AR | Source | Notes |
|---|---------|----|--------|-------|
| 1 | Header | اهتماماتي + back chevron | `interestsTitle` | custom header band (no Material app bar) |
| 2 | Heading + helper | اختر اهتماماتك · «اختر ما لا يقل عن واحد وبحد أقصى 10 اهتمامات تُستخدم لاقتراح أشخاص وجلسات مناسبة لك.» | `interestsChooseTitle` / `interestsHelper` | static l10n copy (Figma 505:1083) |
| 3 | Interest pills | — | **interests lookup** | `GET /app/account/interests`; two-column pill grid, multi-select, ordered by `displayOrder`; AR/EN label from each row |
| 4 | Counter | n / 10 مُختارة | `interestsCounter(n)` | live selected count |
| 5 | **متابعة (Continue)** | متابعة | `continueLabel` | one `POST /app/account/user-profile` (draft + interestIds); disabled while 0 selected or submitting |

## User steps
1. The screen opens with the Page-007 draft in memory, **pre-selects any interest ids
   already on the draft** (re-entry / edit), and calls `GET /app/account/interests`.
2. The user selects **at least 1, at most 10** pills (the counter updates; متابعة
   enables at ≥ 1; tapping an 11th is blocked with the snackbar «الحد الأقصى 10 اهتمامات»).
3. The user taps **متابعة**.
4. The app sends **one** `POST /app/account/user-profile` carrying the Page-007 fields
   **and** the picked `interestIds` (`draft.request.copyWith(interestIds: …)`); if an
   ID image was carried from Page 007, it is uploaded after the row exists
   (a failed upload is a non-blocking warning).
5. On `ApiResult.Ok` → toast «تم حفظ الملف الشخصي» (or the upload-failed warning) and
   the app routes to **Page 010 (registration success)** with the issued
   **`referenceNumber`** as the route extra (D-373).
6. On a server error → the bilingual `error.message` shows inline under the counter
   and the user stays on the screen with the selection intact.

## Navigation
- **In:** from **Page 007 (Next)** with the `SignUpProfileDraft` as `extra`.
- **Out (success):** **Page 010** (registration success — renders the reference
  number), then **Page 011** (registration status).
- **Out (back):** the chevron pops back to **Page 007** (falls back to
  `/sign-up/visitor` when there is nothing to pop); the draft is preserved in memory
  so the user can edit and return. Back is disabled while submitting.
- **Out (no draft):** the recover state's button goes to **Page 007** (`/sign-up/visitor`).

## Acceptance criteria
- AC1 — Renders only for a signed-in account; an anonymous open is impossible (route 701 auth gate).
- AC2 — The pills populate from the lookup; an empty lookup shows «لا توجد اهتمامات», not an error; a lookup failure shows the message + a Retry button.
- AC3 — **متابعة is disabled until ≥ 1 interest is picked**; an 11th pick is blocked with the max-10 snackbar.
- AC4 — متابعة fires **exactly one** `POST /app/account/user-profile` carrying the Page-007 draft **and** `interestIds`; there is no separate interests write.
- AC5 — On success the user sees the saved toast and **Page 010** with the issued reference number; the account is profile-complete, awaiting approval.
- AC6 — On a server error the user stays on the screen with the selection intact (inline message).
- AC7 — Full **RTL** in Arabic; labels from l10n resources + lookup rows.
- AC8 — A draft-less deep link shows the recover state routing back to Page 007 — it never submits.
