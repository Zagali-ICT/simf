import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/sponsor_detail_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _sponsor = SponsorDetail(
  id: 'sp-1',
  nameEn: 'Northern Shipyards',
  nameAr: 'أحواض الشمال',
  tier: 1,
  tierName: 'Platinum',
  about: 'A builder of naval platforms.',
  aboutArabic: 'شركة لبناء المنصات البحرية.',
  city: 'Dammam',
  cityArabic: 'الدمام',
  countryId: 682,
  countryNameEn: 'Saudi Arabia',
  countryNameAr: 'السعودية',
  url: 'https://example.test',
);

Future<void> _pump(
  WidgetTester tester, {
  SponsorDetail? detail,
  bool fail = false,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_config),
        sponsorDetailProvider.overrideWith((ref, id) async {
          if (fail) {
            throw const ApiFailure(
              code: ApiErrorCodes.clientNetwork,
              message: 'x',
              httpStatus: 500,
            );
          }
          return detail ?? _sponsor;
        }),
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
        home: const SponsorDetailScreen(sponsorId: 'sp-1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SponsorDetailScreen (Figma 1439:11826)', () {
    testWidgets('renders the identity card, about and website rows',
        (tester) async {
      await _pump(tester);

      expect(find.text('Sponsor'), findsOneWidget); // header
      expect(find.text('Northern Shipyards'), findsOneWidget);
      expect(find.text('About the sponsor'), findsOneWidget);
      expect(find.text('A builder of naval platforms.'), findsOneWidget);
      expect(find.text('Website'), findsOneWidget);
      expect(find.text('https://example.test'), findsOneWidget);
    });

    testWidgets('shows the localized tier pill', (tester) async {
      await _pump(tester);

      expect(find.textContaining('Platinum'), findsOneWidget);
    });

    testWidgets('Arabic renders the Arabic name, about and city',
        (tester) async {
      await _pump(tester, locale: const Locale('ar'));

      expect(find.text('أحواض الشمال'), findsOneWidget);
      expect(find.text('شركة لبناء المنصات البحرية.'), findsOneWidget);
      expect(find.text('نبذة عن الراعي'), findsOneWidget);
    });

    testWidgets('a read failure shows the error state with retry',
        (tester) async {
      await _pump(tester, fail: true);

      expect(find.text('Could not load the details.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('an about-less sponsor hides the about card', (tester) async {
      await _pump(
        tester,
        detail: const SponsorDetail(
          id: 'sp-2',
          nameEn: 'Quiet Co',
          nameAr: 'شركة هادئة',
          tier: 2,
          tierName: 'Gold',
        ),
      );

      expect(find.text('Quiet Co'), findsOneWidget);
      // The about header only renders when there is a body to show.
      expect(find.text('About the sponsor'), findsNothing);
      // No website set either, so that row stays hidden too.
      expect(find.text('Website'), findsNothing);
    });
  });
}
