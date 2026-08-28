# Store submission assets

**`play-ready/` is the folder the console consumes.** Nothing else here is
uploadable, and the two sets look alike enough that picking the wrong one is easy
— which is why the raw captures moved out of the top level.

## play-ready/ — verified against Play's spec

| Asset | Requirement | This file |
|---|---|---|
| 8 phone screenshots | JPEG or **24-bit PNG, no alpha**; longest side at most **2x** the shortest | 1080x1920, 24-bit RGB, ratio **1.778** ✅ |
| `feature-graphic-1024x500.png` | 1024x500, JPEG or **24-bit PNG, no alpha** | 1024x500, 24-bit RGB ✅ |
| `store-icon-512.png` | 512x512, **32-bit PNG WITH alpha**, under 1024 KB | 512x512, 32-bit RGBA, fully opaque, 69 KB ✅ |

**The icon and the feature graphic have OPPOSITE alpha rules.** The icon must be
32-bit; the graphic must be 24-bit. That is not a typo, and it is how this folder
came to hold one correct asset and one rejected one: the icon was exported as
24-bit RGB and Play refuses it on upload, which blocks the listing, which blocks
every track including internal testing. Re-exported 2026-08-28 from
`src/Mobile/simf_app/icon/app_icon.png` (1024x1024 RGBA) with a fully opaque
alpha channel — Play wants a 32-bit file, not a transparent one.

## raw-captures/ — do NOT upload, untracked

The original device captures: 1080x2400, RGBA. Both facts disqualify them —
the ratio is **2.222**, over Play's 2:1 ceiling, and screenshots must carry no
alpha channel. Kept because they are the masters the play-ready set was derived
from; excluded from git because they are large and never uploaded.

## Capturing new screenshots

`MainActivity.kt` sets `FLAG_SECURE` app-wide, so Android blocks screenshots and
screen recording on a real device. Capture from an **emulator**, or on iOS. See
`docs/dev/Mobile-Store-Release.md` section 5.4.

## Open

The feature graphic's wordmark reads «الملتقى الدولى البحرى», which matches
neither the launcher name «الملتقى البحري» nor the full brand name
«الملتقى البحري السعودي الدولي» used on the website. D-358 flagged that spelling
when the sign-in screen took it verbatim from the design. Settle the canonical
Arabic name before the listing goes up, and re-render this graphic with it.
