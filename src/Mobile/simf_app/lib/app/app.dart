import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/accessibility/data/accessibility_controller.dart';
import 'localization/app_l10n.dart';
import 'localization/locale_controller.dart';
import 'router.dart';
import 'theme/app_theme.dart';

/// The root widget. `MaterialApp.router` is wired to the go_router instance
/// from [routerProvider], and the current locale comes from
/// [localeControllerProvider].
class SimfApp extends ConsumerWidget {
  const SimfApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final locale = ref.watch(localeControllerProvider);
    final a11y = ref.watch(accessibilityControllerProvider);

    return MaterialApp.router(
      title: 'SIMF',
      debugShowCheckedModeBanner: false,
      routerConfig: router,
      // The app is navy-always (the Mockup.html brand surface): every screen is
      // built on the dark/navy tokens, so themeMode is pinned to dark and the OS
      // light/dark setting is ignored — on a light-mode device the on-navy text
      // would otherwise sit on a light scaffold and be unreadable. High-contrast
      // swaps the dark slot to the high-contrast-dark theme (white-on-black). The
      // light slots stay wired for a possible future light mode but are not
      // selected while themeMode is pinned to dark.
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
          child: child ?? const SizedBox.shrink(),
        );
      },
    );
  }
}
