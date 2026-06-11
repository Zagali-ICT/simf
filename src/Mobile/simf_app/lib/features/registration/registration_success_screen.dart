import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

// Success-frame colours (Figma 505:1451) not yet shared by a second screen.
const Color _green = Color(0xFF22C55E);
const Color _tileBorder = Color(0xFF253660);
const Color _refCardFill = Color(0xCC01132D); // #01132D at 80%
const Color _sweepTint = Color(0x0AFFFFFF);

/// The design's masked reference shown until the account is approved — the
/// real badge/reference surfaces later on the badge/status pages (owner
/// decision, D-366). A literal mask, deliberately not localized.
const String _maskedReference = 'SIMF-2026-xxxx';

/// Page 010 — تم التسجيل · Registration success. The KSA-Project Figma design
/// (node 505:1451 — D-366): green-ringed check, the success headline + the
/// review copy, the masked reference-number card, the gold حالة التسجيل and
/// outlined الانتقال للرئيسية actions, and the visual-only تواصل معانا tiles.
/// The previous screen is parked in `_legacy_mockup/`.
///
/// Contract unchanged: terminal confirmation of sign-up, **no API**,
/// offline-safe, reached as a replacement. Primary action → Page_011 status;
/// secondary → home. Owner decisions (D-366): the reference card renders the
/// design's masked value (no fetch — the page stays offline-safe) and the
/// contact tiles are visual-only until official contact details exist
/// (tracked on the programme board).
class RegistrationSuccessScreen extends StatelessWidget {
  const RegistrationSuccessScreen({super.key});

  void _back(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.go('/');
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          // Decorative diagonal sweep (Figma 505:1453, top-right area).
          Positioned(
            top: -180,
            right: -40,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Header band (Figma 505:1456): chevron left, centred title.
                SizedBox(
                  height: 56,
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding: const EdgeInsets.only(left: 8),
                          child: IconButton(
                            onPressed: () => _back(context),
                            icon: const Icon(
                              Icons.arrow_back_ios_new,
                              color: Colors.white,
                              size: 20,
                              textDirection: TextDirection.ltr,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        l10n.regSuccessHeaderTitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 24,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Center(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 400),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: <Widget>[
                            const SizedBox(height: 24),
                            // Green-ringed success mark (Figma 505:1343).
                            Center(
                              child: Container(
                                width: 104,
                                height: 104,
                                alignment: Alignment.center,
                                decoration: BoxDecoration(
                                  color: SimfTokens.navyDeep,
                                  shape: BoxShape.circle,
                                  border:
                                      Border.all(color: _green, width: 2.4),
                                ),
                                child: const Icon(
                                  Icons.check_rounded,
                                  size: 40,
                                  color: _green,
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),
                            Text(
                              l10n.registrationSuccessTitle,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                fontSize: 24,
                                fontWeight: FontWeight.w700,
                                color: Colors.white,
                              ),
                            ),
                            const SizedBox(height: 16),
                            Text(
                              l10n.registrationSuccessMessage,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: SimfTokens.beigeBorder,
                                fontSize: 14,
                                height: 1.5,
                              ),
                            ),
                            const SizedBox(height: 24),
                            // Masked reference card (Figma 505:1525, D-366).
                            Container(
                              padding:
                                  const EdgeInsets.symmetric(vertical: 24),
                              decoration: BoxDecoration(
                                color: _refCardFill,
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: Column(
                                children: <Widget>[
                                  Text(
                                    l10n.referenceNumberLabel,
                                    style: const TextStyle(
                                      color: SimfTokens.beigeBorder,
                                      fontSize: 14,
                                    ),
                                  ),
                                  const SizedBox(height: 8),
                                  const Text(
                                    _maskedReference,
                                    textDirection: TextDirection.ltr,
                                    style: TextStyle(
                                      color: SimfTokens.accent,
                                      fontSize: 16,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 32),
                            FilledButton(
                              onPressed: () => context
                                  .goNamed(RouteNames.registrationStatus),
                              child: Text(
                                l10n.registrationStatusButton,
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),
                            OutlinedButton(
                              onPressed: () => context.go('/'),
                              style: OutlinedButton.styleFrom(
                                side:
                                    const BorderSide(color: SimfTokens.accent),
                                foregroundColor: Colors.white,
                                minimumSize: const Size.fromHeight(48),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(
                                    SimfTokens.radiusSmall,
                                  ),
                                ),
                              ),
                              child: Text(
                                l10n.goHomeButton,
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            const SizedBox(height: 32),
                            Text(
                              l10n.contactUsTitle,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 16,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                            const SizedBox(height: 16),
                            // Visual-only contact tiles (D-366): no official
                            // contact details yet — wiring tracked on the
                            // programme board.
                            Row(
                              children: const <Widget>[
                                Expanded(
                                  child: _ContactTile(icon: Icons.call_outlined),
                                ),
                                SizedBox(width: 16),
                                Expanded(
                                  child: _ContactTile(icon: Icons.mail_outline),
                                ),
                              ],
                            ),
                            const SizedBox(height: 16),
                            Text(
                              l10n.simfSocialFooter,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: SimfTokens.beigeBorder,
                                fontSize: 12,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                            const SizedBox(height: 24),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// One bordered contact tile (Figma 522:2223) — visual-only until the
/// official contact details exist (D-366).
class _ContactTile extends StatelessWidget {
  const _ContactTile({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 52,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        border: Border.all(color: _tileBorder, width: 0.8),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Icon(icon, color: Colors.white, size: 24),
    );
  }
}
