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
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/my_sessions_repository.dart';
import 'package:simf_app/features/myarea/my_sessions_screen.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the My-Sessions screen against Figma frame **1388:9067**
/// (عروض الجلسات, Approved account). Compare to the frame: flutter test
/// --update-goldens test/golden/my_sessions_golden_test.dart
///
/// Frame parity expected: the four equal-width القادمة/حضرتها/فاتتني/الأرشيف
/// filter tabs (the first selected), the "{n} {tab}" count header, then
/// borderless navyDeep cards — each a title over a clock·time·category line,
/// the speaker·rank + hall meta row, and the gold المفضلة heart on the trailing
/// edge (solid+filled when favourited, gold-50%+outline otherwise). RTL
/// throughout.
///
/// The default tab is القادمة (upcoming = start after now), so the items use a
/// far-future start — permanently "upcoming" regardless of the run date — while
/// the card itself only renders the time-of-day (never the date), so the future
/// year never shows. The favourites are seeded through the AsyncNotifier so two
/// of the three hearts read as filled.

MyAreaSessionItem _item({
  required String id,
  required String titleAr,
  required String titleEn,
  required int hourUtc,
  required String hallAr,
  String? categoryAr,
  String? speakerAr,
  String? speakerTitle,
}) =>
    MyAreaSessionItem(
      id: id,
      title: titleEn,
      titleArabic: titleAr,
      // Far future → always "upcoming"; only the time-of-day is rendered.
      start: DateTime.utc(2099, 6, 20, hourUtc),
      end: DateTime.utc(2099, 6, 20, hourUtc + 1, 30),
      status: SessionStatus.scheduled,
      attended: false,
      isFavourite: false,
      hallNameAr: hallAr,
      categoryNameAr: categoryAr,
      speakerNameAr: speakerAr,
      speakerTitle: speakerTitle,
    );

final _items = <MyAreaSessionItem>[
  _item(
    id: 'ms1',
    titleAr: 'مستقبل الأمن البحري',
    titleEn: 'The Future of Maritime Security',
    hourUtc: 7,
    hallAr: 'القاعة الرئيسية',
    categoryAr: 'جلسة حوارية',
    speakerAr: 'د. علي الحربي',
    speakerTitle: 'أستاذ الدراسات البحرية',
  ),
  _item(
    id: 'ms2',
    titleAr: 'التحول الرقمي في الدفاع',
    titleEn: 'Digital Transformation in Defence',
    hourUtc: 9,
    hallAr: 'قاعة الشرق',
    categoryAr: 'ورشة عمل',
    speakerAr: 'أ. فاطمة السهيل',
    speakerTitle: 'خبيرة التقنيات الدفاعية',
  ),
  _item(
    id: 'ms3',
    titleAr: 'الصناعات العسكرية المستقبلية',
    titleEn: 'Future Military Industries',
    hourUtc: 11,
    hallAr: 'قاعة الغرب',
    categoryAr: 'محاضرة',
    speakerAr: 'م. عبدالرحمن العتيبي',
    speakerTitle: 'مهندس أنظمة',
  ),
];

/// Seeds the favourites set so ms1 + ms3 render as filled hearts. Overrides the
/// AsyncNotifier's build() only — toggle() (which would hit the client) is
/// never invoked because the golden takes no taps.
class _FakeFavourites extends SessionFavouritesController {
  _FakeFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My-Sessions @375x1000 — Figma 1388:9067 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/my-area/sessions',
      routes: <RouteBase>[
        GoRoute(
          path: '/my-area/sessions',
          name: RouteNames.myAreaSessions,
          builder: (_, __) => const MySessionsScreen(),
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
          simfDataConfigProvider.overrideWithValue(_config),
          mySessionsProvider
              .overrideWith((ref) async => MyAreaSessions(_items)),
          sessionFavouritesProvider
              .overrideWith(() => _FakeFavourites(<String>{'ms1', 'ms3'})),
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
      find.byType(MySessionsScreen),
      matchesGoldenFile('goldens/my_sessions_1388-9067.png'),
    );
  });
}
