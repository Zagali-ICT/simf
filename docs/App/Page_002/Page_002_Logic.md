# Page 002 — Logic (التهيئة · Onboarding)

Business rules behind the screen. There is **no server logic** for this page — all rules
are client-side. The backend contract is in [Page_002_API.md](Page_002_API.md).

## L-1 First-run gate
A single **client-side** boolean (e.g. `hasSeenOnboarding`) in local storage decides
whether this screen runs:
- **Unset / false** → run the sequence (loading image → 3 videos).
- **True** → skip the screen entirely; the router advances straight to the next entry screen.

The flag is set to **true** the moment the sequence completes **or** the user taps Skip
(whichever comes first). Reinstalling or clearing app data resets it. No server round-trip.

## L-2 Media resolution (stable names)
Media is addressed by **stable logical names**, not URLs hard-coded in screen code:

| Logical name | Role | Preferred source | Fallback |
|--------------|------|------------------|----------|
| `introd_loading` | Loading / brand image (FE-1) | bundled asset | — |
| `introd_001` | Intro video 1 | **YouTube** | bundled clip |
| `introd_002` | Intro video 2 | **YouTube** | bundled clip |
| `introd_003` | Intro video 3 | **YouTube** | bundled clip |

The naming series continues `introd_004`, `introd_005`, … if more clips are added later.
The screen iterates the ordered list, so adding/removing a clip is a content change, not a
code change.

## L-3 State transitions
```
LoadingImage ──(first clip ready)──▶ Video_001 ──▶ Video_002 ──▶ Video_003 ──▶ Done
     │                                   │             │             │           │
     └──────────────── Skip ─────────────┴─────────────┴─────────────┘           ▼
                                                                        set first-run = true
                                                                        route → next entry screen
```
- Each video auto-advances on completion; Next advances manually.
- Skip from any state jumps straight to **Done**.
- **Done** sets the first-run flag (L-1) and routes onward; the screen is popped so Back
  does not return to it.

## L-4 Source preference & fallback (videos)
- **Preferred:** YouTube (embed/player). Requires network.
- If YouTube fails to load (offline, blocked, error) → play the **bundled local clip** for
  the same logical name. If neither is available → treat that clip as finished and advance
  (never block the user on a missing intro video).
- Videos are **muted by default** with an optional sound toggle (FE-7).

## L-5 Edge cases
- **Offline on first run** → loading image shows, YouTube fails, bundled fallback plays; if no
  fallback exists, advance silently. The user is never trapped on this screen.
- **App killed mid-sequence** → flag not yet set, so the sequence replays on next launch
  (acceptable; it is set only on completion/skip).
- **Returning user** → never reaches this screen (L-1).
- **No SIMF API** → there is no network error surface tied to a SIMF endpoint here; the only
  network dependency is the external YouTube player, which degrades to the bundled fallback.

## L-6 Auth / privilege gate
- Runs **before** sign-in; the actor is **Guest**. No token, no auth header, no permission code.
- App authorization is expressed only in the four app roles (Guest/Visitor/Moderator/Staff);
  this screen sits at **Guest** and is therefore reachable by everyone on first run.

## L-7 Localization & direction
Arabic primary (RTL), English secondary. The only on-screen text is the Skip / Next controls
and (optionally) a localized caption — pulled from app resources per active locale. Video
content itself is fixed media; captions, if any, ship with the clip. Layout mirrors for RTL
(progress order and controls flip).
