# SIMF engineering diagrams

Six sheets, each drawn in an established notation rather than to taste, and
each fact traceable to a named source. Sheet 1's outputs are **not** in the
repository: LLD-003 v1.2 replaced that sheet with sheet 4 and the files were
deleted in `02ca3c8f4`. Its script is kept and still runs, so re-running it
writes the `.svg` back; delete it again unless a document has asked for it.

| Sheet | Notation | Output |
|---|---|---|
| Deployment and network | UML deployment diagram | `docs/diagrams/SIMF-Fig1-Deployment-Network.{svg,png}` |
| Components and interaction | UML component diagram, C4 container discipline | `docs/diagrams/SIMF-Fig2-Component-Interaction.{svg,png}` |
| System and data flow | Data flow diagram, Gane and Sarson | `docs/diagrams/SIMF-Fig3-Data-Flow.{svg,png}` |
| Target tier separation | UML deployment diagram | `docs/diagrams/SIMF-Fig4-Target-Tier-Separation.{svg,png}` |
| Security areas and egress | UML deployment diagram | `docs/diagrams/SIMF-Fig5-Security-Areas-And-Egress.{svg,png}` |
| Phase one, on-site services | UML deployment diagram | `docs/diagrams/SIMF-Fig6-Phase-One-Security-Areas.{svg,png}` |

Sheet 4 is the same notation as sheet 1 and a different estate, not a redraw of
it. Sheet 1 puts the web, API and Control Panel hosts together in one
application zone, which the API is published from. Sheet 4 separates them: the
presentation zone holds the web, Control Panel and mobile-edge hosts, the
application zone holds the API alone and is reachable only from the presentation
zone, and stored files sit on their own server rather than on the database node.
Keep both. One is the estate SIMF-HLD-004 describes, the other is the target
agreed on 2026-08-10, and a document that shows one should say which.

**Sheet 5 stands to sheet 4 exactly as sheet 4 stands to sheet 1: a new sheet,
not a redraw.** It carries sheet 4's estate unchanged and adds what sheet 4 does
not show: two **security areas** grouping the zones, HSA over the data zone and
SSA over everything else; a **load balancer** in front of the API nodes; an
**internet zone**; and the API's **two outbound calls** to it, which cross both
firewalls. Those four are owner decisions of 2026-08-20. The acronyms HSA and
SSA are printed bare, with no expansion, because none was supplied and inventing
one would put invented wording in front of the customer.

**Sheet 6 is phase one**, and stands to sheet 5 as sheet 5 stands to sheet 4. It
is a customer requirement of 2026-08-30 and moves five things: the API server
out of SSA and into HSA, so it has no internet path; the AI from a cloud
provider to an on-site **LLM server** in HSA, reached over an OpenAI-compatible
API; mail from an external relay to an on-site **mail server** in HSA; the file
store from a directory on a share to **MinIO** object storage in HSA, reached
over the S3 API; and the one remaining internet call, the YouTube caption
fetch, from the API to the **Control Panel**, which is the tier that has
internet access. The model is the SITE-hosted **GPT OSS 120B**, served over an
OpenAI-compatible API. CP, WEB and the mobile edge stay in the presentation
zone.

Sheet 4 is the deployment figure **as published in LLD-003 v1.2** and its files
are held at those bytes; sheet 5 is what v1.3 carries in its place. One
consequence, because the zone typography changed with sheet 5 and lives in the
shared kit: **re-running `fig4_tier_separation.py` no longer reproduces the
committed sheet 4** - it renders the same artwork with the newer zone name
styling. That is expected. Do not commit the result unless a document has asked
for a re-issued sheet 4.

The `Fig1` to `Fig6` in the filenames identify the sheet, not its figure
number. **The artwork carries no figure number**: each document numbers its own
figures in order of appearance and states that number in the caption, so the same
sheet is Figure 1 in one document and Figure 3 in another without any clash. Do
not put a number back into a sheet title.

**Two documents embed these sheets, and both embed all three.** Whenever a sheet
is re-rendered, its image in each of them is stale until that document is
reissued at a new version.

