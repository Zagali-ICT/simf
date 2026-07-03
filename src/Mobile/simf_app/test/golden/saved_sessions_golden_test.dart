@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart'
    show aiSummarySessionsProvider;
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/saved_sessions_screen.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the Saved-sessions screen (الجلسات المحفوظة). The
/// screen was built and owner-verified to Figma frame **1701:8928** (D-584);
/// that node has since been REMOVED from the Figma file (checked 2026-07-03,
/// D-599), so this golden locks the verified render rather than a live frame —
/// the unsuffixed filename marks it render-locked, not frame-locked.
///   flutter test --update-goldens test/golden/saved_sessions_golden_test.dart
///
/// Render expected: the gold "★ N جلسة محفوظة" count row, the الكل + category
/// chips (SessionFilterTabs), then borderless navyDeep cards — title over
/// "{category} · {hall}" and "{date} · {time}" beige meta lines with the
/// Material bookmark toggle (filled = saved) on the trailing edge. RTL.

SessionListItem _item(
  String id,
  String titleAr, {
  required int hourLocal,
  String? categoryAr,
}) =>
    SessionListItem(
      id: id,
      code: 'C-$id',
      title: titleAr,
      titleArabic: titleAr,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      // LOCAL instants: the card renders `startLocal` (= toLocal()), so a UTC
      // fixture would bake the generating machine's timezone into the PNG —
      // local in, local out renders identically in every timezone.
      startUtc: DateTime(2026, 11, 23, hourLocal),
      endUtc: DateTime(2026, 11, 23, hourLocal + 1),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
      categoryName: categoryAr,
      categoryNameArabic: categoryAr,
    );

final _programme = <SessionListItem>[
  _item('s1', 'مستقبل الأمن البحري', hourLocal: 9, categoryAr: 'جلسة حوارية'),
  _item('s2', 'التحول الرقمي في الدفاع', hourLocal: 11, categoryAr: 'ورشة عمل'),
  _item(
    's3',
    'الصناعات البحرية الوطنية',
    hourLocal: 13,
    categoryAr: 'جلسة حوارية',
  ),
  // Not favourited — must NOT render.
  _item('s4', 'جلسة غير محفوظة', hourLocal: 15),
];

/// Seeds the favourites set (s1..s3 saved). build() only — the golden takes no
/// taps, so toggle() never hits the client.
class _FakeFavourites extends SessionFavouritesController {
  _FakeFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Saved-sessions @375x812 — render-lock (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/saved-sessions',
      routes: <RouteBase>[
        GoRoute(
          path: '/saved-sessions',
          name: RouteNames.savedSessions,
          builder: (_, __) => const SavedSessionsScreen(),
        ),
        GoRoute(
          path: '/sessions/:sessionId',
          name: RouteNames.sessionDetail,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          aiSummarySessionsProvider.overrideWith((ref) async => _programme),
          sessionFavouritesProvider
              .overrideWith(() => _FakeFavourites(<String>{'s1', 's2', 's3'})),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(SavedSessionsScreen),
      matchesGoldenFile('goldens/saved_sessions.png'),
    );
  });
}
