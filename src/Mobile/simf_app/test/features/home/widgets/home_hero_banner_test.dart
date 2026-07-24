import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';
import 'package:simf_app/features/home/widgets/home_hero_banner.dart';

OrgProfile _edition() => OrgProfile(
      name: 'Saudi International Maritime Forum',
      nameArabic: 'الملتقى البحري السعودي الدولي الرابع',
      title: 'The future of seabed security',
      titleArabic: 'مستقبل أمن قاع البحار وسلاسل الإمداد في بيئة عالمية متغيرة',
      currentYear: 2026,
      status: 'Open',
      locationText: 'Riyadh, Saudi Arabia',
      locationTextArabic: 'الرياض – المملكة العربية السعودية',
      eventStartDate: DateTime(2026, 11, 23),
      eventEndDate: DateTime(2026, 11, 25),
      social: const OrgSocial(),
      aboutItems: const <OrgAboutItem>[],
      details: const <OrgDetail>[],
    );

Widget _harness({
  required OrgProfile? profile,
  required List<PublicBannerItem> banners,
  Locale locale = const Locale('ar'),
}) =>
    MaterialApp(
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => HomeHeroBanner(
            l10n: AppL10n.of(context),
            profile: profile,
            banners: banners,
            baseUrl: 'http://test.local/api/v1',
            onTap: () {},
          ),
        ),
      ),
    );

Future<void> _pumpHero(
  WidgetTester tester, {
  required OrgProfile? profile,
  required List<PublicBannerItem> banners,
  Locale locale = const Locale('ar'),
}) async {
  await tester.pumpWidget(
    _harness(profile: profile, banners: banners, locale: locale),
  );
  await tester.pump(const Duration(milliseconds: 100));
}

void main() {
  group('HomeHeroBanner (#43)', () {
    testWidgets('edition overlay shows name + theme + dates + location (ar)',
        (tester) async {
      await _pumpHero(
        tester,
        profile: _edition(),
        banners: const <PublicBannerItem>[],
      );
      expect(
        find.textContaining('الملتقى البحري السعودي الدولي الرابع'),
        findsOneWidget,
      );
      expect(find.textContaining('مستقبل أمن قاع البحار'), findsOneWidget);
      expect(find.text('23-25 نوفمبر 2026'), findsOneWidget);
      expect(find.textContaining('الرياض'), findsOneWidget);
    });

    testWidgets('video unavailable → static discover fallback image',
        (tester) async {
      await _pumpHero(
        tester,
        profile: null,
        banners: const <PublicBannerItem>[],
      );
      // No PageView, no carousel — the hero is a static image now.
      expect(find.byType(PageView), findsNothing);
    });

    testWidgets('English overlay uses the English edition fields',
        (tester) async {
      await _pumpHero(
        tester,
        profile: _edition(),
        banners: const <PublicBannerItem>[],
        locale: const Locale('en'),
      );
      expect(
        find.textContaining('Saudi International Maritime Forum'),
        findsOneWidget,
      );
      expect(find.text('23-25 November 2026'), findsOneWidget);
      expect(find.textContaining('Riyadh'), findsOneWidget);
    });
  });
}