| Document | Deployment | Components | Data flow |
|---|---|---|---|
| `SIMF-HLD-004-MoD-HLD-External-v1.1` | sheet 4 | sheet 2 | sheet 3 |
| `SIMF-HLD-004-MoD-HLD-External-v1.2` | **sheet 5** | sheet 2 | sheet 3 |
| `SIMF-LLD-003-Solution-Design-Document-v1.2` | sheet 4 | sheet 2 | sheet 3 |
| `SIMF-LLD-003-Solution-Design-Document-v1.3` | **sheet 5** | sheet 2 | sheet 3 |
| `SIMF-HLD-004-MoD-HLD-External-v1.3` | sheet 6 | sheet 2 | sheet 3 |
| `SIMF-LLD-003-Solution-Design-Document-v1.4` | sheet 6 | sheet 2 | sheet 3 |
| `SIMF-HLD-004-MoD-HLD-External-v1.4` | **sheet 6** | sheet 2 | sheet 3 |
| `SIMF-LLD-003-Solution-Design-Document-v1.5` | **sheet 6** | sheet 2 | sheet 3 |

In the HLD the three figures sit together at the front, sized to a common
height; in the LLD they sit at 2.1.1, 2.2 and 7.1, sized to a common width. A
sheet whose aspect ratio differs from the one it replaces must be re-scaled on
the axis the document does not fix, or the picture is silently stretched.

`SIMF-HLD-005` is named in older notes as a consumer. **It is not on disk**: it
was deleted in `02ca3c8f4` along with sheet 1's outputs. HLD-004 is the HLD that
exists.

## Regenerate

```
python tools/diagrams/fig1_deployment.py
python tools/diagrams/fig2_component.py
python tools/diagrams/fig3_dataflow.py
python tools/diagrams/fig4_tier_separation.py
python tools/diagrams/fig5_security_areas.py
python tools/diagrams/fig6_phase_one.py
```

Each script writes the `.svg` only. Render the `.png` with headless Chrome. The
`--screenshot` path **must be absolute**; a relative path fails with "Access
denied". A fresh `--user-data-dir` avoids Chrome serving a cached `file://` copy.

Use plain `--headless`, not `--headless=old`. The old mode was removed from
Chrome and the flag now exits **silently with no file written** and no error
worth reading, which looks exactly like a path problem and is not one
(2026-08-10).

