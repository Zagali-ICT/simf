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
  static const Color navy = Color(0xFF01132D); // "Primary- Color" — dark scaffold
  static const Color navyDeep = Color(0xFF192B41); // "BG" — boxes/cards on navy
  static const Color accent = Color(0xFFC9A84C); // "Secondary- Color" — gold
  static const Color ink = Color(0xFF1A2030);
  static const Color inkMuted = Color(0xFF5A6573);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color background = Color(0xFFE9ECEF);
  static const Color field = Color(0xFFEEF1F4);
  static const Color danger = Color(0xFFA8182A);
  static const Color success = Color(0xFF2E7D32);
  static const Color warning = Color(0xFFE8932A); // amber — "being answered" (Figma 1461:12227)

  // Moderator desk per-action colours — Figma 1461:12227 (Tailwind red-600 /
  // green-600 / amber-500): reject, answered, being-answered (on-stage).
  static const Color qReject = Color(0xFFDC2626);
  static const Color qAnswered = Color(0xFF16A34A);
  static const Color qStage = Color(0xFFF59E0B);

  // Request-status colours — الطلبات chips + card borders (Figma 1408:9760+,
  // Tailwind-500): accepted green / rejected red / cancelled grey. Pending
  // reuses [qStage] (#F59E0B). Chips render the colour at 12% fill + 20% border.
  static const Color statusAccepted = Color(0xFF22C55E);
  static const Color statusRejected = Color(0xFFEF4444);
  static const Color statusCancelled = Color(0xFF6B7280);

  // D-745 — bilateral-meetings card flag badge (Figma 1408:9726): the nationality
  // flag emoji on a soft green well (green #27AE60 @ 9% fill + 21% border).
  static const Color flagBadgeBg = Color(0x1727AE60);
  static const Color flagBadgeBorder = Color(0x3627AE60);

  // KSA-Project light-surface + auth-flow palette (D-358/D-359).
  static const Color navySurface = Color(0xFF102238); // elevated navy surface (login bg)
  static const Color navyHeader = Color(0xFF071832); // darker header block behind the forum title (Figma 1467:12565 / 1461:12565)
  static const Color navyFill80 = Color(0xCC01132D); // navy #01132D @ 80% — reference-number card fill (registration success 505:1525)
  static const Color navyFill70 = Color(0xB301132D); // navy #01132D @ 70% — gallery video play-circle fill (Figma 949:4059)
  static const Color navyFill90 = Color(0xE601132D); // navy #01132D @ 90% — onboarding photo overlay (Figma 148:22)
  static const Color chipBorderNavy = Color(0xFF2A4066); // muted navy border on unselected pills (interests grid, Figma 505:1222)
  static const Color tileBorderNavy = Color(0xFF253660); // contact-tile border (registration success, Figma 522:2223)
  static const Color scannerCard = Color(0xFF0F2044); // QR-scanner card fill (Figma 758:4566)
  static const Color scannerTrack = Color(0xFF132A50); // QR-scanner progress-bar track (Figma 758:4598)
  static const Color mutedBlue = Color(0xFF8A9CC0); // muted blue caption — OTP countdown + scanner status (Figma 505:987 / 758:4596)
  static const Color beigeBorder = Color(0xFFC2B8A2); // "Pragraph Color" — borders + on-navy paragraph text
  static const Color beigeFill10 = Color(0x1AC2B8A2); // beige 10% — tier-pill + link-row icon-box fill (Figma rgba(194,184,162,0.1))
  static const Color beigeBorder40 = Color(0x66C2B8A2); // beige 40% — agenda timeline row divider (Figma 1310:3239 rgba(194,184,162,0.4))
  static const Color beigeFill50 = Color(0x80C2B8A2); // beige 50% — liveness pending progress dash (Figma 758:4242 rgba(194,184,162,0.5))
  static const Color goldFill7 = Color(0x12C9A84C); // gold 7% — delegations stats-strip grid (Figma 1426:10771)
  static const Color goldFill6 = Color(0x0FC9A84C); // gold 6% — delegations head-of-delegation box fill (Figma 1426:10771)
  static const Color goldBorder15 = Color(0x26C9A84C); // gold 15% — delegations head-of-delegation box border (Figma 1426:10771)
  static const Color cardBeige = Color(0xFFF1ECE4); // light card surface
  static const Color goldSoft = Color(0xFFD0AC77); // secondary gold text/icons
  static const Color goldSoftFill50 = Color(0x80D0AC77); // goldSoft @ 50% — onboarding inactive page dot (Figma 148:22)
  static const Color headlineInk = Color(0xFF111827); // headings on light surfaces
  static const Color greyText = Color(0xFF6C7278); // secondary text on light surfaces
  static const Color calendarBand = Color(0xFFE9EAEC); // #4 — light-grey agenda day-strip band
  static const Color linkNavy = Color(0xFF00245E); // inline links on light surfaces
  static const Color inputInk = Color(0xCC111827); // input text on light surfaces (#111827 at 80%)
  static const Color onGoldMuted = Color(0xFFF0F0F0); // muted text on the gold identity strip (badge 758:1469)
  static const Color timestampMuted = Color(0xFF4C555F); // notification timestamp (758:2491)
  // Per-kind notification category-icon colours (Figma 758:2491 palette). Kept
  // distinct from the semantic success/danger so the icons match the mockup's
  // decorative per-kind styling exactly.
  static const Color notifGreen = Color(0xFF13C296); // Figma "Green/green-500"
  static const Color notifCoral = Color(0xFFFF6347); // Figma "Primary/primary-500"
  static const Color codeBoxBeige = Color(0xCCFFF4DC); // pale-beige code chip fill (venue map 758:1358, #FFF4DC @ 80%)
  static const Color cardShadow = Color(0x29111827); // soft card drop-shadow (headlineInk @ ~16%)
  static const Color dayInactive = Color(0xFFC2C2C2); // calendar day with no sessions (758:1415)
  static const Color bannerScrim = Color(0xCC001030); // day-banner bottom gradient #001030 @ 80% (Figma 1310:3232 / 1064:13240)
  static const Color navInactive = Color(0xFF5E584B); // bottom-nav inactive icon (758:1476)
  static const Color chatBubbleText = Color(0xFFF0F4FF); // assistant chat-bubble text (1064:13278)
  static const Color captionText = Color(0xFFDDE4F0); // live AI-caption placeholder text (934:3613)
  // Live-broadcast player band (Figma 934:3450): the LIVE pill is a brighter
  // brick-red than the semantic [danger]; the language chip is a translucent
  // dark glassy pill; the resting play button is a translucent-white circle.
  static const Color liveRed = Color(0xFFC0392B); // LIVE badge fill (934:3609)
  static const Color scrimBlack55 = Color(0x8C000000); // language chip fill rgba(0,0,0,0.55) (934:3604)
  static const Color playScrim = Color(0x26FFFFFF); // play-circle fill rgba(255,255,255,0.15) (934:3595)

  // KSA main-shell disabled palette (W2 frames 512:1492 / 512:1780): the
  // "بطاقتي" locked card and the disabled theme tile render on these.
  static const Color navyDisabled = Color(0xFF0A1628); // disabled card fill
  static const Color navyDisabledBorder = Color(0xFF1E3A5F); // disabled card border
  static const Color navyDisabledText = Color(0xFF4A6080); // disabled label/icon

  // On-navy hairlines + muted text + light-surface hairline, straight from
  // Mockup.html (--line / --line-2 / --txt-2 / --txt-3 / --line-light). These
  // drive the dark theme's cards, dividers, borders and secondary text.
  static const Color line = Color(0x1AFFFFFF); // white 10%
  static const Color line2 = Color(0x0FFFFFFF); // white 6%
  static const Color surfaceTint = Color(0x0AFFFFFF); // white 4% (card fill)
  static const Color txtSecondary = Color(0xA6FFFFFF); // white 65%
  static const Color txtTertiary = Color(0x66FFFFFF); // white 40%
  static const Color lineLight = Color(0x140F2238); // navy 8%

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

  // Spacing scale.
  static const double space1 = 4;
  static const double space2 = 8;
  static const double space3 = 12;
  static const double space4 = 16;
  static const double space5 = 20;
  static const double space6 = 24;
  static const double space8 = 32;
  static const double space10 = 40; // 10*4 extension of the spacing scale (onboarding/splash vertical gaps)

  // Radii.
  static const double radiusSmall = 4;
  static const double radius6 = 6; // LIVE badge (Figma 934:3609)
  static const double radius = 8;
  static const double radiusLarge = 12;
  static const double radiusLg = 16; // W2 cards / nav bar top corners
  static const double radius14 = 14; // exhibitor/sponsor link rows (Figma 1439:11904/11917)
  static const double radiusXl = 20;
  static const double radius10 = 10; // delegations head-of-delegation box (Figma 1426:10838)

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
  static const double controlHeight = 48; // pill / banner / row height (booth code-pill+hall-box, gallery coverage-tab, archive edition-pill/notice/session-title)
  static const double contactRowHeight = 44; // booth contact-box row (Figma 922:2810)
  static const double codePillWidth = 109; // booth code pill A-12 (Figma 922:2796) — fixed content width
  static const double sponsorRowHeight = 72; // sponsor hero/premium row + grid tile (Figma 922:2824)
  static const double galleryScrimHeight = 40; // archive gallery-tile bottom scrim (Figma 926:3299)
  static const double mediaTileAspectRatio = 164 / 104; // gallery media tile (Figma 949:4043)
  static const double scrimOpacityStrong = 0.8; // archive gallery-tile scrim gradient bottom stop
  static const double bulletTopNudge = 7; // archive bullet disc top-align nudge (Figma 925:3258)
  static const double headBoxPad = 9; // delegations head-of-delegation box padding (Figma 1426:10838)
  static const double gap2 = 2; // off-grid 2px micro-gap (below the space-4 scale)
  static const double gap6 = 6; // off-grid 6px micro-gap (below the space-4 scale)
  static const double gap18 = 18; // off-grid 18px gap (forum-guide step content -> caret, Figma 1426:11374)
  static const double newsThumbWidth = 155; // news card thumbnail tile (Figma 958:2202)
  static const double newsThumbHeight = 85; // news card thumbnail tile (Figma 958:2202)
  static const double partnerCardAspectRatio = 163.5 / 104; // media-partners grid tile (Figma 958:2246)
  static const double onboardCarouselHeight = 170; // onboarding step carousel viewport (Figma 148:22)
  static const double statsStripHeight = 100; // delegations decorative stats-strip map (Figma 1426:10781)
  static const double mapControlSize = 40; // venue-map floating zoom/locate control square (Figma 758:1358)
  static const double tapTarget = 44; // minimum touch-target height — venue-map direct-me button (Figma 758:1358)
  static const double dayCardWidth = 58; // meeting-request day card (Figma 1776:4975)
  static const double dayCardHeight = 64; // meeting-request day card + its picker row (Figma 1776:5052)

  // #16 S3 — chatbot (Figma 1064:13275) + quick-reply strip metrics.
  static const double radiusTail = 2; // chat-bubble inner-bottom tail corner
  static const double chatBubblePadH = 15; // chat-bubble horizontal text inset
  static const double chatBubbleMaxWidth = 288; // chat-bubble max width
  static const double sendSquareSize = 24; // composer send-button box
  static const double quickReplyStripHeight = 34; // quick-reply chip strip

  // #16 S3 — ai_summary section-heading gold bar (Figma 1072:14660).
  static const double headingBarWidth = 4;
  static const double headingBarHeight = 20;

  // #16 S3 — requests card icon-box + status-chip metrics (Figma 1408:9761+).
  static const double requestIconBox = 32; // gold type-icon box on a request card
  static const double statusChipHeight = 32; // status filter chip height
  static const double statusChipPadH = 13; // status filter chip horizontal padding
  static const double chipFillActiveOpacity = 0.24; // selected chip fill alpha
  static const double chipFillOpacity = 0.12; // resting chip fill alpha
  static const double chipBorderOpacity = 0.2; // resting chip border alpha

  // Type scale (Material text styles override these for actual rendering;
  // tokens here are for places that need a raw size).
  static const double textXs = 10.5;
  static const double textSm = 12;
  static const double textMd = 14;
  static const double textLg = 16;
  static const double textTitle = 18; // KSA "Sub-title 18" (Phase-0 type scale)
  static const double textXl = 20;
  static const double textXxl = 22; // exhibitor/sponsor name (Figma 1439:11894)
  static const double text24 = 24; // registration-status headline (Figma 1701:3803)
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
  // Colourless bullet bases — the [ArchiveBullet] text colour is a runtime param,
  // so these carry only size/weight/height and take `.copyWith(color:)`.
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
  static const TextStyle labelBeigeSemibold11 = TextStyle(
    color: beigeBorder,
    fontSize: 11,
    fontWeight: FontWeight.w600,
  );
  static const TextStyle labelGoldBold9 = TextStyle(
    color: accent,
    fontSize: 9,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle labelGoldBoldXl = TextStyle(
    color: accent,
    fontSize: textXl,
    fontWeight: FontWeight.w700,
  );
  // venue map (Figma 758:1358) — light info-card subtitle + node-marker caption.
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
    color: Colors.white70,
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
}
