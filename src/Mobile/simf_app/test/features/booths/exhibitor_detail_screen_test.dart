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

    testWidgets('falls back to the legacy CompanyLogo when no own logo id',
        (tester) async {
      await _pump(tester, _legacyOnly);
      final urls = _networkImageUrls(tester);
      // No exhibitorId → the tile uses the legacy Contact CompanyLogo route so
      // an exhibitor that has not re-uploaded its own logo still shows its
      // logo, and no ExhibitorLogo route is wired at all.
      expect(
        urls,
        contains('http://test.local/api/v1/app/assets/CompanyLogo/c1/image'),
      );
      expect(
        urls.any((u) => u.contains('/ExhibitorLogo/')),
        isFalse,
      );
    });
  });
}
