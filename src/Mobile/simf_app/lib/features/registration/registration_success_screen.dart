import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/env/build_config.dart';
import '../../core/external_link.dart';
import '../../core/site_settings/site_settings.dart';

// Success-frame colours (Figma 505:1451) not yet shared by a second screen.
const Color _green = Color(0xFF22C55E);
const Color _tileBorder = Color(0xFF253660);
const Color _refCardFill = Color(0xCC01132D); // #01132D at 80%
const Color _sweepTint = Color(0x0AFFFFFF);

/// The fallback mask when no real reference is available (offline arrival or
/// a pre-D-373 save). D-373 superseded the D-366 always-masked rule: the save
/// response now carries the DB-issued `SIMF-YYYY-NNNNNNNN` reference and the
/// interests screen passes it here via the route extra.
const String _maskedReference = 'SIMF-2026-xxxx';

/// Page 010 — تم التسجيل · Registration success. The KSA-Project Figma design
/// (node 505:1451 — D-366): green-ringed check, the success headline + the
/// review copy, the masked reference-number card, the gold حالة التسجيل and
/// outlined الانتقال للرئيسية actions, and the visual-only تواصل معانا tiles.
/// The previous screen is parked in `_legacy_mockup/`.
///
/// Contract: terminal confirmation of sign-up, offline-safe, reached as a
/// replacement. Primary action → Page_011 status; secondary → home.
/// D-373: the reference card renders the real DB-issued registration
/// reference carried from the save ([referenceNumber]); the literal mask
/// remains only as the no-data fallback so the page stays offline-safe.
class RegistrationSuccessScreen extends ConsumerWidget {
  const RegistrationSuccessScreen({super.key, this.referenceNumber});

  /// The `SIMF-YYYY-NNNNNNNN` reference issued by the save (D-373); null on
  /// an offline / out-of-flow arrival → the mask renders.
  final String? referenceNumber;

  void _back(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.go('/');
  }

  /// Opening the dialer / mail app is best-effort — a missing handler must
  /// never crash the confirmation screen (the shared D-369 helper).
  static Future<void> _launchContact(Uri uri) => launchExternalUri(uri);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    // D-461 — the CP-editable registration welcome message ("تهانينا، مرحباً
    // بكم في الملتقى السعودي الرابع"). Falls back to the bundled copy while the
    // public site-settings load or if they are unavailable (offline-safe).
    final isArabic = Localizations.localeOf(context).languageCode == 'ar';
    final configuredWelcome = ref
        .watch(siteSettingsProvider)
        .valueOrNull
        ?.messageFor(isArabic ? 'ar' : 'en');
    final welcomeMessage =
        (configuredWelcome != null && configuredWelcome.isNotEmpty)
            ? configuredWelcome
            : l10n.registrationSuccessMessage;
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
                                  border: Border.all(color: _green, width: 2.4),
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
                              welcomeMessage,
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
                              padding: const EdgeInsets.symmetric(vertical: 24),
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
                                  Text(
                                    referenceNumber ?? _maskedReference,
                                    textDirection: TextDirection.ltr,
                                    style: const TextStyle(
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
                            // Contact tiles (D-369): each opens the OS
                            // dialer / mail app when its BuildConfig value is
                            // supplied; an empty value keeps the tile inert.
                            Row(
                              children: <Widget>[
                                Expanded(
                                  child: _ContactTile(
                                    icon: Icons.call_outlined,
                                    onTap: BuildConfig.supportPhone.isEmpty
                                        ? null
                                        : () => unawaited(
                                              _launchContact(
                                                Uri(
                                                  scheme: 'tel',
                                                  path:
                                                      BuildConfig.supportPhone,
                                                ),
                                              ),
                                            ),
                                  ),
                                ),
                                const SizedBox(width: 16),
                                Expanded(
                                  child: _ContactTile(
                                    icon: Icons.mail_outline,
                                    onTap: BuildConfig.supportEmail.isEmpty
                                        ? null
                                        : () => unawaited(
                                              _launchContact(
                                                Uri(
                                                  scheme: 'mailto',
                                                  path:
                                                      BuildConfig.supportEmail,
                                                ),
                                              ),
                                            ),
                                  ),
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

/// One bordered contact tile (Figma 522:2223). Inert (null [onTap]) until its
/// BuildConfig contact value is supplied (D-369).
class _ContactTile extends StatelessWidget {
  const _ContactTile({required this.icon, this.onTap});

  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        height: 52,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          border: Border.all(color: _tileBorder, width: 0.8),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Icon(icon, color: Colors.white, size: 24),
      ),
    );
  }
}
