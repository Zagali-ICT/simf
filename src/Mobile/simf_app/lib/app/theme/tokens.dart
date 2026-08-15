import 'package:flutter/material.dart';

/// Design tokens for the SIMF app (SIMF-MAA-001 v1.2 §11).
///
/// The **colour** tokens carry the delivered KSA-Project Figma design system
/// (file PSXHhY0UVTAPSaIOf9uNKd — D-358/D-359 app redesign programme); they
/// supersede the interim `Mockup.html` placeholder palette. Spacing, radii and
/// the raw type scale are unchanged until further design frames dictate
/// otherwise. Widgets reference [SimfTokens], not literals, so any future
/// design change stays local to this file.
class SimfTokens {
  SimfTokens._();

  // Core brand colours — KSA-Project Figma variables (D-359).
  /// "Primary- Color" — dark scaffold
  static const Color navy = Color(0xFF01132D);
  /// "BG" — boxes/cards on navy
  static const Color navyDeep = Color(0xFF192B41);
  /// "Secondary- Color" — gold
  static const Color accent = Color(0xFFC9A84C);
  static const Color ink = Color(0xFF1A2030);
  static const Color inkMuted = Color(0xFF5A6573);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color background = Color(0xFFE9ECEF);
  static const Color field = Color(0xFFEEF1F4);
  static const Color danger = Color(0xFFA8182A);
  static const Color success = Color(0xFF2E7D32);
  /// amber — "being answered" (Figma 1461:12227)
  static const Color warning = Color(0xFFE8932A);

  // Moderator desk per-action colours — Figma 1461:12227 (Tailwind red-600 /
  // green-600 / amber-500): reject, answered, being-answered (on-stage).
  static const Color qReject = Color(0xFFDC2626);
  static const Color qAnswered = Color(0xFF16A34A);
  static const Color qStage = Color(0xFFF59E0B);

  // Request-status colours — الطلبات chips + card borders (Figma 1408:9760+,
  // Tailwind-500): accepted green / rejected red / cancelled grey. Pending
  // reuses [qStage] (#F59E0B). Chips render the colour at 12% fill + 20%
  // border.
  static const Color statusAccepted = Color(0xFF22C55E);
  static const Color statusRejected = Color(0xFFEF4444);
  static const Color statusCancelled = Color(0xFF6B7280);

  // D-745 — bilateral-meetings card flag badge (Figma 1408:9726): the
  // nationality flag emoji on a soft green well (green #27AE60 @ 9% fill + 21%
  // border).
  static const Color flagBadgeBg = Color(0x1727AE60);
  static const Color flagBadgeBorder = Color(0x3627AE60);

  // KSA-Project light-surface + auth-flow palette (D-358/D-359).
  /// elevated navy surface (login bg)
  static const Color navySurface = Color(0xFF102238);
  /// darker header block behind the forum title (Figma 1467:12565 / 1461:12565)
  static const Color navyHeader = Color(0xFF071832);
  /// navy #01132D @ 80% — reference-number card fill (registration success
  /// 505:1525)
  static const Color navyFill80 = Color(0xCC01132D);
  /// navy #01132D @ 70% — gallery video play-circle fill (Figma 949:4059)
  static const Color navyFill70 = Color(0xB301132D);
  /// navy #01132D @ 90% — onboarding photo overlay (Figma 148:22)
  static const Color navyFill90 = Color(0xE601132D);
  /// navy #01132D @ 60% — onboarding VIDEO scrim (owner 2026-07-26: the 90% photo overlay hid the moving footage; 60% keeps white/beige copy legible)
  static const Color navyFill60 = Color(0x9901132D);
  /// muted navy border on unselected pills (interests grid, Figma 505:1222)
  static const Color chipBorderNavy = Color(0xFF2A4066);
  /// contact-tile border (registration success, Figma 522:2223)
  static const Color tileBorderNavy = Color(0xFF253660);
  /// QR-scanner card fill (Figma 758:4566)
  static const Color scannerCard = Color(0xFF0F2044);
  /// QR-scanner progress-bar track (Figma 758:4598)
  static const Color scannerTrack = Color(0xFF132A50);
  /// muted blue caption — OTP countdown + scanner status (Figma 505:987 / 758:4596)
  static const Color mutedBlue = Color(0xFF8A9CC0);
  /// "Pragraph Color" — borders + on-navy paragraph text
  static const Color beigeBorder = Color(0xFFC2B8A2);
  /// beige 10% — tier-pill + link-row icon-box fill (Figma
  /// rgba(194,184,162,0.1))
  static const Color beigeFill10 = Color(0x1AC2B8A2);
  /// beige 40% — agenda timeline row divider (Figma 1310:3239
  /// rgba(194,184,162,0.4))
  static const Color beigeBorder40 = Color(0x66C2B8A2);
  /// beige 50% — liveness pending progress dash (Figma 758:4242
  /// rgba(194,184,162,0.5))
  static const Color beigeFill50 = Color(0x80C2B8A2);
  /// gold 7% — delegations stats-strip grid (Figma 1426:10771)
  static const Color goldFill7 = Color(0x12C9A84C);
  /// gold 6% — delegations head-of-delegation box fill (Figma 1426:10771)
  static const Color goldFill6 = Color(0x0FC9A84C);
  /// gold 15% — delegations head-of-delegation box border (Figma 1426:10771)
  static const Color goldBorder15 = Color(0x26C9A84C);
  /// light card surface
  static const Color cardBeige = Color(0xFFF1ECE4);
  /// secondary gold text/icons
  static const Color goldSoft = Color(0xFFD0AC77);
  /// goldSoft @ 50% — onboarding inactive page dot (Figma 148:22)
  static const Color goldSoftFill50 = Color(0x80D0AC77);
  /// headings on light surfaces
  static const Color headlineInk = Color(0xFF111827);
  /// secondary text on light surfaces
  static const Color greyText = Color(0xFF6C7278);
  /// inline links on light surfaces
  static const Color linkNavy = Color(0xFF00245E);
  /// input text on light surfaces (#111827 at 80%)
  static const Color inputInk = Color(0xCC111827);
  /// muted text on the gold identity strip (badge 758:1469)
  static const Color onGoldMuted = Color(0xFFF0F0F0);
  /// notification timestamp (758:2491)
  static const Color timestampMuted = Color(0xFF4C555F);
  // Per-kind notification category-icon colours (Figma 758:2491 palette). Kept
  // distinct from the semantic success/danger so the icons match the mockup's
  // decorative per-kind styling exactly.
  /// Figma "Green/green-500"
  static const Color notifGreen = Color(0xFF13C296);
  /// Figma "Primary/primary-500"
  static const Color notifCoral = Color(0xFFFF6347);
  /// pale-beige code chip fill (venue map 758:1358, #FFF4DC @ 80%)
  static const Color codeBoxBeige = Color(0xCCFFF4DC);
  /// soft card drop-shadow (headlineInk @ ~16%)
  static const Color cardShadow = Color(0x29111827);
  /// calendar day with no sessions (758:1415)
  static const Color dayInactive = Color(0xFFC2C2C2);
  /// day-banner bottom gradient #001030 @ 80% (Figma 1310:3232 / 1064:13240)
  static const Color bannerScrim = Color(0xCC001030);
  /// bottom-nav inactive icon (758:1476)
  static const Color navInactive = Color(0xFF5E584B);
  /// assistant chat-bubble text (1064:13278)
  static const Color chatBubbleText = Color(0xFFF0F4FF);
  /// live AI-caption placeholder text (934:3613)
  static const Color captionText = Color(0xFFDDE4F0);
  // Live-broadcast player band (Figma 934:3450): the LIVE pill is a brighter
  // brick-red than the semantic [danger]; the language chip is a translucent
  // dark glassy pill; the resting play button is a translucent-white circle.
  /// LIVE badge fill (934:3609)
  static const Color liveRed = Color(0xFFC0392B);
  /// language chip fill rgba(0,0,0,0.55) (934:3604)
  static const Color scrimBlack55 = Color(0x8C000000);
  /// play-circle fill rgba(255,255,255,0.15) (934:3595)
  static const Color playScrim = Color(0x26FFFFFF);

