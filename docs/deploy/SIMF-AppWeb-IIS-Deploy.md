# SIMF App — Web build deployment on IIS (D-376)

Last updated: 2026-06-12

The Flutter app's **web build** is published as a static IIS site (proof-of-
concept distribution of the mobile app's UI in a browser; the shipped product
targets remain Android + iOS per SIMF-MAA-001 §2). The published build calls
the production API at **`https://simf_api.zagali-ict.com/api/v1`**.

## 1. Produce the deploy folder

```powershell
D:\SIMF\System\V1.0.0\deploy\app-web\publish-app-web.ps1 `
    -ApiBase "https://simf_api.zagali-ict.com/api/v1" `
    -OutDir  "D:\SIMF\Publish\simf-app-web"
```

The script builds `simf_app` for web (`--release`, `SIMF_BUILD=prod` — request
logging off) with the API base **compiled in**, and assembles the output +
`web.config` into `-OutDir`. Changing the API URL requires a rebuild — there
is no runtime config file. Optional parameters: `-AppKey`, `-SupportPhone`,
`-SupportEmail` (the close-out contact tiles stay inert while empty, D-369).

## 2. IIS site

1. Create an IIS **website** (or application) whose physical path is the
   deploy folder. No application pool .NET runtime is needed — set the pool
   to **No Managed Code** (pure static content).
2. The bundled `web.config` registers the MIME types Flutter needs
   (`.wasm`, `.json`, `.bin`, fonts, `.mp4`) and sets `index.html` as the
   default document. The app uses **hash routing** (`/#/route`), so no
   URL-Rewrite module is required.
3. Bind **HTTPS**. A browser page served over `https://` cannot call an
   `http://` API (mixed content), so the site and the API must both be HTTPS.

## 3. API-side requirements

1. **CORS (only if the site origin ≠ the API origin).** Browsers block
   cross-origin calls unless the API allows the site's origin. The API reads
   an explicit allow-list from configuration (empty = no CORS, the default
   posture):

   ```json
   "Cors": {
     "WebAppOrigins": [ "https://<the-iis-site-host>" ]
   }
   ```

   Set this in the **published API's** `appsettings.json`, or as the
   Machine-scope environment variable `SIMF_Cors__WebAppOrigins__0`
   (the `SIMF_` prefix convention, D-355 — see `deploy/set-env-api.ps1`),
   then restart the API app pool.
   If the web app is hosted on the **same origin** as the API (e.g. the API
   site also serves the static folder under `/`), no CORS entry is needed.
2. The API must be reachable at the compiled-in base URL with a valid
   certificate.

## 4. Known caveats

- **Hostname `simf_api` contains an underscore.** Public CAs do not issue
  TLS certificates for hostnames with `_` (RFC 952/1123 hostnames allow
  letters/digits/hyphens only). If certificate issuance fails, the host
  should be renamed (e.g. `simf-api.zagali-ict.com`) and the web app
  rebuilt with the corrected `-ApiBase`.
- **Web storage is not a secure enclave.** On web, tokens live in browser
  storage (`flutter_secure_storage` has no Keychain/Keystore there) — this
  distribution is a proof of concept, not the NCA-hardened channel; the
  hardened channel remains the native app.
- The camera capture (C7 male-photo) uses the browser camera via
  `image_picker`'s web implementation; behaviour depends on the browser's
  permission prompts.

## 5. Smoke test after deploy

1. Open the site root — the splash must route to onboarding/sign-in.
2. Register / sign in end-to-end (watch the browser network tab: all calls
   go to `https://simf_api.zagali-ict.com/api/v1/...`, no CORS errors).
3. `docs/tests/e2e/mobile-sign-in.md` scenarios are the full catalogue.
