import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_app/features/venuemap/venue_map_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _boothNode = VenueMapNode(
  id: 'n1',
  label: 'Booth A-12',
  labelArabic: 'جناح A-12',
  kind: VenueMapNodeKind.booth,
  x: 10,
  y: 10,
  boothId: 'b1',
);

const _hallNode = VenueMapNode(
  id: 'n2',
  label: 'Hall A',
  labelArabic: 'القاعة أ',
  kind: VenueMapNodeKind.hall,
  x: 0,
  y: 0,
);

// A second node far from the booth so the booth normalises to the top-left of
// the canvas (clear of the bottom legend overlay), keeping its marker tappable.
const _farNode = VenueMapNode(
  id: 'n3',
  label: 'Far',
  labelArabic: 'بعيد',
  kind: VenueMapNodeKind.zone,
  x: 100,
  y: 100,
);

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

class _FakeVenueMapRepository implements VenueMapRepository {
  _FakeVenueMapRepository({
    this.nodes = const <VenueMapNode>[],
    this.booths = const <BoothSummary>[],
    this.detail,
    this.failList = false,
    this.failDetail = false,
  });

  final List<VenueMapNode> nodes;
  final List<BoothSummary> booths;
  final BoothDetail? detail;
  final bool failList;
  final bool failDetail;
  int nodeCalls = 0;

  @override
  Future<List<VenueMapNode>> getNodes() async {
    nodeCalls++;
    if (failList) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return nodes;
  }

  @override
  Future<List<BoothSummary>> getBooths() async {
    if (failList) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return booths;
  }

  @override
  Future<BoothDetail> getBoothDetail(String id) async {
    if (failDetail) {
      throw const ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: 404,
      );
    }
    return detail!;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required VenueMapRepository repo,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
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
        home: const VenueMapScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('VenueMapScreen (Page 015)', () {
    testWidgets('renders a marker per node + the legend', (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _hallNode],
          booths: const <BoothSummary>[_booth],
        ),
      );

      expect(find.text('Booth A-12'), findsOneWidget);
      expect(find.text('Hall A'), findsOneWidget);
      // Legend chips for the four kinds.
      expect(find.text('Booth'), findsOneWidget);
      expect(find.text('Point of interest'), findsOneWidget);
    });

    testWidgets('tapping a booth node opens the popup with name, code, detail',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _farNode],
          booths: const <BoothSummary>[_booth],
          detail: _detail,
        ),
      );

      await tester.tap(find.text('Booth A-12'));
      await tester.pumpAndSettle();

      expect(find.text('SAMI'), findsOneWidget); // booth name (sheet)
      expect(find.text('A-12'), findsOneWidget); // code chip
      expect(find.text('World-class maritime systems.'), findsOneWidget);
    });

    testWidgets('a detail 404 keeps the summary and drops the description',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _farNode],
          booths: const <BoothSummary>[_booth],
          failDetail: true,
        ),
      );

      await tester.tap(find.text('Booth A-12'));
      await tester.pumpAndSettle();

      expect(find.text('SAMI'), findsOneWidget); // summary kept
      expect(find.text('A-12'), findsOneWidget);
    });

    testWidgets('an empty node list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeVenueMapRepository());
      expect(find.text('No map items yet'), findsOneWidget);
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeVenueMapRepository(failList: true);
      await _pump(tester, repo: repo);

      expect(find.text('Could not load the map.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(repo.nodeCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('chrome mirrors in Arabic but the canvas geometry does not',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_hallNode],
          booths: const <BoothSummary>[],
        ),
        locale: const Locale('ar'),
      );

      // The legend (chrome) follows the locale → RTL.
      expect(
        Directionality.of(tester.element(find.text('قاعة'))),
        TextDirection.rtl,
      );
      // The map canvas (and its node labels) stays LTR — venue geometry is not
      // mirrored (Page_015 L-3).
      expect(
        Directionality.of(tester.element(find.text('القاعة أ'))),
        TextDirection.ltr,
      );
    });
  });
}
