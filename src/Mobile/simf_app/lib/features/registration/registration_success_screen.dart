import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../core/site_settings/site_settings.dart';
import 'widgets/registration_success_body.dart';
import 'widgets/registration_success_header.dart';

/// Page 010 — تم التسجيل · Registration success. The KSA-Project Figma design
/// (node 505:1451 — D-366): green-ringed check, the success headline + the
/// review copy, the reference-number card, the gold حالة التسجيل and outlined
/// الانتقال للرئيسية actions, and the visual-only تواصل معنا tiles.
///
/// Contract: terminal confirmation of sign-up, offline-safe, reached as a
/// replacement. Primary action → Page_011 status; secondary → home.
/// D-373: the reference card renders the real DB-issued registration reference
/// carried from the save ([referenceNumber]); the literal mask remains only as
/// the no-data fallback so the page stays offline-safe.
class RegistrationSuccessScreen extends ConsumerWidget {
  const RegistrationSuccessScreen({super.key, this.referenceNumber});

  /// The `SIMF-YYYY-NNNNNNNN` reference issued by the save (D-373); null on an
  /// offline / out-of-flow arrival → the mask renders.
  final String? referenceNumber;

  void _back(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }
    context.go('/');
  }

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
                width: SimfTokens.registrationSuccessScreenWidth,
                height: SimfTokens.registrationSuccessScreenHeight,
                decoration: BoxDecoration(
                  color: SimfTokens.surfaceTint,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSheet),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                RegistrationSuccessHeader(
                  title: l10n.regSuccessHeaderTitle,
                  onBack: () => _back(context),
                ),
                Expanded(
                  child: RegistrationSuccessBody(
                    welcomeMessage: welcomeMessage,
                    referenceNumber: referenceNumber,
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
