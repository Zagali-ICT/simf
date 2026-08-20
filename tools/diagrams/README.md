# SIMF engineering diagrams

Four sheets, each drawn in an established notation rather than to taste, and
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

Sheet 4 is the same notation as sheet 1 and a different estate, not a redraw of
it. Sheet 1 puts the web, API and Control Panel hosts together in one
application zone, which the API is published from. Sheet 4 separates them: the
presentation zone holds the web, Control Panel and mobile-edge hosts, the
application zone holds the API alone and is reachable only from the presentation
zone, and stored files sit on their own server rather than on the database node.
Keep both. One is the estate SIMF-HLD-004 describes, the other is the target
agreed on 2026-08-10, and a document that shows one should say which.

Sheet 4 also carries what sheet 1 does not: two **security areas** grouping the
zones, HSA over the data zone and SSA over everything else; a **load balancer**
in front of the API nodes; and an **internet zone** holding the two third-party
services the API calls outbound. Those four are owner decisions of 2026-08-20.
The acronyms HSA and SSA are printed bare, with no expansion, because none was
supplied and inventing one would put invented wording in front of the customer.

The `Fig1` / `Fig2` / `Fig3` in the filenames identify the sheet, not its figure
number. **The artwork carries no figure number**: each document numbers its own
figures in order of appearance and states that number in the caption, so the same
sheet is Figure 1 in one document and Figure 3 in another without any clash. Do
not put a number back into a sheet title.

Consumers: `SIMF-HLD-005` (landscape, sections 2.1 to 2.3) and `SIMF-LLD-003`
(components at 2.1.1, data flow at 2.2, deployment at 7.1). LLD-003 v1.2 embeds
sheets 2, 3 and 4; whenever one is re-rendered its image in that document is
stale until it is replaced.

## Regenerate

```
python tools/diagrams/fig1_deployment.py
python tools/diagrams/fig2_component.py
python tools/diagrams/fig3_dataflow.py
python tools/diagrams/fig4_tier_separation.py
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
1660 x 1200, figure 4 is 1940 x 1682. At scale factor 2 the PNG is twice that,
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
* Where a sheet runs a path up the inside of a zone, raise that zone's `pad` so
  the name and the note sit clear of the channel rather than being crossed.
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
| 4 | The HSA and SSA security areas, the API load balancer, the internet zone, and the two outbound calls | owner decisions of 2026-08-20 |
| 4 | YouTube caption host `youtubei.googleapis.com` | `PlayerUrl` in `SIMF.Infrastructure/Programme/YoutubeTranscriptService.cs` |
| 4 | Gemini host `generativelanguage.googleapis.com` | `BaseUrl` in `SIMF.Infrastructure/Ai/AiOptions.cs` |

The mobile edge, the file server and the API load balancer carry **no node count
and no specification** on sheet 4: the customer workbook lists none of them, and
inventing a figure would put an unsourced number in front of the customer. The
two servers say "specification to be confirmed with the site" until the workbook
is updated; the load balancer is drawn as a device, like the WAF, which carries
no specification either.

Sheet 4 draws the API's outbound calls as the **target** state. The YouTube
egress is not open today: `docs/deploy/SIMF-YouTube-Egress-Allowlist-Request.md`
records the request as **PENDING** against the NCA egress posture, and until it
is granted the Control Panel's subtitle fetch returns `SUBTITLE_FETCH_FAILED`
and the admin pastes or uploads the transcript instead. That belongs in the
document body, not on the artwork, which states no caveats.

`SIMF.RealTime` appears in none of the sheets: the project was removed from the
solution in commit `7160a9ba`.
