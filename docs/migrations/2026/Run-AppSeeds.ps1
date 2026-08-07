<#
.SYNOPSIS
    Runs the SIMF_App 2026 content seeds, in order, from a terminal.

.DESCRIPTION
    D-845 — the companion to Run_All_App_Seeds.sql, which only works inside SSMS
    with SQLCMD Mode switched on (it uses :setvar / :r / :on error). That left no
    supported way to seed from a terminal or a deploy script, and the obvious
    improvisation — `sqlcmd -i SIMF_App_Programme.sql` — FAILS:

        Msg 2628 ... String or binary data would be truncated in column 'NmAr'.
        Truncated value: 'Ø¯ÙˆØ± Ø§Ù„Ø°ÙƒØ§Ø¡ ...'

    sqlcmd reads an input file in the system ANSI codepage unless the file
    carries a BOM or -f is given. Arabic UTF-8 bytes then arrive as two or three
    Latin-1 characters each, so every Arabic string silently doubles in length
    and overflows its column. The seeds now carry a UTF-8 BOM, which fixes this
    for every tool; this script ALSO passes -f 65001 so it stays correct even if
    a file is ever re-saved without one.

    Each seed is idempotent, so re-running is safe. -b makes sqlcmd exit non-zero
    on the first error, and this script stops there — no partial content.

.PARAMETER Server
    SQL Server instance. Default "." (local default instance).

.PARAMETER Database
    The App / CONTENT database — the one holding dbo.Speakers / dbo.Halls.
    Local dev: SIMF_App. Server: SIMF_Data. NEVER the Identity database.

.EXAMPLE
    .\Run-AppSeeds.ps1
    .\Run-AppSeeds.ps1 -Server "PROD\SQL01" -Database SIMF_Data

.NOTES
    NOT run here (deliberately):
      * SIMF_App_RegistrationReferenceSequence_Hotfix.sql — a prod-only unblock
        for a database missing the sequence; run by hand when that applies.
      * SIMF_App_AssistancePrompt*.sql — one-shot updates that re-point an
        already-seeded AI prompt; a fresh database already has the right template.
      * The speaker-photo BYTES — after this completes, copy
            speaker-photos\speakerphoto  ->  <FileStorage:RootPath>\speakerphoto
        (production: C:\SIMF\Storage\files\speakerphoto). A StoredFile row whose
        bytes are missing simply 404s the photo.
#>
[CmdletBinding()]
param(
    [string] $Server   = '.',
    [string] $Database = 'SIMF_App'
)

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "sqlcmd is not on PATH. Install the SQL Server command line utilities."
    exit 1
}

# -C ("trust server certificate") only exists in the ODBC-era tools (sqlcmd 17+).
# The SQL 2012/2014-era sqlcmd still shipped on some servers rejects it outright
# with "Sqlcmd: 'C': Unknown Option", so probe the help text once and only pass it
# when this build understands it. Older builds do not encrypt by default, so they
# do not need it; newer ones do, and would fail on a self-signed cert without it.
$sqlcmdHelp = (& sqlcmd -? 2>&1 | Out-String)
$common = @('-E')
if ($sqlcmdHelp -match '\-C\b') {
    $common += '-C'
} else {
    Write-Host "note: this sqlcmd build has no -C; running without trust-server-certificate." -ForegroundColor DarkGray
}

# Order matters: Programme creates the MAIN hall that SeedGaps (booths + venue
# map) references, and Speakers creates the rows SpeakerPhotos points at.
$seeds = @(
    'SIMF_App_Programme.sql',
    'SIMF_App_News.sql',
    'SIMF_App_Sponsors.sql',
    'SIMF_App_MediaPartners.sql',
    'SIMF_App_Archive.sql',
    'SIMF_App_Organization.sql',
    'SIMF_App_Speakers.sql',
    'SIMF_App_SpeakerPhotos.sql',
    'SIMF_App_SeedGaps.sql'
)

# Refuse to seed the Identity database — the two are physically separate (D-157)
# and the content tables simply are not there, so this would fail late and messily.
$probe = & sqlcmd -S $Server -d $Database @common -h-1 -W -b -Q `
    "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID('dbo.Speakers') IS NULL THEN 'NO' ELSE 'YES' END;" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Cannot connect to [$Database] on [$Server]: $probe"
    exit 1
}
if (($probe | Out-String).Trim() -ne 'YES') {
    Write-Error ("[$Database] has no dbo.Speakers table, so it is not the App/CONTENT " +
                 "database. Local dev uses SIMF_App; the server uses SIMF_Data. " +
                 "Pass -Database explicitly.")
    exit 1
}

Write-Host "=== SIMF_App 2026 content seed -> [$Database] on [$Server] ===" -ForegroundColor Cyan

$n = 0
foreach ($seed in $seeds) {
    $n++
    $path = Join-Path $dir $seed
    if (-not (Test-Path $path)) { Write-Error "Missing seed file: $path"; exit 1 }

    Write-Host ("[{0}/{1}] {2}" -f $n, $seeds.Count, $seed) -NoNewline
    # -f 65001: read the file as UTF-8 regardless of BOM or system codepage.
    # -b      : non-zero exit on the first SQL error.
    $out = & sqlcmd -S $Server -d $Database @common -b -f 65001 -i $path 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED" -ForegroundColor Red
        $out | Select-Object -Last 15 | ForEach-Object { Write-Host "    $_" }
        Write-Error "Seeding stopped at $seed. No later seed ran."
        exit 1
    }
    Write-Host "  ok" -ForegroundColor Green
}

Write-Host "=== content seed COMPLETE on [$Database] ===" -ForegroundColor Cyan
Write-Host "Next: copy speaker-photos\speakerphoto -> <FileStorage:RootPath>\speakerphoto" -ForegroundColor Yellow