  // KSA main-shell disabled palette (W2 frames 512:1492 / 512:1780): the
  // "بطاقتي" locked card and the disabled theme tile render on these.
  /// disabled card fill
  static const Color navyDisabled = Color(0xFF0A1628);
  /// disabled card border
  static const Color navyDisabledBorder = Color(0xFF1E3A5F);
  /// disabled label/icon
  static const Color navyDisabledText = Color(0xFF4A6080);

  // On-navy hairlines + muted text + light-surface hairline, straight from
  // Mockup.html (--line / --line-2 / --txt-2 / --txt-3 / --line-light). These
  // drive the dark theme's cards, dividers, borders and secondary text.
  /// white 10%
  static const Color line = Color(0x1AFFFFFF);
  /// white 6%
  static const Color line2 = Color(0x0FFFFFFF);
  /// white 4% (card fill)
  static const Color surfaceTint = Color(0x0AFFFFFF);
  /// white 65%
  static const Color txtSecondary = Color(0xA6FFFFFF);
  /// white 40%
  static const Color txtTertiary = Color(0x66FFFFFF);
  /// navy 8%
  static const Color lineLight = Color(0x140F2238);

  // High-contrast accessibility palette (WCAG-boosted; interim, not final
  // design — see SIMF-VID-001). Used only when the Page 038 high-contrast
  // toggle is on, via SimfTheme.highContrastLight()/highContrastDark().
  static const Color hcLightSurface = Color(0xFFFFFFFF);
  static const Color hcLightInk = Color(0xFF000000);
  static const Color hcLightField = Color(0xFFEAEAEA);
  static const Color hcDarkSurface = Color(0xFF000000);
  static const Color hcDarkInk = Color(0xFFFFFFFF);
  static const Color hcDarkField = Color(0xFF1A1A1A);

  // Framework colour alias — the design token for a fully-transparent fill so
  // widgets never reference `Colors.transparent` directly (#16 sweep).
  static const Color transparent = Color(0x00000000);

  // Opaque black — the letterbox behind video/camera surfaces (the live player
  // band, the scanner viewfinder). Its own token so no widget reaches for
  // `Colors.black`; it is a surface colour, not a scrim.
  static const Color black = Color(0xFF000000);

  // White at 70% — the secondary label on a photo/camera surface where the
  // on-navy [txtSecondary] would wash out.
  static const Color white70 = Color(0xB3FFFFFF);

  // Black scrims over photo / video / camera surfaces, by opacity. Named the
  // same way as [scrimBlack55] so the set reads as one scale.
  /// scanner card shadow
  static const Color scrimBlack25 = Color(0x40000000);
  /// scanner viewfinder mask
  static const Color scrimBlack35 = Color(0x59000000);
  /// scanner busy overlay
  static const Color scrimBlack40 = Color(0x66000000);
  /// home hero image scrim
  static const Color scrimBlack50 = Color(0x80000000);
  /// radio pill on beige
  static const Color scrimWhite90 = Color(0xE6FFFFFF);

  // [accent] at zero alpha — the fade-out stop of the scanner sweep gradient.
  // A token, not `accent.withValues(alpha: 0)`, because the gradient is const.
  static const Color accentFade = Color(0x00C9A84C);
  // D-771 — seat TIER colours (Normal / VIP / VVIP). The tier belongs to a hall
  // ROW, so these tint the row's start-edge band, never the seat square (which
  // keeps its reservation-state colour). The two values match the seeded VVIP /
  // VIP profile-type badge colours and the Control Panel's --color-seat-tier-*
  // tokens, so a tier reads identically on a badge, a CP seat plan and the app.
  /// deep red — protocol
  static const Color seatTierVvip = Color(0xFFB91C1C);
  /// deep teal — VIP
  static const Color seatTierVip = Color(0xFF0E7490);
  // A12 — the CONFIRMED seat square: the holder scanned in at the hall gate,
  // so the seat is no longer just held. Mirrors the Control Panel's
  // --color-seat-confirmed (= --color-success, dark #4FA37D) so a confirmed
  // seat reads the same green on the CP seat map and in the app.
  static const Color seatConfirmed = Color(0xFF4FA37D);

  // Spacing scale.
  static const double space1 = 4;
  static const double space2 = 8;
  static const double space3 = 12;
  static const double space4 = 16;
  static const double space5 = 20;
  static const double space6 = 24;
  static const double space8 = 32;
  /// 10*4 extension of the spacing scale (onboarding/splash vertical gaps)
  static const double space10 = 40;

  // Radii.
  static const double radiusSmall = 4;
  /// LIVE badge (Figma 934:3609)
  static const double radius6 = 6;
  static const double radius = 8;
  static const double radiusLarge = 12;
  /// W2 cards / nav bar top corners
  static const double radiusLg = 16;
  /// exhibitor/sponsor link rows (Figma 1439:11904/11917)
  static const double radius14 = 14;
  /// delegations head-of-delegation box (Figma 1426:10838)
  static const double radius10 = 10;

  /// Ready-made [BorderRadius] for the default 4px corner (fields, cards,
  /// pills). Use this instead of re-wrapping [radiusSmall] in every widget.
  static const BorderRadius borderRadiusSmall =
      BorderRadius.all(Radius.circular(radiusSmall));

  // Hairline border weights (the KSA frames' 0.2px card hairline and the
  // 0.5px emphasised hairline).
  static const double hairline = 0.2;
  static const double hairlineBold = 0.5;

