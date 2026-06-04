# Logs viewer — `/admin/logs`

| | |
|--|--|
| **Route** | `/admin/logs` |
| **Audience** | Administrator |
| **Auth** | `[Authorize(Roles = "Administrator")]` |
| **Pattern** | SimfBanner (D-132) + 2-row filter / actions layout (D-117 §11.1). |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/admin/logs/projects`, `GET /account/api/admin/logs/files?project={p}`, `GET /account/api/admin/logs/tail?project={p}&file={f}&lines={n}`, `GET /account/api/admin/logs/download?project={p}&file={f}` |
| **Source** | [`LogsViewer.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/LogsViewer.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Real-time technical log viewer. Lists every project's log files under
`{Storage:LogDirectory}` (typically `Api`, `ControlPanel`, `Website`),
tails the chosen file with a 5-second poll, and offers a Download button
that streams the full file to disk. Per-project per-day file layout
mirrors the IBS pattern.

## 4. UI

Two rows below the banner (D-117 §11.1):

- **Row 1 (filters):** Project select / File select / Lines select (50, 100,
  500, 1000) + Auto-refresh checkbox.
- **Row 2 (actions):** Refresh + Download buttons.
- **Body:** monospaced `<pre>` block with the tailed log.

## 7. Edge cases

- **No log files yet** → file select is empty + body shows "(no file selected)".
- **Auto-refresh** → 5 s poll; pauses if the tab loses focus.
- **Large file** → only the last N lines are tailed; full file via Download.
- **Permission denied on the disk path** → bilingual error in a SimfAlert.

## 10. Use cases

UC-LOG-PICK, UC-LOG-TAIL, UC-LOG-DOWNLOAD _(pending UCS)_.

## 11. E2E

| Scenario | ID |
|----------|----|
| Pick Api → file list populates | E2E-LOG-001 |
| Tail file → 5 s poll updates body | E2E-LOG-002 |
| Download → file streams | E2E-LOG-003 |
| Non-admin → /not-permitted | E2E-LOG-004 |

## 12. Related

- Decisions: D-117 (2-row layout), D-132 (banner swap).
- Source: `LogsViewer.razor` + backend `LogsEndpoints.cs`.

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
