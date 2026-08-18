import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/core/session/session_guard.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

class SimfApp extends ConsumerWidget {
  const SimfApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final locale = ref.watch(localeControllerProvider);
    final a11y = ref.watch(accessibilityControllerProvider);

    // D-443 — when a signed-in session ENDS (refresh failed at the 24h cap /
    // revoked / signed out), land on sign-in. The router's route-gate already
    // bounces protected screens, but a session can also die while the user is
    // on a public screen (e.g. Home with its sponsors strip) — there the gate
    // does not fire and the user would otherwise sit on stale content after a
    // 401. Guests never transition from SignedIn, so they are unaffected.
    ref.listen<AuthState>(authControllerProvider, (previous, next) {
      if (previous is AuthStateSignedIn && next is AuthStateSignedOut) {
        router.go('/sign-in');
      }
    });

    return MaterialApp.router(
      // onGenerateTitle, not `title`: the OS task-switcher label is user-facing
      // copy and the app has an Arabic name. `title` is evaluated outside any
      // Localizations scope, so it cannot read AppL10n; this callback receives
      // a context that can.
      onGenerateTitle: (context) => AppL10n.of(context).appName,
      debugShowCheckedModeBanner: false,
      routerConfig: router,
      // The app is navy-always (the Mockup.html brand surface): every screen is
      // built on the dark/navy tokens, so themeMode is pinned to dark and the
      // OS light/dark setting is ignored — on a light-mode device the on-navy
      // text would otherwise sit on a light scaffold and be unreadable.
      // High-contrast swaps the dark slot to the high-contrast-dark theme
      // (white-on-black). The light slots stay wired for a possible future
      // light mode but are not selected while themeMode is pinned to dark.
      theme:
          a11y.highContrast ? SimfTheme.highContrastLight() : SimfTheme.light(),
      darkTheme:
          a11y.highContrast ? SimfTheme.highContrastDark() : SimfTheme.dark(),
      themeMode: ThemeMode.dark,
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      // Apply the accessibility text-scale + reduce-motion choices app-wide by
      // overriding the MediaQuery the whole app sees (Page 038).
      builder: (context, child) {
        final mq = MediaQuery.of(context);
        return MediaQuery(
          data: mq.copyWith(
            textScaler: TextScaler.linear(a11y.textSize.scaleFactor),
            disableAnimations: a11y.reduceMotion || mq.disableAnimations,
          ),
          // D-726 (item 11) — the app-wide session auto-extend guard wraps
          // every screen: it marks activity on each touch, silently refreshes
          // the access token while active, and paints the idle timeout
          // countdown.
          child: SessionGuard(child: child ?? const SizedBox.shrink()),
        );
      },
    );
  }
}