  // Component metrics (#16 sweep). Fixed component box heights + ratios lifted
  // out of the widgets so no screen carries a raw layout number. Each value is
  // the exact Figma measurement it replaces (behaviour-preserving).
  /// pill / banner / row height (booth code-pill+hall-box, gallery coverage-tab, archive edition-pill/notice/session-title)
  static const double controlHeight = 48;
  /// booth contact-box row (Figma 922:2810)
  static const double contactRowHeight = 44;
  /// booth code pill A-12 (Figma 922:2796) — fixed content width
  static const double codePillWidth = 109;
  /// sponsor hero/premium row + grid tile (Figma 922:2824)
  static const double sponsorRowHeight = 72;
  /// archive gallery-tile bottom scrim (Figma 926:3299)
  static const double galleryScrimHeight = 40;
  /// gallery media tile (Figma 949:4043)
  static const double mediaTileAspectRatio = 164 / 104;
  /// archive gallery-tile scrim gradient bottom stop
  static const double scrimOpacityStrong = 0.8;
  /// archive bullet disc top-align nudge (Figma 925:3258)
  static const double bulletTopNudge = 7;
  /// off-grid 2px micro-gap (below the space-4 scale)
  static const double gap2 = 2;
  /// off-grid 6px micro-gap (below the space-4 scale)
  static const double gap6 = 6;
  /// off-grid 18px gap (forum-guide step content -> caret, Figma 1426:11374)
  static const double gap18 = 18;
  /// news card thumbnail tile (Figma 958:2202)
  static const double newsThumbWidth = 155;
  /// news card thumbnail tile (Figma 958:2202)
  static const double newsThumbHeight = 85;
  /// media-partners grid tile (Figma 958:2246)
  static const double partnerCardAspectRatio = 163.5 / 104;
  /// onboarding step carousel viewport (Figma 148:22)
  static const double onboardCarouselHeight = 170;
  /// delegations decorative stats-strip map (Figma 1426:10781)
  static const double statsStripHeight = 100;
  /// venue-map floating zoom/locate control square (Figma 758:1358)
  static const double mapControlSize = 40;
  /// minimum touch-target height — venue-map direct-me button (Figma 758:1358)
  static const double tapTarget = 44;
  /// meeting-request day card (Figma 1776:4975)
  static const double dayCardWidth = 58;
  /// meeting-request day card + its picker row (Figma 1776:5052)
  static const double dayCardHeight = 64;

  // #16 S3 — chatbot (Figma 1064:13275) + quick-reply strip metrics.
  /// chat-bubble inner-bottom tail corner
  static const double radiusTail = 2;
  /// chat-bubble horizontal text inset
  static const double chatBubblePadH = 15;
  /// chat-bubble max width
  static const double chatBubbleMaxWidth = 288;
  /// composer send-button box
  static const double sendSquareSize = 24;
  /// quick-reply chip strip
  static const double quickReplyStripHeight = 34;

  // #16 S3 — ai_summary section-heading gold bar (Figma 1072:14660).
  static const double headingBarWidth = 4;
  static const double headingBarHeight = 20;

  // #16 S3 — home (Figma 758:1183+) metrics.
  /// social-button border (follow-us)
  static const double hairlineWide = 0.8;
  /// home LIVE banner مباشر badge square
  static const double liveBadgeSize = 60;
  /// highlights carousel slide
  static const double highlightSlideHeight = 170;
  /// home hero banner strip
  static const double heroBannerHeight = 160;
  /// home tall nav-tile min height
  static const double navTileHeight = 80;

  // #16 S3 — requests card icon-box + status-chip metrics (Figma 1408:9761+).
  /// gold type-icon box on a request card
  static const double requestIconBox = 32;
  /// status filter chip height
  static const double statusChipHeight = 32;
  /// status filter chip horizontal padding
  static const double statusChipPadH = 13;
  /// selected chip fill alpha
  static const double chipFillActiveOpacity = 0.24;
  /// resting chip fill alpha
  static const double chipFillOpacity = 0.12;
  /// resting chip border alpha
  static const double chipBorderOpacity = 0.2;

  // Type scale (Material text styles override these for actual rendering;
  // tokens here are for places that need a raw size).
  static const double textXs = 10.5;
  static const double textSm = 12;
  static const double textMd = 14;
  static const double textLg = 16;
  /// KSA "Sub-title 18" (Phase-0 type scale)
  static const double textTitle = 18;
  static const double textXl = 20;
  /// exhibitor/sponsor name (Figma 1439:11894)
  static const double textXxl = 22;
  /// registration-status headline (Figma 1701:3803)
  static const double text24 = 24;
  static const double textHero = 28;

