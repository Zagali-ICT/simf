import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/booths/booths_screen.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The HTTP client fails network-image loads in tests, so the booth logo's
// errorBuilder falls back to its initials — no real bytes are needed.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _sami = BoothSummary(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  exhibitorName: 'Saudi Arabian Military Industries',
  sector: 'Defense',
);

// A booth whose exhibitor has a linked Contact (the CompanyLogo owner).
const _samiWithLogo = BoothSummary(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  exhibitorName: 'Saudi Arabian Military Industries',
  sector: 'Defense',
  exhibitorContactId: 'c1',
);

/// Every NetworkImage URL currently in the tree.
Set<String> _networkImageUrls(WidgetTester tester) => tester
    .widgetList<Image>(find.byType(Image))
    .map((img) => img.image)
    .whereType<NetworkImage>()
    .map((n) => n.url)
    .toSet();

const _other = BoothSummary(
  id: 'b2',
  code: 'B-03',
  name: 'Lockheed',
  nameArabic: 'لوكهيد',
  exhibitorName: 'Lockheed Martin',
  sector: 'Aerospace',
);

const _detail = BoothDetail(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  description: 'World-class maritime systems.',
);

class _FakeRepo implements VenueMapRepository {
  _FakeRepo({
    this.booths = const <BoothSummary>[],
    this.detail,
    this.fail = false,
  });

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

Future<void> _pump(
  WidgetTester tester, {
  required VenueMapRepository repo,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        venueMapRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: locale,
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
  group('BoothsScreen (Page 022, frame 922:2458)', () {
    testWidgets('renders the booth card header + code pill + hall box',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(booths: const <BoothSummary>[_sami]));
      // Company header: short name + full name.
      expect(find.text('SAMI'), findsOneWidget);
      expect(
        find.text('Saudi Arabian Military Industries'),
        findsOneWidget,
      );
      // The gold code pill.
      expect(find.text('A-12'), findsOneWidget);
      // The hall box falls back to the sector when no hall name ships.
      expect(find.text('Defense'), findsOneWidget);
    });

    testWidgets('search filters the list client-side', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(booths: const <BoothSummary>[_sami, _other]),
      );
      expect(find.text('SAMI'), findsOneWidget);
      expect(find.text('Lockheed'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'lockheed');
      await tester.pumpAndSettle();

      expect(find.text('SAMI'), findsNothing);
      expect(find.text('Lockheed'), findsOneWidget);
    });

    testWidgets('search with no match shows the no-match state',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(booths: const <BoothSummary>[_sami]));
      await tester.enterText(find.byType(TextField), 'zzz-no-such-booth');
      await tester.pumpAndSettle();
      expect(find.text('SAMI'), findsNothing);
    });

    testWidgets('tapping a booth opens the detail sheet', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(booths: const <BoothSummary>[_sami], detail: _detail),
      );
      await tester.tap(find.text('SAMI'));
      await tester.pumpAndSettle();
      expect(find.text('World-class maritime systems.'), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeRepo());
      expect(find.text('No booths'), findsOneWidget);
    });

    testWidgets('P6 — a booth with a linked exhibitor wires the CompanyLogo route',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(booths: const <BoothSummary>[_samiWithLogo]),
      );
      expect(
        _networkImageUrls(tester),
        contains('http://test.local/api/v1/app/assets/CompanyLogo/c1/image'),
      );
    });

    testWidgets('P6 — a booth with no linked exhibitor shows no logo image '
        '(initials fallback)', (tester) async {
      await _pump(tester, repo: _FakeRepo(booths: const <BoothSummary>[_sami]));
      // No exhibitorContactId → the logo tile renders initials, not a network image.
      expect(_networkImageUrls(tester), isEmpty);
    });

    testWidgets('PAR-B3 — RTL: the company logo tile leads at the inline start '
        '(right) of the company name', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(booths: const <BoothSummary>[_samiWithLogo]),
        locale: const Locale('ar'),
      );
      // The logo tile (a network Image) must sit to the inline start (physical
      // right) of the company name 'سامي', per frame 922:2789.
      final logoDx = tester.getCenter(find.byType(Image)).dx;
      final nameDx = tester.getCenter(find.text('سامي')).dx;
      expect(logoDx, greaterThan(nameDx));
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
