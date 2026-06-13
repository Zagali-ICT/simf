# Page 010 — API (تم التسجيل بنجاح · Registration success)

Backend contract for this page. Inherits the `ApiResult<T>` envelope, headers,
error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Behaviour rules
are in [Page_010_Logic.md](Page_010_Logic.md).

> Last updated: 2026-06-13 — conformance pass to the as-built code (D-366 / D-369 / D-373).
>
> **Status:** transitional confirmation screen — as built it makes **zero API
> calls**. The account + profile were already created upstream; everything on
> this screen renders from the route **extra** and compile-time config. The
> optional status poll the earlier draft described was **never wired** — the
> status read belongs to Page 011, which calls it itself.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247).

## Does this screen have its own API?
**No.** `RegistrationSuccessScreen` is a `StatelessWidget` with no repository or
client import. It does not fetch, poll, or write anything:

- **Reference number (D-373):** the real DB-issued `SIMF-YYYY-NNNNNNNN`
  registration reference is returned by the **Page 007-01 save**
  (`POST /api/v1/app/account/user-profile` → `UserProfileResponse.referenceNumber`)
  and carried here as the go_router route **extra** (a `String`). No fetch from
  this screen; with no extra (offline / out-of-flow arrival) the literal mask
  `SIMF-2026-xxxx` renders instead.
- **Contact tiles (D-369):** no HTTP — the tiles launch OS-level `tel:` /
  `mailto:` URIs via `url_launcher`, gated on `BuildConfig.supportPhone` /
  `supportEmail` (`--dart-define` `SIMF_SUPPORT_PHONE` / `SIMF_SUPPORT_EMAIL`;
  empty value = inert tile).
- **Buttons:** pure client navigation (Page 011 / home).

## Reused / related endpoints (not called from this screen)
| Endpoint | Relation |
|---|---|
| `POST /api/v1/app/account/user-profile` (Page 007-01 upsert/save) | Creates the profile and issues + returns `referenceNumber` (D-373); its **success** is what brings the user here, with the reference passed as the route extra. Owned by Page 007/007-01. |
| `GET /api/v1/app/users/me` (`CurrentUserEndpoint`, D-249) | Page 011's endpoint — Page 011 reads `registrationStatus` itself (on entry + Re-check). Page 010 only **navigates** there; it never calls this endpoint. |
| Home screen | The outlined/secondary navigation target. Pure client navigation — no call made from this screen. |

## Wire reference (context only — decoded upstream, on Page 007-01)
The save response this screen's extra originates from includes:

```jsonc
// UserProfileResponse (excerpt — decoded by the Page 007-01 save, not here)
{
  "referenceNumber": "SIMF-2026-00000001"   // string?, issued once at profile creation (D-373)
}
```

## Error codes
Not applicable — this screen issues no requests, so it surfaces no API errors.
A failed `tel:`/`mailto:` launch is swallowed (best-effort, D-369 contract).

## Build dependencies
- **None.** The base confirmation renders fully offline. The reference card
  depends only on the upstream Page 007-01 save having returned
  `referenceNumber` (D-373); the contact tiles depend only on the
  `SIMF_SUPPORT_PHONE` / `SIMF_SUPPORT_EMAIL` dart-defines being supplied at
  build time (D-369 — still an open owner input on the redesign board).
