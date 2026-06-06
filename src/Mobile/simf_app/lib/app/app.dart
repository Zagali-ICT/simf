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
      // High-contrast is orthogonal to brightness: it swaps both the light and
      // dark slots and leaves themeMode to the OS. A future theme-mode control
      // should set themeMode and extend this ternary, not add a parallel path.
      theme:
          a11y.highContrast ? SimfTheme.highContrastLight() : SimfTheme.light(),
      darkTheme:
          a11y.highContrast ? SimfTheme.highContrastDark() : SimfTheme.dark(),
      themeMode: ThemeMode.system,
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
