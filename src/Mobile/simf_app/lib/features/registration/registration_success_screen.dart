import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 010 — تم التسجيل بنجاح · Registration success (Page_010 docs).
///
/// Terminal confirmation of the sign-up journey: the profile was submitted and
/// the account is now **pending approval**. It owns **no write API** (the account
/// was already created by the profile save) and renders entirely client-side, so
/// it is offline-safe. Reached as a **replacement** (the multi-step sign-up form
/// is dropped from the back stack — no app bar back). Primary action → Page_011
/// (registration status); ghost action → home. The optional auto-advance poll is
/// deferred to Page_011, which owns the real status polling (Page_010 L-3).
class RegistrationSuccessScreen extends StatelessWidget {
  const RegistrationSuccessScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                const Icon(
                  Icons.check_circle_outline,
                  size: 72,
                  color: SimfTokens.success,
                ),
                const SizedBox(height: SimfTokens.space5),
                Text(
                  l10n.registrationSuccessTitle,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: SimfTokens.textXl,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: SimfTokens.space3),
                Text(
                  l10n.registrationSuccessMessage,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: SimfTokens.inkMuted),
                ),
                const SizedBox(height: SimfTokens.space6),
                FilledButton(
                  onPressed: () =>
                      context.goNamed(RouteNames.registrationStatus),
                  child: Text(l10n.registrationStatusButton),
                ),
                const SizedBox(height: SimfTokens.space2),
                TextButton(
                  onPressed: () => context.go('/'),
                  child: Text(l10n.goHomeButton),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
