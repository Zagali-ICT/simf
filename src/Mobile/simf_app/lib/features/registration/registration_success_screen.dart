import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/registration/widgets/registration_success_body.dart';
import 'package:simf_app/features/registration/widgets/registration_success_header.dart';
import 'package:simf_app/features/registration/widgets/registration_success_sweep.dart';

/// Registration success — route: RouteNames.registrationSuccess · Figma
/// 505:1451
/// Contract: terminal confirmation of sign-up, offline-safe, reached as a
/// replacement (D-366). D-373 — the reference card renders the real DB-issued
/// registration reference carried from the save ([referenceNumber]); the
/// literal mask remains only as the no-data fallback.
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
          const RegistrationSuccessSweep(),
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
