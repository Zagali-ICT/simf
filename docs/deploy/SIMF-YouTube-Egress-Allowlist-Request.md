# SIMF — Outbound Egress Request: YouTube Caption Endpoints (Subtitle Fetch)

Date: 2026-07-08
Owner decision: **PENDING** (weigh against NCA egress posture)
Feature: D-578 "fetch subtitle from video" on the Control Panel Sessions editor
Related: D-349 (YouTube as the live-video provider, proof of concept)

## Summary

The Control Panel Sessions editor (`/admin/sessions` → Edit) has a **"fetch
subtitle from video"** action that imports a YouTube video's captions to feed the
AI session-summary drafter. The import is a **server-to-server** call: the SIMF
**API host** contacts YouTube's caption endpoints — the admin's browser is not
involved.

Today the on-prem / hosting network blocks outbound YouTube egress from the API
host, so the action returns a graceful `SUBTITLE_FETCH_FAILED` (502) with a
bilingual message telling the admin to **paste or upload** the subtitle instead.
**Nothing is broken** — the fetch is disabled by the network, and the
paste/upload path works without any egress. This document requests the egress so
the fetch can work, for the owner/infra team to approve or decline.

## Request

Allow **outbound HTTPS (TCP 443)** from the **SIMF API host** (the backend
serving `cp.simrsnf.com` / `api.simrsnf.com`) to:

| Host | Role | Necessity |
|------|------|-----------|
| `youtubei.googleapis.com` | YouTube "innertube" player API — lists a video's caption tracks (1st hop). | **Required** — without it the fetch fails immediately. |
| `www.youtube.com` | Serves the `timedtext` caption-download URLs returned by the player API (2nd hop). | **Required** |
| `*.googlevideo.com` | Some caption tracks are served from Google's video CDN. | Recommended |
| `*.google.com` | Belt-and-suspenders; the server re-validates the returned caption host before the 2nd request. | Optional |

- **Direction:** egress only, from the API host's IP → the hosts above, port 443.
- **Inbound:** no change.
- **Thumbnails/other:** not required (`ytimg.com` etc. are not contacted by this feature).

## Source in code

- `src/Backend/SIMF.Infrastructure/Programme/YoutubeTranscriptService.cs`
  - `PlayerUrl` → `youtubei.googleapis.com/youtubei/v1/player` (1st hop, caption-track list).
  - Caption text is downloaded from the `baseUrl` YouTube returns; the host is
    re-validated by `IsCaptionHost` (`youtube.com` / `google.com` /
    `googlevideo.com` and subdomains) before the 2nd request. A dedicated
    **no-redirect** HttpClient is used (SSRF hardening, D-578 security review).
- Endpoint: `src/Backend/SIMF.Api/Endpoints/Admin/SessionSubtitleEndpoints.cs`
  (`POST /admin/sessions/subtitle/fetch-from-video`, gated by `Sessions.Edit` +
  approved account, rate-limited).

## Scope / decision note (NCA)

This egress should be weighed against the **NCA egress posture** before approval.
It is a proof-of-concept convenience, not a hard dependency: if declined, the
**paste transcript text** and **upload `.srt`/`.vtt`/`.txt`** paths remain the
supported way to load a transcript for the AI summary, and neither needs any
outbound network.

## Confirm the failure first

Opening this egress only helps if the observed failure is the egress 502. The
exact toast on the Sessions editor distinguishes the causes:

| Message | HTTP | Meaning | Does egress fix it? |
|---------|------|---------|---------------------|
| "…the server may not be able to reach YouTube…" | 502 | Egress block | **Yes** |
| "This video has no captions to import…" | 422 | Video has no caption track | No |
| "The link is not a recognised YouTube video URL." | 400 | `LiveStreamUrl` has no valid 11-char video id | No |

To confirm: open a session on prod, click **fetch**, and read the message (or the
network response for `POST /account/api/admin/sessions/subtitle/fetch-from-video`).

## Verification after the egress is opened

1. From the API host, confirm the two required hosts resolve and connect on 443
   (e.g. an HTTPS reachability check to `youtubei.googleapis.com` and
   `www.youtube.com`).
2. On the CP Sessions editor, set a YouTube `LiveStreamUrl` **known to have
   captions**, click **fetch**, and confirm the transcript text is imported.
