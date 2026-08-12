# SIMF engineering diagrams

Three sheets, each drawn in an established notation rather than to taste, and
each fact traceable to a named source.

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

The `Fig1` / `Fig2` / `Fig3` in the filenames identify the sheet, not its figure
number. **The artwork carries no figure number**: each document numbers its own
figures in order of appearance and states that number in the caption, so the same
sheet is Figure 1 in one document and Figure 3 in another without any clash. Do
not put a number back into a sheet title.

Consumers: `SIMF-HLD-005` (all three, landscape, sections 2.1 to 2.3) and
`SIMF-LLD-003` (components at 2.1.1, data flow at 2.2, deployment at 7.1).

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

```
& 'C:\Program Files\Google\Chrome\Application\chrome.exe' `
    --headless --disable-gpu --no-sandbox --hide-scrollbars `
    --user-data-dir=<a fresh temp dir> --force-device-scale-factor=2 `
    --window-size=<the sheet width>,<the sheet height> `
    --screenshot=<absolute .png path> 'file:///<absolute .svg path>'
```

Sheet sizes: figure 1 is 1660 x 1020, figure 2 is 1660 x 1220, figure 3 is
1660 x 1200, figure 4 is 1660 x 1310. At scale factor 2 the PNG is twice that,
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

The mobile edge and the file server carry **no node count and no specification**
on sheet 4: the customer workbook lists neither, and inventing a figure for
either would put an unsourced number in front of the customer. Both say
"specification to be confirmed with the site" until the workbook is updated.

`SIMF.RealTime` appears in none of the sheets: the project was removed from the
solution in commit `7160a9ba`.
