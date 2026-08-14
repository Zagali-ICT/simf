import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/exhibition/widgets/entity_detail_scaffold.dart';

/// Pumps [EntityDetailScaffold] in an RTL host (Figma 1439:11881 "العارض" /
/// 1439:11826 "الراعي"). A tall surface so the lazy ListView lays out the whole
/// card stack.
Future<void> _pump(
  WidgetTester tester, {
  required Widget child,
}) async {
  tester.view.physicalSize = const Size(375, 2200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/detail',
    routes: <RouteBase>[
      GoRoute(path: '/detail', builder: (_, __) => child),
      for (final (name, path) in <(String, String)>[
        (RouteNames.home, '/'),
        (RouteNames.venueMap, '/map'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (_, __) => const Scaffold(body: SizedBox()),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      child: MaterialApp.router(
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
}

EntityDetailScaffold _exhibitor({
  String? standCode = 'A2-E5.3',
  String? about = 'نبذة تجريبية عن العارض ومجال عمله.',
  String? website = 'www.aramco.com',
  String? tierPill = 'عارض بريميوم',
  String? locationLine = 'الظهران، المملكة العربية السعودية',
}) =>
    EntityDetailScaffold(
      onRefresh: () async {},
      headerTitle: 'العارض',
      aboutHeader: 'نبذة عن العارض',
      websiteLabel: 'الموقع الإلكتروني',
      logo: const SizedBox(),
      name: 'أرامكو السعودية',
      locationLine: locationLine,
      countryId: 682, // ISO 3166-1 numeric — Saudi Arabia
      tierPill: tierPill,
      standLabel: 'موقع الجناح على الخريطة',
      standCode: standCode,
      about: about,
      website: website,
    );

void main() {
  group('EntityDetailScaffold (Figma 1439:11881 / 1439:11826)', () {
    testWidgets('renders the full card stack for an exhibitor', (tester) async {
      await _pump(tester, child: _exhibitor());

      expect(find.text('العارض'), findsOneWidget); // header
      expect(find.text('أرامكو السعودية'), findsOneWidget); // name
      expect(
        find.text('الظهران، المملكة العربية السعودية'),
        findsOneWidget,
      ); // location line
      expect(find.text('عارض بريميوم'), findsOneWidget); // tier pill
      expect(find.text('A2-E5.3'), findsOneWidget); // stand code
      expect(find.text('موقع الجناح على الخريطة'), findsOneWidget); // map label
      expect(find.text('نبذة عن العارض'), findsOneWidget); // about header
      expect(
        find.text('نبذة تجريبية عن العارض ومجال عمله.'),
        findsOneWidget,
      ); // about paragraph
      expect(find.text('الموقع الإلكتروني'), findsOneWidget); // website label
      expect(find.text('www.aramco.com'), findsOneWidget); // website value
    });

    testWidgets('hides the stand→map row when there is no stand code',
        (tester) async {
      await _pump(tester, child: _exhibitor(standCode: null));

      expect(find.text('أرامكو السعودية'), findsOneWidget);
      // The map-row label and the stand code are both gone.
      expect(find.text('موقع الجناح على الخريطة'), findsNothing);
      expect(find.text('A2-E5.3'), findsNothing);
    });

    testWidgets('hides the about card when there is no about text',
        (tester) async {
      await _pump(tester, child: _exhibitor(about: '   '));

      expect(find.text('نبذة عن العارض'), findsNothing);
    });

    testWidgets('hides the website row when there is no website',
        (tester) async {
      await _pump(tester, child: _exhibitor(website: null));

      expect(find.text('الموقع الإلكتروني'), findsNothing);
      expect(find.text('www.aramco.com'), findsNothing);
    });

    testWidgets('the stand code + website align to the leading edge (start)',
        (tester) async {
      await _pump(tester, child: _exhibitor());

      // The value tracks the locale via TextAlign.start (right in the Arabic
      // design target, left in English) rather than a hardcoded .right.
      final code = tester.widget<Text>(find.text('A2-E5.3'));
      final url = tester.widget<Text>(find.text('www.aramco.com'));
      expect(code.textAlign, TextAlign.start);
      expect(url.textAlign, TextAlign.start);
    });

    testWidgets('a null stand label still shows the code without a blank line',
        (tester) async {
      // standLabel omitted but standCode present → the scaffold passes
      // standLabel ?? '' which the row must guard (no empty label line).
      await _pump(
        tester,
        child: EntityDetailScaffold(
          onRefresh: () async {},
          headerTitle: 'العارض',
          aboutHeader: 'نبذة عن العارض',
          websiteLabel: 'الموقع الإلكتروني',
          logo: const SizedBox(),
          name: 'أرامكو السعودية',
          standCode: 'A2-E5.3',
        ),
      );

      expect(find.text('A2-E5.3'), findsOneWidget);
      // No empty label Text was injected next to the code.
      expect(find.text(''), findsNothing);
    });
  });
}