  // Named text styles — built incrementally per screen (§5.1). The font family
  // comes from the theme; these bundle only colour/size/weight/line-height so a
  // widget never constructs a raw TextStyle. First two land with the faq pilot
  // (the on-navy beige "Paragraph Color" body, Figma 1388:7582).
  static const TextStyle bodyBeige = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    height: 1.5,
  );
  static const TextStyle labelBeigeMedium = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
  );
  // The on-navy white row label (accessibility card labels + toggle titles,
  // Figma 1116:16630) — the white sibling of [labelBeigeMedium].
  static const TextStyle labelWhiteMedium = TextStyle(
    color: Colors.white,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
  );

  // #16 sweep — shared named text styles (tone / weight / size) for the browse
  // features. Each bundles only colour/size/weight/height; the font family
  // stays on the theme.
  static const TextStyle labelGoldMedium = TextStyle(
    color: accent,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
  );
  static const TextStyle labelGoldSemibold = TextStyle(
    color: accent,
    fontSize: textMd,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelGoldSemiboldSm = TextStyle(
    color: accent,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelBeigeSemibold = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelBeigeBold = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyBeigeSm = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w400,
    height: 1.3,
  );
  static const TextStyle bodyBeigeXs = TextStyle(
    color: beigeBorder,
    fontSize: textXs,
  );
  static const TextStyle labelWhiteSemibold = TextStyle(
    color: surface,
    fontSize: textMd,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelWhiteSemiboldSm = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle bodyWhiteXs = TextStyle(
    color: surface,
    fontSize: textXs,
  );
  static const TextStyle labelNavyBoldSm = TextStyle(
    color: navy,
    fontSize: textSm,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelNavySemibold = TextStyle(
    color: navy,
    fontSize: textMd,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelWhiteBold = TextStyle(
    color: surface,
    fontSize: textMd,
    fontWeight: FontWeight.w700,
    height: 1.3,
  );
  static const TextStyle bodySm = TextStyle(
    fontSize: textSm,
    height: 1.4,
  );
  static const TextStyle labelWhiteSemiboldXs = TextStyle(
    color: surface,
    fontSize: textXs,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelNavySemiboldSm = TextStyle(
    color: navy,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelBeigeSemiboldSm = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelGoldBoldLg = TextStyle(
    color: accent,
    fontSize: textLg,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelGoldBoldTitle = TextStyle(
    color: accent,
    fontSize: textTitle,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelGoldSemiboldTitle = TextStyle(
    color: accent,
    fontSize: textTitle,
    fontWeight: FontWeight.w600,
    height: 1,
  );
  static const TextStyle labelWhiteSemiboldSmTall = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
    height: 1.2,
  );
  static const TextStyle labelBeigeSm = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
  );
  // Colourless bullet bases — the [ArchiveBullet] text colour is a runtime
  // param, so these carry only size/weight/height and take `.copyWith(color:)`.
  static const TextStyle bulletTitle = TextStyle(
    fontSize: textLg,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );
  static const TextStyle bulletBody = TextStyle(
    fontSize: textMd,
    fontWeight: FontWeight.w500,
    height: 1.4,
  );
  // Delegations (Figma 1426:10838/10781) — some sizes are off the type scale
  // (15/11/10/9); preserved exactly here, the single source of truth.
  static const TextStyle labelWhiteBold15 = TextStyle(
    color: surface,
    fontSize: 15,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelWhiteBoldSm = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelBeigeMediumSm = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w500,
  );
  static const TextStyle labelBeigeMedium10 = TextStyle(
    color: beigeBorder,
    fontSize: 10,
    fontWeight: FontWeight.w500,
  );
  static const TextStyle labelGoldBoldXl = TextStyle(
    color: accent,
    fontSize: textXl,
    fontWeight: FontWeight.w700,
  );
  // venue map (Figma 758:1358) — light info-card subtitle + node-marker
  // caption.
  static const TextStyle bodyGreySm = TextStyle(
    color: greyText,
    fontSize: textSm,
  );
  static const TextStyle bodyInkMuted = TextStyle(
    color: inkMuted,
  );
  static const TextStyle labelWhiteSemibold9 = TextStyle(
    color: surface,
    fontSize: 9,
    fontWeight: FontWeight.w600,
  );

  /// The AR / EN pill on the language toggle. Sits between
  /// [labelWhiteSemibold9] and [labelBeigeMedium10]: white like the former,
  /// 10pt like the latter, and neither of those combinations existed.
  static const TextStyle labelWhiteSemibold10 = TextStyle(
    color: surface,
    fontSize: 10,
    fontWeight: FontWeight.w600,
  );
  // Colourless (inherit the ambient text colour, exactly as the inline styles
  // they replace did) — the booth-sheet title + code chip.
  static const TextStyle titleBold = TextStyle(
    fontSize: textLg,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle codeLabelSm = TextStyle(
    fontSize: textSm,
    fontWeight: FontWeight.w700,
  );
  // exhibition detail (Figma 1439:11881/11826) — entity name, logo initials,
  // tier-pill label + about-card header.
  static const TextStyle labelWhiteBoldXl = TextStyle(
    color: surface,
    fontSize: textXl,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelWhiteBoldXxl = TextStyle(
    color: surface,
    fontSize: textXxl,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelGoldBold = TextStyle(
    color: accent,
    fontSize: textMd,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelWhiteMediumLg = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w500,
  );
  // speakers (Figma 908:1744 / 908:2110 / 1776:5036) — list card, profile header,
  // CV body, sessions heading + meeting-request sheet/picker.
  static const TextStyle labelWhiteSemiboldLg = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelWhiteSemiboldTitle = TextStyle(
    color: surface,
    fontSize: textTitle,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelWhiteBoldLg = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyWhite = TextStyle(
    color: surface,
    fontSize: textMd,
    height: 1.5,
  );
  static const TextStyle labelBeigeBoldXsTracked = TextStyle(
    color: beigeBorder,
    fontSize: textXs,
    fontWeight: FontWeight.w700,
    letterSpacing: 0.8,
  );
  static const TextStyle labelInkSemiboldTitle = TextStyle(
    color: headlineInk,
    fontSize: textTitle,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelInkSemibold = TextStyle(
    color: headlineInk,
    fontSize: textMd,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelNavyMediumSm = TextStyle(
    color: navy,
    fontSize: textSm,
    fontWeight: FontWeight.w500,
  );
  static const TextStyle bodyInputMd = TextStyle(
    color: inputInk,
    fontSize: textMd,
  );
  static const TextStyle bodyGreyMd = TextStyle(
    color: greyText,
    fontSize: textMd,
  );
  // splash / onboarding (Figma 159:573 / 148:22) — brand splash + intro carousel.
  static const TextStyle bodyBeigeLg = TextStyle(
    color: beigeBorder,
    fontSize: textLg,
  );
  static const TextStyle labelWhiteSemibold24Tall = TextStyle(
    color: surface,
    fontSize: text24,
    fontWeight: FontWeight.w600,
    height: 1.5,
  );
  static const TextStyle bodyBeigeTitleTall = TextStyle(
    color: beigeBorder,
    fontSize: textTitle,
    height: 1.5,
  );
  // forum guide (Figma 1388:7503/7512) — gold banner + numbered step cards.
  static const TextStyle labelWhiteMediumTall = TextStyle(
    color: surface,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
    height: 1.4,
  );
  static const TextStyle labelWhiteBoldMd = TextStyle(
    color: surface,
    fontSize: textMd,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyBeigeSm14 = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    height: 1.4,
  );
  // news (Figma 957:2197 / article) — category chip + article category/title.
  static const TextStyle labelWhiteBoldXs = TextStyle(
    color: surface,
    fontSize: textXs,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelGoldBoldXs = TextStyle(
    color: accent,
    fontSize: textXs,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle titleBoldXl = TextStyle(
    fontSize: textXl,
    fontWeight: FontWeight.w700,
  );
  // media partners (Figma 958:2263) — partner name + initials fallback tile.
  static const TextStyle labelWhiteSemiboldSm13 = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
    height: 1.3,
  );
  static const TextStyle labelNavyBoldTracked = TextStyle(
    color: navy,
    fontSize: textMd,
    fontWeight: FontWeight.w700,
    letterSpacing: 0.5,
  );
  // contact us (Figma 1388:7711) — message-form input + hint.
  static const TextStyle bodyWhiteMd = TextStyle(
    color: surface,
    fontSize: textMd,
  );
  static const TextStyle hintBeige = TextStyle(
    color: beigeBorder,
  );
  // about (Figma 1116:16448) — card body line-heights + status badge.
  static const TextStyle bodyBeigeSm16 = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    height: 1.6,
  );
  static const TextStyle bodyBeigeSm15 = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    height: 1.5,
  );
  static const TextStyle labelNavyDeepBoldSm = TextStyle(
    color: navyDeep,
    fontSize: textSm,
    fontWeight: FontWeight.w700,
  );
  // onboarding (Figma 148:22) — colourless Skip-button label.
  static const TextStyle titleSemibold = TextStyle(
    fontSize: textLg,
    fontWeight: FontWeight.w600,
  );

  // ── #16 sweep — S3 signed-in features ──────────────────────────────────
  // meetings (Figma 1408:9726) — the card date/time line.
  static const TextStyle labelBeigeMediumXs = TextStyle(
    color: beigeBorder,
    fontSize: textXs,
    fontWeight: FontWeight.w500,
  );
  // notifications (Figma 758:2491) — mark-all link + card timestamp line.
  static const TextStyle labelGoldSm = TextStyle(
    color: accent,
    fontSize: textSm,
  );
  static const TextStyle labelTimestampSm = TextStyle(
    color: timestampMuted,
    fontSize: textSm,
  );
  // badge (Figma 758:1469) — scan-to-enter hint on the white QR card + the
  // muted tier / ID lines on the gold identity strip.
  static const TextStyle bodyBlackLgTracked = TextStyle(
    color: Colors.black,
    fontSize: textLg,
    letterSpacing: -0.366,
  );
  static const TextStyle labelOnGoldMutedSm = TextStyle(
    color: onGoldMuted,
    fontSize: textSm,
  );
  // feedback / rate (Figma #40) — kicker + score body, the bold lead line, and
  // the navy note-chip message.
  static const TextStyle bodyBeigeMd = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
  );
  static const TextStyle labelWhiteBoldTitleTall = TextStyle(
    color: surface,
    fontSize: textTitle,
    fontWeight: FontWeight.w700,
    height: 1.4,
  );
  static const TextStyle labelBeigeSemiboldSmTall = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );
  // chatbot (Figma 1064:13275) — gold "AI" badge, composer input/hint (surface
  // 12; [bodyWhiteSm] also serves home's white captions), quick-reply chip.
  static const TextStyle labelWhiteBold12Tall = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w700,
    height: 16 / 12,
  );
  static const TextStyle bodyWhiteSm = TextStyle(
    color: surface,
    fontSize: textSm,
  );
  static const TextStyle labelBeigeSemibold12Tall = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w600,
    height: 18 / 12,
  );
  // contacts (SIMF-FDS-014) — empty-state title (bare bold, also requests'
  // submit label), contact-card avatar initials, job-title + note lines, and
  // the share-hint caption.
  static const TextStyle emphasisBold = TextStyle(
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelNavyBold = TextStyle(
    color: navy,
    fontWeight: FontWeight.w700,
  );

  /// Gold bold with NO size — for a span inside a richer line that should keep
  /// the parent's size (the OTP resend countdown). Sized siblings live above as
  /// labelGoldBold / labelGoldBoldLg / labelGoldBoldXl.
  static const TextStyle labelGoldBoldInherit = TextStyle(
    color: accent,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyInkMutedSm = TextStyle(
    color: inkMuted,
    fontSize: textSm,
  );
  static const TextStyle labelInkMutedBoldXs = TextStyle(
    color: inkMuted,
    fontSize: textXs,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyWhite70 = TextStyle(
    color: white70,
  );
  // ai_summary (Figma 1072:14628 / 1388:8392) — list card title/category, day
  // header, session label, agenda rows, section heading, bullets + paragraph.
  // [labelWhiteMediumSm] + [labelGoldSemiboldLg] also serve home.
  static const TextStyle labelWhiteMediumSm = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w500,
  );
  static const TextStyle labelGoldSemiboldLg = TextStyle(
    color: accent,
    fontSize: textLg,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelBeigeBoldSm = TextStyle(
    color: beigeBorder,
    fontSize: textSm,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle bodyBeigeRegularTall = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w400,
    height: 1.5,
  );
  static const TextStyle bodyBeigeMediumTall = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
    height: 1.5,
  );
  // requests (Figma 1408:9773+) — card date line + cancel-action danger label.
  static const TextStyle labelBeigeSemiboldXs = TextStyle(
    color: beigeBorder,
    fontSize: textXs,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle bodyDanger = TextStyle(
    color: danger,
  );

  /// Colour-only bodies for text inside a sized parent (list tiles, inline
  /// hints) — the size comes from the surrounding style, as with
  /// [bodyInkMuted].
  static const TextStyle bodyGrey = TextStyle(
    color: greyText,
  );
  static const TextStyle bodyHeadlineInk = TextStyle(
    color: headlineInk,
  );

  /// The small inline validation error under a form field — the single most
  /// repeated hand-rolled style in the app (18 sites across the auth, sign-up
  /// and badge screens all spelled it out).
  static const TextStyle labelDangerSm = TextStyle(
    color: danger,
    fontSize: textSm,
  );
  // home (Figma 758:1239) — highlights carousel slide title. Most home text
  // reuses S3 tokens (labelWhiteMediumSm, labelGoldSemiboldLg, bodyWhiteSm,
  // labelGoldBoldLg, labelGoldBold, labelWhiteSemibold, bodyWhiteMd,
  // bodyBeige).
  static const TextStyle labelWhiteBoldLgTall = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w700,
    height: 1.3,
  );

  // ── #16 sweep — S4 sessions-wave features ──────────────────────────────
  // questions (Figma 934:3668 / 942:3746) — the fixed tinted question box,
  // the gold inline-span colour, the colourless submit label (rides the
  // theme font + FilledButton foreground per D-546/D-549), and the tight
  // numbered session-data line (leading 1.3, tighter than the 1.5 body).
  /// tinted question textarea box
  static const double questionBoxHeight = 100;
  static const TextStyle textAccent = TextStyle(color: accent);
  static const TextStyle labelSemiboldSm = TextStyle(
    fontSize: textSm,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle bodyBeigeMedium13 = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
    height: 1.3,
  );
  // gates (Figma 758:4651+) — the gold QR-glyph tile box, the 2px emphasis
  // border (shared with the moderator boxes), and the disabled / subtle-tint
  // opacity stops on the setup + verdict cards.
  /// setup gold QR-glyph tile box
  static const double qrTileSize = 134;
  // 2px emphasis border (gate tile, moderator boxes).
  static const double borderThick = 2;
  /// disabled control (Opacity)
  static const double opacityDisabled = 0.5;
  /// disabled button fill alpha
  static const double opacityDisabledFill = 0.4;
  /// disabled button text alpha
  static const double opacityDisabledText = 0.6;
  /// gold/verdict subtle tint fill
  static const double fillOpacitySubtle = 0.08;
  // moderation — the moderator desk (Figma 805:1876 / 1461:12565 / 1462:12236):
  // filter-chip bar, navy header, question card + its three action buttons.
  // radius5 is shared with the live AI-caption badge.
  static const double radius5 = 5;
  static const double moderatorFilterChipHeight = 58;
  /// off-scale hairline
  static const double moderatorChipBorderWidth = 1.18;
  /// chip count-badge square
  static const double moderatorCountBadgeSize = 28;
  /// gold card top accent
  static const double moderatorCardTopBorderWidth = 8;
  static const double moderatorActionButtonHeight = 88;
  static const double moderatorActionShadowBlur = 10;
  static const double moderatorActionShadowOffsetY = 8;
  static const double moderatorCountBadgeActiveOpacity = 0.3;
  // 25% overlay — question-box inset fill + on-stage button shadow.
  static const double moderatorScrimOpacity = 0.25;
  static const double moderatorActionRestingFillOpacity = 0.1;
  static const TextStyle labelWhiteBoldHero = TextStyle(
    color: surface,
    fontSize: textHero,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelWhiteExtraBoldHero = TextStyle(
    color: surface,
    fontSize: textHero,
    fontWeight: FontWeight.w800,
  );
  static const TextStyle labelWhiteBoldHeroTall = TextStyle(
    color: surface,
    fontSize: textHero,
    fontWeight: FontWeight.w700,
    height: 1.5,
  );
  static const TextStyle labelWhiteBoldTitle = TextStyle(
    color: surface,
    fontSize: textTitle,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelBeigeSemibold24 = TextStyle(
    color: beigeBorder,
    fontSize: text24,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelGoldTitle = TextStyle(
    color: accent,
    fontSize: textTitle,
  );
  // live (Figma 934:3450+) — LIVE/language-chip pads, the 16:9 player band
  // (5 uses), the fixed HH:mm time-chip + AI-caption badge boxes, and the two
  // white body styles. bodyWhiteRegularSm is shared with the sessions timeline.
  /// off-grid 5px micro-gap
  static const double gap5 = 5;
  /// off-grid 10px chip padding
  static const double gap10 = 10;
  /// live player band
  static const double videoAspectRatio = 16 / 9;
  /// upcoming-session HH:mm chip
  static const double timeChipWidth = 53;
  static const TextStyle bodyWhiteRegularSm = TextStyle(
    color: surface,
    fontSize: textSm,
    fontWeight: FontWeight.w400,
  );
  // sessions (Figma 758:5307+) — seat-map caps / legend swatches / row-label
  // column / seat corner; the my-seat + header action chip; the day banner +
  // calendar-strip cell; the meta icon box; type-tab; time rail; seat marker.
  /// my-seat + header action chip
  static const double actionChipHeight = 34;
  /// my-seat seat-square cap
  static const double seatCapDefault = 40;
  /// seat-picker seat-square cap
  static const double seatCapPicker = 52;
  static const double seatViewportMaxHeight =
      340; // seat-map viewport height before the grid scrolls vertically
  /// my-seat legend swatch
  static const double seatSwatchSm = 14;
  /// picker + available legend swatch
  static const double seatSwatchLg = 16;
  // D-771 — the staff seating desk (tablet): the body's reading-width cap and
  // the guest-photo square on the result card.
  static const double staffSeatingMaxWidth = 960;
  static const double staffSeatingPhotoSize = 64;
  /// seat-map row column (1 letter)
  static const double seatRowLabelWidth = 12;
  static const double seatRowLabelCharWidth =
      10; // row column grows this-per-char for multi-char labels (VVIP/A001)
  /// seat square / legend swatch corner
  static const double radiusSeat = 3;
  /// reservation seat-marker inner
  static const double seatMarkerInner = 20;
  /// programme day banner
  static const double dayBannerHeight = 85;
  /// programme calendar-day cell
  static const double dayStripCellWidth = 52;
  /// session-card meta icon box
  static const double metaIconBox = 24;
  /// session type-tab cell
  static const double typeTabHeight = 41;
  /// timeline time-rail min height
  static const double timeRailMinHeight = 44;
  /// timeline time-rail column width
  static const double timeRailWidth = 48;
  /// 50% (resting favourite heart)
  static const double opacityHalf = 0.5;
  /// reservation seat-marker fill
  static const double seatFillOpacity = 0.15;
  /// centred numeral inside a seat cell
  static const double seatNumberSize = 18;
  /// reserved/your-seat LEGEND icon
  static const double seatStateIconSize = 12;
  /// reserved/your-seat in-cell icon
  static const double seatCellIconSize = 24;
  // Western-digit seat numeral (w600, height 1) centred in the square. OnDark =
  // light on the reserved/available dark cells; OnGold = navy on the gold
  // selected / "mine" cell.
  static const TextStyle seatNumberOnDark = TextStyle(
    color: surface,
    fontSize: seatNumberSize,
    fontWeight: FontWeight.w600,
    height: 1,
  );
  static const TextStyle seatNumberOnGold = TextStyle(
    color: navy,
    fontSize: seatNumberSize,
    fontWeight: FontWeight.w600,
    height: 1,
  );
  static const TextStyle labelBeigeSemiboldLg = TextStyle(
    color: beigeBorder,
    fontSize: textLg,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle bodyGold = TextStyle(
    color: accent,
    fontSize: textMd,
  );
  static const TextStyle labelWhiteSemiboldLgTall = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );
  static const TextStyle labelWhiteExtraboldLg = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w800,
  );
  static const TextStyle labelWhiteBlackLg = TextStyle(
    color: surface,
    fontSize: textLg,
    fontWeight: FontWeight.w900,
  );
  static const TextStyle labelGoldMediumTall = TextStyle(
    color: accent,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
    height: 1.3,
  );
  // #16 sweep (2026-07-30) — the shared app/widgets layer. The navigation
  // drawer's heading was the last inline TextStyle there; this token is its
  // exact value (surface / 20 / w600), so the render is unchanged.
  static const TextStyle labelWhiteSemiboldXl = TextStyle(
    color: surface,
    fontSize: textXl,
    fontWeight: FontWeight.w600,
  );

  // ── Per-site metrics (SIMF-CQP-001 W4c) ────────────────────────────
  // Named for the thing each one measures, following the convention
  // above (seatStateIconSize, metaIconBox). Every value is the literal
  // it replaced, so the render is unchanged.
  static const double aboutHeaderSize = 22;
  static const double accountAuthPromptWidth = 6;
  static const double accountFormFieldSize = 16;
  static const double accountHeaderSize = 44;
  static const double accountRememberForgotHeight = 19;
  static const double accountRememberForgotWidthLg = 19;
  static const double accountRememberForgotWidthMd = 5;
  static const double accountRememberForgotWidthSm = 1.5;
  static const double accountSubHeaderHeight = 56;
  static const double accountTermsCheckboxFontSize = 13;
  static const double accountTermsCheckboxHeightMd = 19;
  static const double accountTermsCheckboxHeightSm = 6;
  static const double accountTermsCheckboxWidthMd = 19;
  static const double accountTermsCheckboxWidthSm = 1.5;
  static const double accountTopControlsSize = 24;
  static const double activeFilterChipSize = 14;
  static const double archiveBulletHeight = 5;
  static const double archiveBulletWidth = 5;
  static const double archiveGalleryRowHeight = 104;
  static const double archiveGalleryTileHeight = 104;
  static const double archiveGalleryTileSize = 28;
  static const double archiveGalleryTileWidth = 104;
  static const double archivePastSpeakerCardHeight = 72;
  static const double archivePastSpeakerCardWidth = 72;
  static const double askHostCardSize = 24;
  static const double attachBoxHeight = 56;
  static const double attachBoxSize = 24;
  static const double authChromeStrokeWidth = 2;
  static const double authChromeWidthSm = 10;
  static const double badgeActionsSize = 24;
  static const double badgeActivationScreenHeight = 48;
  static const double badgeActivationScreenMaxWidth = 560;
  static const double badgeBoxHeight = 53;
  static const double badgeBoxWidth = 53;
  static const double badgePasswordScreenHeight = 48;
  static const double badgePasswordScreenMaxWidth = 560;
  static const double badgeQrCardSize = 64;
  static const double beigeTabsHeight = 34;
  static const double biometricStepUpScreenHeight = 48;
  static const double biometricStepUpScreenMaxWidth = 560;
  static const double boothContactBoxSize = 16;
  static const double boothGuideButtonSize = 18;
  static const double cameraErrorCardSize = 40;
  static const double carouselDotsHeight = 6;
  static const double centreActionHeight = 56;
  static const double centreActionWidth = 56;
  static const double changeSeatButtonSize = 18;
  static const double channelRowSize = 18;
  static const double chatComposerSize = 14;
  static const double chatComposerStrokeWidth = 2;
  static const double comingSoonScreenHeight = 80;
  static const double comingSoonScreenWidth = 80;
  static const double contactCardRadius = 26;
  static const double contactSendMessageCardStrokeWidth = 2;
  static const double contactTileHeight = 52;
  static const double contactTileSize = 24;
  static const double contactTileWidth = 0.8;
  static const double contactsEmptyStateSize = 56;
  static const double countryFlagTileFontSize = 28;
  static const double cvTabHeight = 1.2;
  static const double dateOfBirthFieldSize = 18;
  static const double dayBannerFallbackSize = 28;
  static const double delegationMeetingRequestSheetHeightSm = 5;
  static const double delegationMeetingRequestSheetMaxHeight = 264;
  static const double delegationMeetingRequestSheetSize = 18;
  static const double delegationMeetingRequestSheetStrokeWidth = 2;
  static const double delegationMeetingRequestSheetWidthMd = 80;
  static const double delegationOptionTileFontSize = 22;
  static const double delegationsStatsStripFontSize = 14;
  static const double deviceRowStrokeWidth = 2;
  static const double emailOtpVerifyScreenHeightMd = 64;
  static const double emailOtpVerifyScreenHeightSm = 48;
  static const double emailOtpVerifyScreenMaxWidth = 560;
  static const double entityIdentityCardHeight = 108;
  static const double entityIdentityCardWidth = 108;
  static const double entityLinkRowSize = 18;
  static const double faqTileSize = 20;
  static const double favouriteHeartButtonSize = 16;
  static const double fileIconSize = 20;
  static const double flagBadgeHeight = 48;
  static const double flagBadgeWidth = 48;
  static const double flagBoxFontSize = 28;
  static const double flagBoxHeight = 48;
  static const double flagBoxWidth = 48;
  static const double forgotPasswordScreenHeight = 48;
  static const double forgotPasswordScreenMaxWidth = 560;
  static const double forgotPasswordScreenSize = 18;
  static const double forgotPasswordScreenWidth = 6;
  static const double forumGuideCardsSizeMd = 20;
  static const double forumGuideCardsSizeSm = 14;
  static const double galleryPlaceholderBoxSize = 32;
  static const double gateDirectionButtonSize = 18;
  static const double gateResultViewSize = 84;
  static const double gateScanScreenSizeMd = 26;
  static const double gateScanScreenSizeSm = 18;
  static const double gateSetupViewSizeLg = 60;
  static const double gateSetupViewSizeMd = 22;
  static const double gateSetupViewSizeSm = 18;
  static const double guestHomeSize = 32;
  static const double guestModeScreenHeightMd = 64;
  static const double guestModeScreenHeightSm = 1.7;
  static const double guestModeScreenSize = 30;
  static const double guestModeScreenWidthMd = 64;
  static const double guestModeScreenWidthSm = 1.5;
  static const double highlightSlideHeight2 = 18;
  static const double highlightSlideSize = 28;
  static const double highlightSlideStrokeWidth = 2;
  static const double highlightSlideWidth = 18;
  static const double homeBannersSize = 24;
  static const double hubRowSize = 20;
  static const double iconBoxHeight = 44;
  static const double iconBoxSize = 20;
  static const double iconBoxSize2 = 16;
  static const double iconBoxWidth = 44;
  static const double identityCaptureViewFontSizeMd = 26;
  static const double identityCaptureViewFontSizeSm = 15;
  static const double identityCaptureViewHeightMd = 1440;
  static const double identityCaptureViewHeightSm = 6;
  static const double identityCaptureViewWidthLg = 1080;
  static const double identityCaptureViewWidthSm = 10;
  static const double identityFallbackViewSize = 56;
  static const double identityVerificationScreenFontSize = 30;
  static const double identityVerificationScreenSize = 32;
  static const double infoRowSize = 18;
  static const double interestChipWidth = 1.2;
  static const double liveBadgesHeight = 7;
  static const double liveBadgesSize = 14;
  static const double liveBadgesWidth = 7;
  static const double liveContentHeightMd = 5;
  static const double liveContentHeightSm = 1.5;
  static const double liveContentSizeMd = 56;
  static const double liveContentSizeSm = 18;
  static const double liveContentWidth = 5;
  static const double mediaTabHeight = 48;
  static const double meetingCardSizeLg = 38;
  static const double meetingCardSizeMd = 20;
  static const double meetingCardSizeSm = 12;
  static const double meetingConfirmScreenSize = 64;
  static const double meetingRequestSheetHeightSm = 5;
  static const double meetingRequestSheetMaxHeight = 264;
  static const double meetingRequestSheetSize = 18;
  static const double meetingRequestSheetStrokeWidth = 2;
  static const double meetingRequestSheetWidthMd = 80;
  static const double messageSurfaceSize = 40;
  static const double metaItemSize = 14;
  static const double metaLineSize = 14;
  static const double moderatedSessionTileSize = 32;
  static const double moderatorActionButtonSize = 30;
  static const double moderatorHeaderSize = 26;
  static const double moderatorQuestionCardHeight = 80;
  static const double moderatorQuestionCardWidth = 80;
  static const double moreListHeight = 48;
  static const double moreListSize = 22;
  static const double moreProfileCardSizeSm = 24;
  static const double myAreaIdentityCardHeight = 48;
  static const double myAreaIdentityCardSize = 18;
  static const double myAreaIdentityCardWidthLg = 48;
  static const double myAreaIdentityCardWidthMd = 0.5;
  static const double myAreaIdentityCardWidthSm = 0.2;
  static const double myAreaRowsHeight = 48;
  static const double myAreaRowsSize = 20;
  static const double myMobileScreenFontSize = 15;
  static const double myMobileScreenMaxWidth = 560;
  static const double navyPasswordToggleSize = 18;
  static const double newsImageFallbackSize = 28;
  static const double newsThumbnailHeight = 18;
  static const double newsThumbnailStrokeWidth = 2;
  static const double newsThumbnailWidth = 18;
  static const double notificationCategoryIconSize = 20;
  static const double onboardingScreenSize = 136;
  static const double onboardingTopBarSize = 20;
  static const double operationalHomesSize = 32;
  static const double organisationTypeaheadFieldHeight = 14;
  static const double organisationTypeaheadFieldSize = 18;
  static const double organisationTypeaheadFieldStrokeWidth = 2;
  static const double organisationTypeaheadFieldWidthMd = 14;
  static const double organisationTypeaheadFieldWidthSm = 10;
  static const double otpCodeBoxesHeightMd = 96;
  static const double otpCodeBoxesHeightSm = 52;
  static const double otpCodeBoxesSize = 34;
  static const double otpCodeBoxesWidthLg = 96;
  static const double otpCodeBoxesWidthMd = 1.5;
  static const double otpCodeBoxesWidthSm = 1.2;
  static const double partnerCardHeight = 18;
  static const double partnerCardStrokeWidth = 2;
  static const double partnerCardWidth = 18;
  static const double pastSpeakerOverflowHeight = 72;
  static const double pastSpeakerOverflowWidth = 72;
  static const double pendingApprovalCardSize = 24;
  static const double plateNumberFieldWidth = 92;
  static const double playGlyphHeight = 52;
  static const double playGlyphSize = 30;
  static const double playGlyphWidth = 52;
  static const double playerErrorSize = 36;
  static const double playerLoadingHeight = 52;
  static const double playerLoadingSize = 22;
  static const double playerLoadingWidth = 52;
  static const double profileTypeFieldStrokeWidth = 2;
  static const double programmeDayBannerSize = 16;
  static const double rateCategoryRowSize = 18;
  static const double rateGoldButtonStrokeWidth = 2;
  static const double rateNavyNoteChipSize = 16;
  static const double rateScreenSize = 30;
  static const double registrationPrimaryButtonHeight = 48;
  static const double registrationSecondaryButtonHeight = 48;
  static const double registrationStatusHeaderSize = 20;
  static const double registrationStatusHeaderSplashRadius = 22;
  static const double registrationStatusHeaderWidth = 48;
  static const double registrationStatusHeroHeightMd = 104;
  static const double registrationStatusHeroHeightSm = 1.5;
  static const double registrationStatusHeroSize = 40;
  static const double registrationStatusHeroWidthMd = 104;
  static const double registrationStatusHeroWidthSm = 2.36;
  static const double registrationStatusScreenMaxWidth = 480;
  static const double registrationSuccessBodyMaxWidth = 400;
  static const double registrationSuccessHeaderHeight = 56;
  static const double registrationSuccessHeaderSize = 20;
  static const double registrationSuccessMarkHeight = 104;
  static const double registrationSuccessMarkSize = 40;
  static const double registrationSuccessMarkWidthMd = 104;
  static const double registrationSuccessMarkWidthSm = 2.4;
  static const double requestActionRowSize = 14;
  static const double requestCardSizeMd = 20;
  static const double requestCardSizeSm = 16;
  static const double resetPasswordScreenHeight = 48;
  static const double resetPasswordScreenMaxWidth = 560;
  static const double scanContactScreenStrokeWidth = 2;
  static const double scanLineBlurRadius = 8;
  static const double scanLineHeight = 2;
  static const double scannerHeaderFontSize = 24;
  static const double scannerHeaderHeight = 56;
  static const double scannerHeaderSize = 20;
  static const double seatPickerScreenSize = 20;
  static const double sessionArrivalActionSize = 20;
  static const double sessionBookingActionsSize = 24;
  static const double sessionDetailHeaderSize = 22;
  static const double sessionReservationCardSize = 20;
  static const double sessionSpeakerCardSize = 14;
  static const double sessionTimelineRowSize = 14;
  static const double sessionTimeoutOverlayMaxWidth = 360;
  static const double sessionsActionsSize = 18;
  static const double sessionsPillSize = 14;
  static const double sessionsSearchFieldSize = 18;
  static const double shareMyContactScreenSize = 240;
  static const double shareMyContactScreenStrokeWidth = 2;
  static const double signInAltActionsHeight = 48;
  static const double signInAltActionsSize = 20;
  static const double signInScreenMaxWidth = 560;
  static const double signUpEmailVerifyScreenHeight = 48;
  static const double signUpEmailVerifyScreenMaxWidth = 560;
  static const double signUpEmailVerifyScreenWidth = 6;
  static const double signUpFormScreenMaxWidth = 560;
  static const double signUpInterestsScreenExtent = 43;
  static const double signUpInterestsScreenMaxWidth = 560;
  static const double signUpVisitorHeaderAvatarSize = 24;
  static const double signUpVisitorScreenFontSize = 13;
  static const double signUpVisitorScreenMaxWidth = 560;
  // The decorative diagonal sweep block: one 313x323 box drawn by four
  // surfaces — the auth chrome, the page shell, Terms and the
  // registration-success screen. It carried four pairs of names before, one
  // per caller, which read as four different sizes.
  static const double sweepBlockWidth = 313;
  static const double sweepBlockHeight = 323;
  static const double simfBottomNavHeight = 64;
  static const double simfBottomNavItemSize = 24;
  static const double simfCardsHeightMd = 64;
  static const double simfCardsHeightSm = 48;
  static const double simfCardsSizeMd = 24;
  static const double simfCardsSizeSm = 16;
  static const double simfCardsWidth = 72;
  static const double simfCheckboxTileFontSize = 14;
  static const double simfCheckboxTileHeight = 19;
  static const double simfCheckboxTileWidthLg = 19;
  static const double simfCheckboxTileWidthSm = 1.5;
  static const double simfFilterSearchFieldHeight = 48;
  static const double simfFilterSearchFieldSize = 16;
  static const double simfFormScaffoldMaxWidth = 560;
  static const double simfIdentityCellSize = 20;
  static const double simfLanguageToggleWidth = 48;
  static const double simfPageShellHeightSm = 42;
  static const double simfPageShellSizeMd = 24;
  static const double simfPageShellSizeSm = 20;
  static const double simfPageShellWidthSm = 42;
  static const double simfRadioPillFontSize = 14;
  static const double simfRadioPillHeightLg = 48;
  static const double simfRadioPillHeightMd = 18;
  static const double simfRadioPillHeightSm = 10;
  static const double simfRadioPillWidthMd = 10;
  static const double simfRadioPillWidthSm = 1.2;
  static const double simfRadioPillWidthXl = 18;
  static const double simfScannerFrameBlurRadius = 60;
  static const double simfScannerFrameFontSize = 12;
  static const double simfScannerFrameHeightLg = 28;
  static const double simfScannerFrameHeightSm = 6;
  static const double simfScannerFrameSize = 64;
  static const double simfScannerFrameWidth = 28;
  static const double simfSearchFieldMinHeight = 44;
  static const double simfSearchFieldMinWidth = 44;
  static const double simfSearchFieldSizeMd = 18;
  static const double simfSearchFieldSizeSm = 14;
  static const double simfStatesSize = 56;
  static const double sizeChipHeight = 36;
  static const double socialButtonHeight = 48;
  static const double socialButtonSize = 20;
  static const double socialButtonWidth = 48;
  static const double speakerAvatarHeight = 125;
  static const double speakerAvatarWidth = 125;
  static const double speakerListCardSize = 20;
  static const double speakerOptionTileSizeMd = 40;
  static const double speakerOptionTileSizeSm = 20;
  static const double speakerProfileHeaderHeight = 42;
  static const double speakerProfileHeaderWidth = 42;
  static const double speakerProfileScreenSize = 18;
  static const double speakerSessionsSize = 18;
  static const double speakerSortControlSize = 18;
  static const double splashScreenSize = 136;
  static const double sponsorCardSize = 20;
  static const double summaryContentCardHeight = 6;
  static const double summaryContentCardWidth = 6;
  static const double summaryGenerateCardSizeMd = 20;
  static const double summaryGenerateCardSizeSm = 18;
  static const double tappableAvatarSize = 12;
  static const double tappableAvatarWidth = 1.5;
  static const double termsAndNextButtonsStrokeWidth = 2;
  static const double termsBulletCardWidth = 0.2;
  static const double termsScreenHeightSm = 56;
  static const double termsScreenSize = 20;
  static const double thumbnailHeight = 22;
  static const double thumbnailStrokeWidth = 2;
  static const double thumbnailWidth = 22;
  static const double tierPillSize = 16;
  static const double togglePillSize = 16;
  static const double unreadDotHeight = 14;
  static const double unreadDotWidth = 14;
  static const double venueMapControlsSize = 20;
  static const double venueMapInfoCardBlurRadius = 8;
  static const double venueMapInfoCardSizeMd = 20;
  static const double venueMapInfoCardSizeSm = 18;
  static const double venueMapScreenWidth = 80;
  static const double websiteLinkSize = 16;

  // ── Shared shape + control metrics (SIMF-CQP-001 W4d) ──────────────────
  /// Standard full-width button height (the auth CTA, the scanner actions, the
  /// session-timeout dialog): 7 sites shared this number.
  static const double buttonHeight = 48;

  /// The shorter secondary/text-button height (registration sign-out link).
  static const double buttonHeightCompact = 36;

  /// A fully-rounded pill. The value only has to exceed half the box height;
  /// the NAME is what says "pill", which `999` never did.
  static const double radiusPill = 999;

  /// The same intent at a smaller magnitude, where the box is short enough that
  /// 100 already rounds it fully (scanner status chip).
  static const double radiusPillSm = 100;

  /// The large sheet/overlay corner (auth sweep, terms sheet, page-shell body,
  /// registration success).
  static const double radiusSheet = 40;

  /// The scanner viewfinder card corner.
  static const double radiusScanner = 24;

  /// A page-dot corner: small enough to read as a rounded bar, not a circle.
  static const double radiusDot = 3;

  /// Decorative-blur offset behind the scanner frame.
  static const double scannerGlowOffset = 24;

  /// The venue map's pan margin around the plan, so the edges stay reachable.
  static const double venueMapPanMargin = 200;

  /// The speaker avatar's ring inset (off-scale, from the Figma frame).
  static const double avatarRingInset = 2.77;

}
