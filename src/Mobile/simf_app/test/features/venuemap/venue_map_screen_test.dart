import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_app/features/venuemap/venue_map_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

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
// the canvas (clear of the bottom info-card overlay), keeping it tappable.
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
  });

  final List<VenueMapNode> nodes;
  final List<BoothSummary> booths;
  final BoothDetail? detail;
  final bool failList;
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
  Future<BoothDetail> getBoothDetail(String id) async => detail!;
}

Future<void> _pump(
  WidgetTester tester, {
  required VenueMapRepository repo,
  Locale locale = const Locale('en'),
  String? targetBoothId,
}) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        // FR-LGO-005 — the info card's logo badge builds its asset URL off the
        // data config, so the screen now reads it.
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
        home: VenueMapScreen(targetBoothId: targetBoothId),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('VenueMapScreen (Page 015 — KSA frame 215:562, venue plane)', () {
    testWidgets('renders a marker per node + the floating map controls',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _hallNode],
          booths: const <BoothSummary>[_booth],
        ),
      );

      expect(find.text('Booth A-12'), findsOneWidget);
      expect(find.text('Hall A'), findsOneWidget);
      expect(find.byIcon(Icons.add), findsOneWidget);
      expect(find.byIcon(Icons.remove), findsOneWidget);
      expect(find.byIcon(Icons.my_location), findsOneWidget);
      // No info card until a node is selected.
      expect(find.text('Guide me'), findsNothing);
    });

    testWidgets('tapping a booth node opens the info card with name, code, '
        'and the guide-me action', (tester) async {
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

      expect(find.text('SAMI'), findsWidgets); // name (card + gold box)
      expect(find.text('A-12'), findsOneWidget); // code chip
      expect(find.text('SAMI Co · Defense'), findsOneWidget);
      expect(find.text('Guide me'), findsOneWidget);
      // Figma 758:1358 has a single action — the "View details" button was
      // removed (owner 2026-07-08), so no booth node shows it.
      expect(find.text('View details'), findsNothing);
    });

    testWidgets('FR-LGO-005 — a booth card carries the exhibitor logo badge '
        'from the BoothLogo asset route', (tester) async {
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

      // The frame's 60x60 badge was dropped when booths had no logo assets;
      // they do now (BoothLogo, D-357/D-764), so the card renders it.
      final badge = tester.widget<Image>(find.byType(Image));
      // The badge decode-caps its thumbnail, so the provider is a ResizeImage
      // wrapping the real NetworkImage.
      final provider = badge.image;
      final network = provider is ResizeImage
          ? provider.imageProvider as NetworkImage
          : provider as NetworkImage;
      expect(
        network.url,
        'http://test.local/api/v1/app/assets/BoothLogo/b1/image',
      );
      final box = tester.getSize(
        find
            .ancestor(
              of: find.byType(Image),
              matching: find.byType(Container),
            )
            .first,
      );
      expect(box.width, 60);
      expect(box.height, 60);
    });

    testWidgets('FR-LGO-005 — a non-booth node has no logo badge',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_hallNode, _farNode],
        ),
      );

      await tester.tap(find.text('Hall A'));
      await tester.pumpAndSettle();

      // A hall / zone node has no exhibitor, so there is nothing to badge.
      expect(find.byType(Image), findsNothing);
    });

    testWidgets('a non-booth node shows the card with Guide-me only and '
        'closes via the X', (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_hallNode, _farNode],
        ),
      );

      await tester.tap(find.text('Hall A'));
      await tester.pumpAndSettle();

      expect(find.text('Guide me'), findsOneWidget);
      expect(find.text('View details'), findsNothing);

      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();
      expect(find.text('Guide me'), findsNothing);
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

    testWidgets('the canvas geometry stays LTR in Arabic', (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_hallNode],
        ),
        locale: const Locale('ar'),
      );

      // The map canvas (and its node labels) stays LTR — venue geometry is
      // not mirrored (Page_015 L-3).
      expect(
        Directionality.of(tester.element(find.text('القاعة أ'))),
        TextDirection.ltr,
      );
    });

    testWidgets('#9 — a pushed targetBoothId selects that booth on open',
        (tester) async {
      // The map is pushed with a booth id from the booth list's "أرشدني" CTA.
      // The selection is what opens the info card, and it must happen once the
      // nodes have loaded — the screen cannot know which node to centre on
      // before then.
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _farNode],
          booths: const <BoothSummary>[_booth],
        ),
        targetBoothId: 'b1',
      );

      // The info card for that booth is open without the user tapping. The
      // unselected case below finds NONE, so any match is the card.
      expect(find.text('SAMI'), findsWidgets);
    });

    testWidgets('an unknown targetBoothId selects nothing, and does not throw',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeVenueMapRepository(
          nodes: const <VenueMapNode>[_boothNode, _farNode],
          booths: const <BoothSummary>[_booth],
        ),
        targetBoothId: 'does-not-exist',
      );

      expect(find.text('SAMI'), findsNothing);
    });
  });
}
