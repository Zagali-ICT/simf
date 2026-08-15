import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/booths/widgets/booth_company_header.dart';
import 'package:simf_app/features/booths/widgets/booth_contact_boxes.dart';
import 'package:simf_app/features/booths/widgets/booth_guide_button.dart';
import 'package:simf_app/features/booths/widgets/booth_hall_row.dart';
import 'package:simf_app/features/booths/widgets/booth_officer_row.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';

/// One exhibitor card (frame node 922:2554): a navy box with the beige
/// hairline carrying — top to bottom — the company header (short name + full
/// name beside the square logo tile, over a gold hairline), the gold **code
/// pill** beside the deep-navy **hall box**, the booth-officer row + email /
/// phone contact boxes (D-432, shown only when the wire carries them), and a
/// full-width gold **guide-me** CTA. The logo tile renders the booth's own
/// BoothLogo (D-357), short-name initials fallback.
class BoothCard extends StatelessWidget {
  const BoothCard({
    required this.booth,
    required this.l10n,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
    required this.onGuide,
    super.key,
  });

  final BoothSummary booth;
  final AppL10n l10n;
  final bool isArabic;
  final String baseUrl;
  final VoidCallback onTap;
  final VoidCallback onGuide;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            BoothCompanyHeader(
                booth: booth, isArabic: isArabic, baseUrl: baseUrl,),
            const SizedBox(height: SimfTokens.space4),
            BoothHallRow(booth: booth, l10n: l10n),
            // D-432 — the booth-officer row + email/phone boxes (now on the
            // wire, server resolves the officer Contact-first); shown only when
            // the booth actually carries that contact data.
            if ((booth.officerName ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              BoothOfficerRow(name: booth.officerName!.trim(), l10n: l10n),
            ],
            if ((booth.officerEmail ?? '').trim().isNotEmpty ||
                (booth.officerPhone ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              BoothContactBoxes(
                email: booth.officerEmail?.trim(),
                phone: booth.officerPhone?.trim(),
              ),
            ],
            if (booth.code.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              BoothGuideButton(code: booth.code, l10n: l10n, onTap: onGuide),
            ],
          ],
        ),
      ),
    );
  }
}
