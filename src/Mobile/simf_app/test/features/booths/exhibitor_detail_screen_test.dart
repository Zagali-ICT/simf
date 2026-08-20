import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/booths/exhibitor_detail_screen.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

// The HTTP client never resolves network-image loads in tests, so the logo tile
// stays in its (stable) loading state — the NetworkImage stays in the tree with
// its URL, which is exactly what these tests assert.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

// An exhibitor with its OWN ExhibitorLogo (exhibitorId) and a legacy linked
// Contact CompanyLogo (exhibitorContactId).
const _withOwnLogo = BoothDetail(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  exhibitorId: 'ex1',
  exhibitorContactId: 'c1',
);

// An exhibitor with no own logo yet — only the legacy Contact CompanyLogo.
const _legacyOnly = BoothDetail(
  id: 'b1',
  code: 'A-12',
  name: 'SAMI',
  nameArabic: 'سامي',
  exhibitorContactId: 'c1',
);

class _FakeRepo implements VenueMapRepository {
  _FakeRepo(this.detail);

  final BoothDetail detail;

  @override
  Future<List<BoothSummary>> getBooths() => throw UnimplementedError();

  @override
  Future<BoothDetail> getBoothDetail(String id) async => detail;

  @override
  Future<List<VenueMapNode>> getNodes() => throw UnimplementedError();
}

Set<String> _networkImageUrls(WidgetTester tester) => tester
    .widgetList<Image>(find.byType(Image))
    .map((img) => img.image)
    .whereType<NetworkImage>()
    .map((n) => n.url)
    .toSet();

Future<void> _pump(WidgetTester tester, BoothDetail detail) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        venueMapRepositoryProvider.overrideWithValue(_FakeRepo(detail)),
      ],
      child: const MaterialApp(
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: ExhibitorDetailScreen(boothId: 'b1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ExhibitorDetailScreen logo', () {
    testWidgets(
        'wires the exhibitor OWN ExhibitorLogo route as the primary logo',
        (tester) async {
      await _pump(tester, _withOwnLogo);
      // The exhibitor's own logo (owner = the exhibitor) is the primary source.
      // (In tests the fake 400 fires the error path, so the CompanyLogo
      // fallback also mounts nested — production only mounts it if the primary
      // truly 404s.)
      expect(
        _networkImageUrls(tester),
        contains('http://test.local/api/v1/app/assets/ExhibitorLogo/ex1/image'),
      );
    });

    testWidgets('shows initials when the exhibitor has no own logo id',
        (tester) async {
      await _pump(tester, _legacyOnly);
      final urls = _networkImageUrls(tester);
      // There is no logo to request. This case used to fall back to the Contact
      // CompanyLogo route, which D-929 removed server-side - its own comment
      // reads "its Contact owner table was removed, so the category could never
      // resolve" - so the fallback had been 404ing silently ever since. The
      // asset endpoint answers an unknown category with the same 404 it uses
      // for "nothing uploaded", which is why nothing reported it. The tile now
      // goes straight to initials instead of paying for a request that cannot
      // succeed.
      expect(urls.any((u) => u.contains('/CompanyLogo/')), isFalse);
      expect(urls.any((u) => u.contains('/ExhibitorLogo/')), isFalse);
    });
  });
}
