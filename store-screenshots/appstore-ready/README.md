# App Store Connect screenshots

Generated 2026-09-02 from `../play-ready/` (the Google Play set, 1080x1920).

Upload these in App Store Connect under the version's **App Previews and
Screenshots**, picking the matching device size tab.

| Folder | Size | Apple requirement |
|---|---|---|
| `iphone-6.9/` | 1260 x 2736 | The 6.9" slot. Apple's published guidance calls this the only required iPhone size and scales it down to the rest. |
| `iphone-6.5-1284x2778/` | 1284 x 2778 | The 6.5" slot. **Use this one first.** |
| `iphone-6.5-1242x2688/` | 1242 x 2688 | The 6.5" slot, alternate accepted size. Use only if the upload rejects 1284 x 2778. |
| `ipad-13/` | 2064 x 2752 | **Required while the app runs on iPad.** `Runner.xcodeproj` declares `TARGETED_DEVICE_FAMILY = "1,2"` (iPhone + iPad), so App Store Connect will ask for these. |

8 screenshots per size; Apple allows 1 to 10.

**Why there are two iPhone sizes, and which to trust.** Apple's screenshot
specification page says 6.9" (1260 x 2736) is the only required iPhone size. The
App Store Connect UI asked for the **6.5"** sizes instead - `1242 x 2688`,
`2688 x 1242`, `1284 x 2778` or `2778 x 1284`. Both are produced rather than
guessing which is right, because **the dialog in front of you is the authority**:
the published spec describes intent, the uploader enforces it, and they do not
always agree. Only the portrait variants exist - the app is portrait-locked
(`main.dart`), so a landscape screenshot would show a layout users never see.

## How they were made, and the one thing to know

The Play set is 9:16. **Neither Apple size is 9:16**, so these could not simply
be resized. Each source was **fitted and padded** - scaled with its aspect ratio
intact, then centred on a canvas of the target size:

- iPhone 6.9" (1260 x 2736): scaled 1.167x, 248px padding top and bottom.
- iPhone 6.5" (1284 x 2778): scaled 1.189x, 247px padding top and bottom.
- iPhone 6.5" (1242 x 2688): scaled 1.150x, 240px padding top and bottom.
- iPad 13" (2064 x 2752): scaled 1.433x, 258px padding left and right.

Nothing is cropped and nothing is stretched, so every pixel of the UI survives
and no text is distorted. The pad colour is `#01132D`, the brand navy the
screenshots already carry to all four edges - verified by sampling the corners
and edge midpoints, so the seam is invisible rather than merely close.

**They are letterboxed, and that is visible as a band of navy.** It reads as
deliberate framing rather than a mistake, and App Store Connect accepts them, but
they are not native captures. **When the Mac mini exists** (see
`docs/dev/Mobile-iOS-Release-Build.md` section 4), recapture on an iOS simulator
at 6.9" and 13" for edge-to-edge screenshots and replace these.

## Alternative to the iPad set

If the app is not meant to ship for iPad, changing
`TARGETED_DEVICE_FAMILY` from `"1,2"` to `"1"` in
`src/Mobile/simf_app/ios/Runner.xcodeproj/project.pbxproj` removes the iPad
requirement entirely and `ipad-13/` becomes unnecessary. That is a product
decision, not a store one, so it has not been made here.

## What is deliberately NOT here

- **No app icon.** iOS takes its icon from the binary's own asset catalogue
  (`ios/Runner/Assets.xcassets/AppIcon.appiconset`), not from an upload. Play's
  `store-icon-512.png` has no App Store equivalent.
- **No feature graphic.** That is a Play Store concept; the App Store has none.

## Content

All eight are **guest mode** - no account, no personal data, no real attendee
information. Safe for a public listing. They also avoid the `FLAG_SECURE`
problem: that Android control blocks on-device screen capture, which is why the
originals came from an emulator.
