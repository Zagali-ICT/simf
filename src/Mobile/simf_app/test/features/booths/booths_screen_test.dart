import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/booths/booths_screen.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _booth = BoothSummary(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  exhibitorName: 'SAMI Co',
  sector: 'Defense',
);

const _detail = BoothDetail(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  description: 'World-class maritime systems.',
);

class _FakeRepo implements VenueMapRepository {
  _FakeRepo({this.booths = const <BoothSummary>[], this.detail, this.fail = false});

  final List<BoothSummary> booths;
  final BoothDetail? detail;
  final bool fail;
  int calls = 0;

  @override
  Future<List<BoothSummary>> getBooths() async {
    calls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return booths;
  }

  @override
  Future<BoothDetail> getBoothDetail(String id) async => detail!;

  @override
  Future<List<VenueMapNode>> getNodes() => throw UnimplementedError();
}

Future<void> _pump(WidgetTester tester, {required VenueMapRepository repo}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[venueMapRepositoryProvider.overrideWithValue(repo)],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const BoothsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BoothsScreen (Page 022)', () {
    testWidgets('lists the booths', (tester) async {
      await _pump(tester, repo: _FakeRepo(booths: const <BoothSummary>[_booth]));
      expect(find.text('SAMI'), findsOneWidget);
      expect(find.text('A-12'), findsOneWidget);
      expect(find.textContaining('SAMI Co'), findsOneWidget);
    });

    testWidgets('tapping a booth opens the detail sheet', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(booths: const <BoothSummary>[_booth], detail: _detail),
      );
      await tester.tap(find.text('SAMI'));
      await tester.pumpAndSettle();
      expect(find.text('World-class maritime systems.'), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeRepo());
      expect(find.text('No booths'), findsOneWidget);
    });

    testWidgets('error shows retry, which re-fetches', (tester) async {
      final repo = _FakeRepo(fail: true);
      await _pump(tester, repo: repo);
      expect(find.text('Could not load the booths.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
