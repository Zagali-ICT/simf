# Page 007‑01 — Logic (اهتماماتي · Sign up — interests)

*Last updated: 2026-06-13 — conformance pass against the as-built KSA-Project screen (D-365).*

Client + server logic, the 1–10 rule, the combined save, and edge handling. The
contract lives in [Page_007-01_API.md](Page_007-01_API.md); the user flow in
[Page_007-01_Function.md](Page_007-01_Function.md).

> **New (D-332), rebuilt to KSA frame 505:1083 (D-365 — visuals only, contract
> byte-identical).** Interests screen + the **single** profile upsert.

## L-1 — Auth + draft gate
AUTH-only. Bearer token required; **no role, no permission code, not approval-gated,
not `AllowAnonymous`** (D7). The interests lookup and the upsert sit in the `auth`
rate-limit bucket; the actor is the `sub` claim — the body never carries a user id.
Client-side, route 701 (`/sign-up/interests`) is in the router's authenticated set,
and the screen additionally requires the **carried `SignUpProfileDraft`**: a
draft-less open (direct deep link) renders the recover state back to Page 007 and
never calls the API.

## L-2 — Lookup
`GET /app/account/interests` → active interests, ordered by `DisplayOrder` then `Name`
(D-050). Runs only when a draft was carried. Any interest ids already on the draft are
pre-selected, then **ids no longer in the active lookup are dropped**
(`retainWhere`). An empty list shows the empty state («لا توجد اهتمامات»), never a
blocking error; an `ApiFailure` shows the message + a Retry button that re-runs the
lookup.

## L-3 — The 1–10 rule (client mirrors server)
- Required: **min 1, max 10**, **distinct**, all **active** ids
  (`UpsertUserProfileRequestValidator` + service re-check).
- **متابعة (Continue)** is disabled until ≥ 1 is selected (and while submitting);
  tapping an 11th pill is blocked with the snackbar «الحد الأقصى 10 اهتمامات»
  (`interestsMaxReached`).
- A guard in `_save` also refuses to submit outside 1–10.
- The server re-validates (not 1–10 / duplicate / unknown / deactivated id →
  `Validation.Failed`).

## L-4 — The combined save (the only write in the sign-up flow)
On **متابعة** the app fires **one** `POST /app/account/user-profile` carrying:
- the **profile fields collected on [Page 007](../Page_007/README.md)** (names,
  nationality, DOB, gender, organisation, job title, identity-doc, mobiles, plate
  number, the picked `profileTypeId`) — i.e. `draft.request`, **and**
- the picked **`interestIds`** attached via `copyWith`.

There is **no separate interests write** — interests persist through this upsert (D7 /
D-050). If an **ID-document image** was carried from Page 007, it is uploaded
(`POST` multipart) **after** the profile row exists; an upload failure is
**non-blocking** — the save succeeded, so the app shows the warning toast
(`idImageUploadFailed`) instead of the saved toast and still proceeds. The upsert is
idempotent. The response's **`referenceNumber`** (D-373) is passed to Page 010 as
the route extra.

## L-5 — State transitions
```
no-draft (deep link) ──▶ recover state ──[button]──▶ Page 007 (/sign-up/visitor)
draft carried ──[GET interests]──▶ loading ──▶ ready (pre-selected draft ids retained)
loading ──[ApiFailure]──▶ load-error ──[Retry]──▶ loading
ready (0 selected) ──[pick 1–10]──▶ save-ready (متابعة enabled)
save-ready ──[متابعة → POST upsert → Ok (+ optional id-image upload)]──▶ toast ──▶ Page 010 (extra = referenceNumber)
save-ready ──[POST → ApiFailure]──▶ stay on screen (inline message under the counter)
```
A successful save marks the profile complete; the account moves to wait-for-approval
(`Page_010` → `Page_011`).

## L-6 — Error / empty / RTL handling
- **Empty interests lookup:** show the empty state (cannot proceed until seeded — a seed-data dependency, not a screen error).
- **Lookup failure:** show `ApiFailure.message` + a **Retry** button.
- **Upsert failure (validation / 429 / 500):** show the bilingual `error.message` **inline in red under the counter**; the selection + carried draft are preserved, and the **Back** chevron returns to Page 007 to fix a Page-007 field.
- **ID-image upload failure:** non-blocking — warning toast, flow continues to Page 010.
- **RTL:** Arabic primary; the pill grid + counter mirror with the locale; AR/EN labels from each lookup row (`nameArabic` / `name`).

## L-7 — Dependencies
- **[Page 007](../Page_007/README.md)** supplies the in-memory `SignUpProfileDraft` this screen saves.
- Interests lookup data must be seeded (D-050).
- The shipped `POST /app/account/user-profile` (D-046b / D-049) is the single save; its response carries `referenceNumber` (D-373) consumed by **Page 010**.
