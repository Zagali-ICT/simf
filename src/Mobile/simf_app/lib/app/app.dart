import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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

    return MaterialApp.router(
      title: 'SIMF',
      debugShowCheckedModeBanner: false,
      routerConfig: router,
      theme: SimfTheme.light(),
      darkTheme: SimfTheme.dark(),
      themeMode: ThemeMode.system,
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
    );
  }
}
