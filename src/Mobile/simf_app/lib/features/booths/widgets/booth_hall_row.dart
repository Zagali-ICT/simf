import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../venuemap/data/venue_map_models.dart';

/// The hall row (frame node 922:2795): the gold-bordered **code pill** (A-12)
/// beside the deep-navy **hall box**. The frame's hall box reads
/// "HALL A · القاعة الرئيسية"; the wire carries no hall **name**, so the box
/// shows the booth's sector when present, else a generic "exhibition hall"
/// label — never an invented hall name (D11 / Page_015 L-6).
class BoothHallRow extends StatelessWidget {
  const BoothHallRow({required this.booth, required this.l10n, super.key});

  final BoothSummary booth;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Frame 922:2624 — the hall box reads "HALL A · القاعة الرئيسية": the hall's
    // English name (gold, upper-cased) + a beige dot + its Arabic name (beige),
    // shown together. Both ship on the wire (D-432); when a booth carries neither
    // hall name, degrade to the single localized hall name → sector → generic
    // label (D11/Page_015 L-6 — never invent a hall name).
    final hallEn = booth.hallName?.trim();
    final hallAr = booth.hallNameArabic?.trim();
    final hasBoth =
        (hallEn?.isNotEmpty ?? false) && (hallAr?.isNotEmpty ?? false);
    final fallback = booth.localizedHallName(l10n.isArabic) ??
        booth.localizedSector(l10n.isArabic) ??
        l10n.boothsHallFallback;
    final code = booth.code;
    // Both children are a fixed 48 high; the row must NOT stretch — inside the
    // card Column (unbounded height) CrossAxisAlignment.stretch would size the
    // row to infinite height and crash layout. Centre-align the two 48px boxes.
    // Frame 922:2626 — RTL: the hall box at the inline start (right); the A-12
    // code pill at the inline end (left).
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Expanded(
          child: hasBoth
              ? _HallBox(code: hallEn!.toUpperCase(), name: hallAr!)
              : _HallBox(name: fallback),
        ),
        if (code.isNotEmpty) ...<Widget>[
          const SizedBox(width: SimfTokens.space4),
          _CodePill(code: code),
        ],
      ],
    );
  }
}

/// The accent booth-number pill (frame node 922:2796): a 109-wide navy box with
/// a gold hairline, the code in gold, always LTR.
class _CodePill extends StatelessWidget {
  const _CodePill({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 109,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        code,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w600,
          fontSize: SimfTokens.textMd,
        ),
      ),
    );
  }
}

/// The deep-navy hall box (frame node 922:2624): a 48-high `#01132D` box with a
/// beige hairline. When the hall ships both names it reads "HALL A · القاعة
/// الرئيسية" — the [code] (gold) + a beige dot + the [name] (beige); otherwise it
/// falls back to a single gold [name] label.
class _HallBox extends StatelessWidget {
  const _HallBox({required this.name, this.code});

  /// The beige hall name (the frame's Arabic name), or — when [code] is null —
  /// the single gold fallback label (localized hall name / sector / generic).
  final String name;

  /// The gold hall code (the frame's upper-cased English hall name), or null for
  /// the single-tone fallback.
  final String? code;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: code == null
          ? Text(
              name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textMd,
              ),
            )
          // Frame 922:2624 — pinned LTR so the gold "HALL A" leads and the beige
          // Arabic name trails, matching the frame regardless of the app's RTL.
          : Row(
              textDirection: TextDirection.ltr,
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(
                  code!,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontWeight: FontWeight.w600,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                const Text(
                  '·',
                  style: TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Flexible(
                  child: Text(
                    name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                ),
              ],
            ),
    );
  }
}
