import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/_legacy_mockup/badge_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

MyAreaDashboard _dashboard({String? qrId}) => MyAreaDashboard(
      identity: MyAreaIdentity(
        fullNameAr: 'رائد السالم',
        fullNameEn: 'Raed Al-Salem',
        qrId: qrId,
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 0, meetingsCount: 0),
      todaySchedule: const <MyAreaScheduleItem>[],
    );

class _FakeMyAreaRepository implements MyAreaRepository {
  _FakeMyAreaRepository({this.dashboard, this.fail = false});

  final MyAreaDashboard? dashboard;
  final bool fail;
  int dashboardCalls = 0;

  @override
  Future<MyAreaDashboard> getDashboard() async {
    dashboardCalls++;
    if (fail) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: 500,
      );
    }
    return dashboard!;
  }

  @override
  Future<String> getContactCardVcf() async => '';

  @override
  Future<String> getCalendarIcs() async => '';
}

Future<void> _pump(
  WidgetTester tester, {
  required MyAreaRepository repo,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        myAreaRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        home: const BadgeScreen(),
        locale: locale,
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
}

void main() {
  group('BadgeScreen (Page 032)', () {
    testWidgets('an issued qrId renders the QR badge + the name',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
      );

      expect(find.byType(QrImageView), findsOneWidget);
      expect(find.text('Raed Al-Salem'), findsOneWidget);
      expect(find.text('Show this at entry'), findsOneWidget);
    });

    testWidgets('a null qrId shows the pending state, no QR', (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      expect(find.byType(QrImageView), findsNothing);
      expect(
        find.text('Your badge is available once your account is approved.'),
        findsOneWidget,
      );
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeMyAreaRepository(fail: true);
      await _pump(tester, repo: repo);

      expect(find.text('Could not load your badge.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(repo.dashboardCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('renders the Arabic name + hint in Arabic', (tester) async {
      await _pump(
        tester,
        repo: _FakeMyAreaRepository(dashboard: _dashboard(qrId: 'ABC123')),
        locale: const Locale('ar'),
      );

      expect(find.byType(QrImageView), findsOneWidget);
      expect(find.text('رائد السالم'), findsOneWidget);
    });
  });
}
