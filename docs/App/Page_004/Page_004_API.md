# Page 004 — API (إنشاء حساب — النوع · Sign up — type)

Backend contract for this page.

## There is no API for this screen
This screen has **NO SIMF API call.** The account-type choice is a **client-only UI
gate** resolved entirely on the device:

- No request is issued on entry, on selection, or on **Continue**.
- No `Authorization` header, no token, no `ApiResult<T>` round-trip — nothing reaches
  `/api/v1/app/*`.
- The type list (Visitor enabled; Exhibitor/Sponsor disabled/info) is a **static
  in-code constant**, not a server lookup.

The screen runs at **Guest** privilege and behaves identically offline (see
[Page_004_Logic.md](Page_004_Logic.md), Edge cases).

## Why
**App accounts are Visitor-only;** exhibitor and sponsor are **Control-Panel concepts**
(CP-managed Company + accounts, D-199), never self-registered from the App. So there is
nothing for the server to decide here — the only valid outcome is `type = Visitor`,
which is forwarded **in memory** to the next screen.

## Where the visitor path goes next
On **Continue** the App navigates to **Page 005 (`/sign-up/form`)** carrying
`type = Visitor`. The **actual account creation** (the first and only backend call of
the sign-up flow) happens **on Page 005**, not here.

| | |
|---|---|
| This screen (004) | **No API.** |
| Next screen (005) | Visitor sign-up submission → the registration endpoint (documented in `docs/App/Page_005/Page_005_API.md`). |

> If a future requirement makes account types **server-driven** (e.g. an endpoint that
> returns which types are open for self-registration), that would be a new
> **`GET /api/v1/app/account/sign-up-types` (TO BUILD)** read returning
> `ApiResult<IReadOnlyList<SignUpTypeOption>>`. **Not built and not planned** — recorded
> here only so a later reader knows it was considered and deliberately omitted.
