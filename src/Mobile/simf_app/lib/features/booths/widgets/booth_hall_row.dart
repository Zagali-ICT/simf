import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/booths/widgets/booth_hall_box.dart';
import 'package:simf_app/features/booths/widgets/code_pill.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';

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
      children: <Widget>[
        Expanded(
          child: hasBoth
              ? BoothHallBox(code: hallEn!.toUpperCase(), name: hallAr!)
              : BoothHallBox(name: fallback),
        ),
        if (code.isNotEmpty) ...<Widget>[
          const SizedBox(width: SimfTokens.space4),
          CodePill(code: code),
        ],
      ],
    );
  }
}