**Wait for Chrome to exit, and assert the PNG is newer than the SVG.** In
PowerShell, `& chrome.exe ...` returns immediately: Chrome re-executes itself as
a child process, so the call site continues while nothing has been written yet.
The screenshot lands seconds to minutes later, and in the meantime the previous
render is still sitting on disk under the same name, which reads exactly like a
successful render of the new artwork. This cost three wasted diagnoses on
2026-08-20, including two invented causes (a backslash in the `file:///` URL and
the screenshot's target directory) that were both retested afterwards and are
**not** real. Use `Start-Process -Wait`, which also lets Chrome print its
`N bytes written to file` line, and compare timestamps:

```powershell
$svgTime = (Get-Item $svg).LastWriteTime
Start-Process -FilePath $chrome -ArgumentList $args -Wait -NoNewWindow
if ((Get-Item $png).LastWriteTime -le $svgTime) { throw "stale PNG: $png" }
```

A render that reports success without proving which bytes changed is the same
defect class as a deploy that reports success without proving which host it
landed on.

```powershell
Start-Process -FilePath 'C:\Program Files\Google\Chrome\Application\chrome.exe' `
    -Wait -NoNewWindow -ArgumentList @(
    '--headless', '--disable-gpu', '--no-sandbox', '--hide-scrollbars',
    '--user-data-dir=<a fresh temp dir>', '--force-device-scale-factor=2',
    '--window-size=<the sheet width>,<the sheet height>',
    '--screenshot=<absolute .png path>', 'file:///<absolute .svg path>')
```

Sheet sizes: figure 1 is 1660 x 1020, figure 2 is 1660 x 1220, figure 3 is
1660 x 1200, figure 4 is 1660 x 1310, figure 5 is 1940 x 1682,
figure 6 is 1940 x 1706. At scale factor 2
the PNG is twice that,
which is enough for a full-page landscape print.

## Rules the sheets follow

Taken from the published notation guidance, not from preference:

* Every sheet carries a title stating the diagram type and scope, and a key.
  (C4 model notation guidance.)
* Every element states its type and its technology. Every line is
  unidirectional and carries a label; between components that label is the
  protocol and port. (Same source.)
* Colour is one value per element **type**, never per box, and the palette is
  printer friendly. Set `MONO = True` at the top of `svgkit.py` and re-render for
  a pure greyscale version.
* A zone or grouping name is set in ink at weight 700 and a larger size than
  anything inside the zone, so the reader finds the layer before the boxes. This
  is what `band()` and `group()` do; do not set a zone name by hand.
* Firewall wording draws **last**, over any path that crosses the bar, on its own
  backing patch. A bar spans the whole zone, so the paths crossing it can land
  anywhere along it, and one striking through the rule text is a defect. This is
  what `Sheet.late` is for.
* Where a sheet runs a path up the inside of a zone, raise the sheet's `zone_pad`
  so names and notes sit clear of the channel rather than being crossed.
* A change to a **published** sheet is a NEW sheet, never an edit of the old one.
  The old file keeps the bytes the document that embeds it was issued with, and
  the document reissues at a new version pointing at the new sheet. Sheet 4 was
  added beside sheet 1 this way; sheet 5 beside sheet 4.
* Nothing is written on the artwork that states what the system does not do.
  Caveats of that kind belong in the document body.
* No em-dash or en-dash in any title, caption or label.

## Where the facts come from

| Sheet | Fact | Source |
|---|---|---|
| 1 | Node names, quantities, OS, RAM, vCPU, storage, notes | customer server requirements workbook, sheet `List` |
| 1 | Mobile distribution channels | same workbook, sheet `NEW1` |
| 1 | Zone model, perimeter firewall, WAF, internal firewall | `SIMF-HLD-004` as delivered |
| 1, 2 | Deployed artifact and component names | the solution source tree |
| 2 | Inward dependency rule, Api to Infrastructure to Application to Domain | `SIMF-LLD-002` section 7.1 |
| 2 | Background workers run inside the API host | `AddHostedService` registrations in `SIMF.Infrastructure/DependencyInjection.cs` and `SIMF.Api/Program.cs` |
| 2 | Protocols and component roles | `SIMF-LLD-002` section 2.1 |
| 3 | Processes | the module list in `SIMF-LLD-002` section 5 |
| 3 | Data stores | `SIMF-LLD-002` section 6 |
| 4 | Server specifications, WEB / API / CP / database counts | customer server requirements workbook, sheet `List` |
| 4 | Zone model, perimeter firewall, WAF | `SIMF-HLD-004` as delivered |
| 4 | The mobile edge, the file server, and the application zone holding the API alone | owner decisions of 2026-08-10 |
| 5 | Everything sheet 4 carries, unchanged | sheet 4, itself sourced as above |
| 5 | The HSA and SSA security areas, the API load balancer, the internet zone, and the two outbound calls | owner decisions of 2026-08-20 |
| 5 | YouTube caption host `youtubei.googleapis.com` | `PlayerUrl` in `SIMF.Infrastructure/Programme/YoutubeTranscriptService.cs` |
| 5 | Gemini host `generativelanguage.googleapis.com` | `BaseUrl` in `SIMF.Infrastructure/Ai/AiOptions.cs` |
| 6 | Everything sheet 5 carries, except where phase one moves it | sheet 5, itself sourced as above |
| 6 | API into HSA, on-site LLM and mail servers, MinIO, YouTube from the CP | customer requirement of 2026-08-30 |
| 6 | SMTP port 587 | `Port` default in `SIMF.Common/Options/EmailOptions.cs` |

The mobile edge and the file server carry **no node count and no specification**
on sheets 4 and 5, and neither does sheet 5's API load balancer: the customer
workbook lists none of them, and inventing a figure would put an unsourced number
in front of the customer. The two servers say "specification to be confirmed with
the site" until the workbook is updated; the load balancer is drawn as a device,
like the WAF, which carries no specification either.

Sheet 5 draws the API's outbound calls as the **target** state. The YouTube
egress is not open today: `docs/deploy/SIMF-YouTube-Egress-Allowlist-Request.md`
records the request as **PENDING** against the NCA egress posture, and until it
is granted the Control Panel's subtitle fetch returns `SUBTITLE_FETCH_FAILED`
and the admin pastes or uploads the transcript instead. That belongs in the
document body, not on the artwork, which states no caveats.

`SIMF.RealTime` appears in none of the sheets: the project was removed from the
solution in commit `7160a9ba`.
