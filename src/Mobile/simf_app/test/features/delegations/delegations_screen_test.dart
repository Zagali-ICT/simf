import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/delegations_screen.dart';
import 'package:simf_app/features/delegations/widgets/delegations_stats_strip.dart';

DelegationItem _item({
  required int id,
  required String code,
  required String name,
  required String nameAr,
  int members = 1,
  String? head,
  String? headAr,
  String? title,
  DateTime? arrival,
  DateTime? departure,
}) =>
    DelegationItem(
      countryId: id,
      countryCode: code,
      countryName: name,
      countryNameArabic: nameAr,
      memberCount: members,
      headName: head,
      headNameArabic: headAr,
      headTitle: title,
      arrivalDate: arrival,
      departureDate: departure,
    );

Future<void> _pump(
  WidgetTester tester, {
  Delegations? data,
  bool fail = false,
}) async {
  final router = GoRouter(
    initialLocation: '/delegations',
    routes: <RouteBase>[
      GoRoute(
        path: '/delegations',
        name: RouteNames.delegations,
        builder: (_, __) => const DelegationsScreen(),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        // Bi-Meeting rework — the public screen reads the meeting-access flags
        // to decide card tappability; a guest (none) keeps the plain info
        // cards.
        currentUserMeetingAccessProvider
            .overrideWith((ref) => MeetingAccess.none),
        delegationsProvider.overrideWith((ref) async {
          if (fail) {
            throw Exception('boom');
          }
          return data ??
              const Delegations(
                countryCount: 0,
                totalParticipants: 0,
                items: <DelegationItem>[],
              );
        }),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
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
  group('DelegationsScreen (Figma 1426:10771)', () {
    testWidgets('renders a delegation card with head, member count and stats',
        (tester) async {
      await _pump(
        tester,
        data: Delegations(
          countryCount: 1,
          totalParticipants: 3,
          items: <DelegationItem>[
            _item(
              id: 840,
              code: 'US',
              name: 'United States',
              nameAr: 'الولايات المتحدة',
              members: 3,
              head: 'James Mitchell',
              headAr: 'جيمس ميتشل',
              title: 'Ambassador',
              arrival: DateTime(2026, 1, 12),
              departure: DateTime(2026, 1, 15),
            ),
          ],
        ),
      );

      expect(find.text('United States'), findsOneWidget);
      expect(find.text('Participating countries'), findsOneWidget);
      // The head-of-delegation box, the member/date bottom row, and the
      // total-participants stat are intentionally hidden in the current layout
      // (owner 2026-07-24) — only the country identity row + countries stat
      // show.
      expect(find.text('James Mitchell'), findsNothing);
      expect(find.text('Ambassador'), findsNothing);
      expect(find.text('3 members'), findsNothing);
      expect(find.text('Total participants'), findsNothing);
    });

    testWidgets('shows the empty state when there are no delegations',
        (tester) async {
      await _pump(tester);
      expect(find.text('No delegations yet.'), findsOneWidget);
    });

    testWidgets('the search box filters the cards', (tester) async {
      await _pump(
        tester,
        data: Delegations(
          countryCount: 2,
          totalParticipants: 2,
          items: <DelegationItem>[
            _item(
              id: 840,
              code: 'US',
              name: 'United States',
              nameAr: 'الولايات المتحدة',
            ),
            _item(
              id: 682,
              code: 'SA',
              name: 'Saudi Arabia',
              nameAr: 'السعودية',
            ),
          ],
        ),
      );

      expect(find.text('United States'), findsOneWidget);
      expect(find.text('Saudi Arabia'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'Saudi');
      await tester.pumpAndSettle();

      expect(find.text('Saudi Arabia'), findsOneWidget);
      expect(find.text('United States'), findsNothing);
    });

    testWidgets('tapping a stats-strip flag filters to that country',
        (tester) async {
      final us = _item(
        id: 840,
        code: 'US',
        name: 'United States',
        nameAr: 'الولايات المتحدة',
      );
      final sa = _item(
        id: 682,
        code: 'SA',
        name: 'Saudi Arabia',
        nameAr: 'السعودية',
      );
      await _pump(
        tester,
        data: Delegations(
          countryCount: 2,
          totalParticipants: 2,
          items: <DelegationItem>[us, sa],
        ),
      );

      // Both countries listed; no active-filter chip yet.
      expect(find.text('United States'), findsOneWidget);
      expect(find.text('Saudi Arabia'), findsOneWidget);
      expect(find.byIcon(Icons.close), findsNothing);

      // Tap the US flag in the stats strip → the list narrows to the US card.
      await tester.tap(
        find.descendant(
          of: find.byType(DelegationsStatsStrip),
          matching: find.text(us.flagEmoji),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Saudi Arabia'), findsNothing);
      // The country name now appears twice: the card + the active-filter chip.
      expect(find.text('United States'), findsNWidgets(2));
      expect(find.byIcon(Icons.close), findsOneWidget);

      // Clearing the chip restores every country.
      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();

      expect(find.text('United States'), findsOneWidget);
      expect(find.text('Saudi Arabia'), findsOneWidget);
      expect(find.byIcon(Icons.close), findsNothing);
    });

    testWidgets('shows the error state on a wire failure', (tester) async {
      await _pump(tester, fail: true);
      expect(find.text('Could not load delegations.'), findsOneWidget);
    });
  });
}
