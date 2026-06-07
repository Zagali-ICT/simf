# Page 007‑01 — Logic (اهتماماتي · Sign up — interests)

Client + server logic, the 1–10 rule, the combined save, and edge handling. The
contract lives in [Page_007-01_API.md](Page_007-01_API.md); the user flow in
[Page_007-01_Function.md](Page_007-01_Function.md).

> **New (D-332).** Interests screen + the **single** profile upsert.

## L-1 — Auth gate
AUTH-only. Bearer token required; **no role, no permission code, not approval-gated,
not `AllowAnonymous`** (D7). The interests lookup and the upsert sit in the `auth`
rate-limit bucket; the actor is the `sub` claim — the body never carries a user id.

## L-2 — Lookup
`GET /app/account/interests` → active interests, ordered by `DisplayOrder` then `Name`
(D-050). An empty list shows the empty state, never a blocking error.

## L-3 — The 1–10 rule (client mirrors server)
- Required: **min 1, max 10**, **distinct**, all **active** ids
  (`UpsertUserProfileRequestValidator`).
- **Save** is disabled until ≥ 1 is selected; the UI prevents an 11th selection.
- The server re-validates (duplicate / unknown / deactivated id → `Validation.Failed`).

## L-4 — The combined save (the only write in the sign-up flow)
On **Save** the app fires **one** `POST /app/account/user-profile` carrying:
- the **profile fields collected on [Page 007](../Page_007/README.md)** (names,
  nationality, DOB, gender, organisation, job title, identity-doc, mobiles, the picked
  `profileTypeId`), **and**
- the picked **`interestIds`**.

There is **no separate interests write** — interests persist through this upsert (D7 /
D-050). If an **ID-document image** was picked on Page 007, it is uploaded
(`POST` multipart) **after** the profile row exists. The upsert is idempotent.

## L-5 — State transitions
```
interests-empty ──[pick 1–10]──▶ save-ready
save-ready ──[Save → POST upsert → ApiResult.Ok]──▶ profile-complete ──▶ "please wait" / Confirmation (Page 010)
save-ready ──[POST → Validation.Failed]──▶ stay on screen (field/toast)
```
A successful save marks the profile complete; the account moves to wait-for-approval
(`Page_010` → `Page_011`).

## L-6 — Error / empty / RTL handling
- **Empty interests lookup:** show the empty state (cannot proceed until seeded — a seed-data dependency, not a screen error).
- **Validation error from the upsert:** map the code to the offending field; if the error is in a Page-007 field, surface it and let the user go **Back** to fix it (the form state is preserved).
- **Network / 500:** retry toast; the selection + carried form state are preserved.
- **RTL:** Arabic primary; the interests grid + counter mirror; AR/EN labels from each lookup row.

## L-7 — Dependencies
- **[Page 007](../Page_007/README.md)** supplies the in-memory form state this screen saves.
- Interests lookup data must be seeded (D-050).
- The shipped `POST /app/account/user-profile` (D-046b / D-049) is the single save.
