# Rebuilding the Control Panel operations manual

The manual is generated. Nothing in either volume is typed into Word, and every
screenshot is captured from a running Control Panel, so a rebuild is how the
book is corrected — not editing the `.docx`.

Output:

| File | What it is |
|---|---|
| `docs/manuals/SIMF-CP-Operations-Manual-EN.docx` / `.pdf` | the English volume |
| `docs/manuals/SIMF-CP-Operations-Manual-AR.docx` / `.pdf` | the Arabic volume |

The two are separate volumes on purpose: a single Word file paginates in one
direction only, and the book was asked for as English opening from the left and
Arabic opening from the right. Bound head-to-head, the two files are that book.

## Rebuild the book only

When the text changed but the screens did not:

```bash
python tools/manual/build_page_model.py   # re-read routes, permissions, labels
python tools/manual/make_book.py          # re-author book.json from the content files
python tools/manual/build_manual.py       # render both volumes
```

`build_manual.py` **fails** if any referenced screenshot is missing, rather than
emitting a book with a hole in it. That guard exists because the manual this one
supplements carries two image references whose files never existed.

Then refresh the tables of contents and export the PDFs (Word resolves the TOC
fields; python-docx only writes them):

```powershell
$dir = "docs\manuals"
$word = New-Object -ComObject Word.Application
$word.Visible = $false
foreach ($n in @("SIMF-CP-Operations-Manual-EN","SIMF-CP-Operations-Manual-AR")) {
  $doc = $word.Documents.Open("$PWD\$dir\$n.docx", $false, $false)
  $doc.Fields.Update() | Out-Null
  $doc.Repaginate()
  $doc.SaveAs2("$PWD\$dir\$n.pdf", 17)
  $doc.Save(); $doc.Close()
}
$word.Quit()
```

## Recapture the screenshots

Screenshots come from a dedicated environment so the working databases are never
touched. `doc-env.ps1` creates `SIMF_Identity_Doc` and `SIMF_App_Doc`, generates
throwaway keys into `.tmp/manual-env/` (gitignored), and starts both hosts.

```powershell
dotnet build src/Backend/SIMF.Api/SIMF.Api.csproj -c Release
dotnet build src/ControlPanel/SIMF.ControlPanel/SIMF.ControlPanel.csproj -c Release
.\tools\manual\doc-env.ps1 -Reset        # -Reset drops and recreates the databases
```

Then run the capture. It signs in through the real form with a real
authenticator code, so it exercises the same path a person does:

```powershell
. .\.tmp\manual-env\secrets.local.ps1
$env:SIMF_MANUAL_CP_URL      = "http://localhost:5158"
$env:SIMF_MANUAL_EMAIL       = "superadmin@simrsnf.com"
$env:SIMF_MANUAL_PASSWORD    = $DocPassword       # or $DocNewPassword once changed
$env:SIMF_MANUAL_TOTP_SECRET = $DocTotpSecret
$env:SIMF_MANUAL_OUT         = "$PWD\docs\screenshots\manual"
$env:SIMF_MANUAL_ROUTES      = "$PWD\tools\manual\routes.tsv"

foreach ($lang in @("en","ar")) {
  $env:SIMF_MANUAL_LANG = $lang
  dotnet test tests\SIMF.E2E.Tests -c Release --no-build `
    --filter "FullyQualifiedName~ManualCapture.Capture_route_sweep"
}
```

`Capture_account_flows` photographs the create forms step by step and needs the
button labels for the language it is running in
(`SIMF_MANUAL_ADD_LABEL`, `SIMF_MANUAL_ADD_VIP_LABEL`, `SIMF_MANUAL_SUBMIT_LABEL`).

Stop the environment with `.\tools\manual\doc-env.ps1 -Stop`.

### Two routes need a record id

`/admin/roles/{RoleId:guid}/permissions` and `/sessions/{SessionId:guid}/moderate`
cannot be visited as written. Put a concrete route for each in
`.tmp/manual-env/route-ids.tsv` (gitignored) and re-run `build_page_model.py`;
it reports `unresolved parameterised` when one is missing, and the manual build
then fails on the absent screenshot rather than skipping the page.

## Where the content lives

| File | Holds |
|---|---|
| `make_book.py` | signing in, creating a user, the profile picture |
| `content_accounts.py` | changing an account, roles and permissions |
| `content_ops.py` | deployment, every configuration value, the appendix |
| `docx_kit.py` | the right-to-left Word primitives |
| `build_page_model.py` | the extraction that produces the page reference |

Two rules govern the content:

1. **Interface words come from the application.** `L("Admin.CreateUser.Email")`
   resolves the English and Arabic strings out of `Strings.resx` and
   `Strings.ar.resx`, so a renamed button is renamed in both volumes on the next
   build and nothing is hand-translated.
2. **No secret value appears in the book** — names, purposes, defaults, and the
   commands that generate the keys, never a key.

## Verifying a rebuild

Every capture run writes `capture-report-*.json` beside the screenshots with the
console errors, failed requests and horizontal overflow recorded per page. A
clean run is zero of each; the one known exception is a not-found on the
organisation logo, which has simply never been uploaded on a fresh database.
