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
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space5,
              vertical: SimfTokens.space6,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                // Mockup `.success .check`: an 80px circle with a green-tinted
                // fill + 2px green ring holding the success tick.
                Center(
                  child: Container(
                    width: 80,
                    height: 80,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: SimfTokens.success.withValues(alpha: 0.12),
                      border:
                          Border.all(color: SimfTokens.success, width: 2),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.check_rounded,
                      size: 36,
                      color: SimfTokens.success,
                    ),
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                Text(
                  l10n.registrationSuccessTitle,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: SimfTokens.textXl,
                    fontWeight: FontWeight.w700,
                    color: SimfTokens.surface,
                  ),
                ),
                const SizedBox(height: SimfTokens.space3),
                Text(
                  l10n.registrationSuccessMessage,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: SimfTokens.txtSecondary,
                    fontSize: SimfTokens.textSm,
                    height: 1.7,
                  ),
                ),
                const SizedBox(height: SimfTokens.space6),
                FilledButton(
                  onPressed: () =>
                      context.goNamed(RouteNames.registrationStatus),
                  child: Text(l10n.registrationStatusButton),
                ),
                const SizedBox(height: SimfTokens.space2),
                // Mockup `.success .actions .b.ghost`: a transparent, white-text
                // action carrying the RTL go-arrow back to home.
                TextButton(
                  onPressed: () => context.go('/'),
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.surface,
                    minimumSize: const Size.fromHeight(44),
                    textStyle: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Text(l10n.goHomeButton),
                      const SizedBox(width: SimfTokens.space1),
                      const Icon(
                        Icons.chevron_left,
                        size: 18,
                        color: SimfTokens.txtTertiary,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
