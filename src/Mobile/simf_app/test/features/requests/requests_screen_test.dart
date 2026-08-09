import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_app/features/requests/requests_screen.dart';

AppRequestItem _item({
  required AppRequestKind kind,
  required String id,
  required String title,
  AppRequestStatus status = AppRequestStatus.pending,
  bool canCancel = false,
}) =>
    AppRequestItem(
      kind: kind,
      id: id,
      title: title,
      titleArabic: title,
      status: status,
      createdAt: DateTime.utc(2026, 1, 10),
      canCancel: canCancel,
    );

Future<void> _pump(
  WidgetTester tester, {
  List<AppRequestItem>? data,
  bool fail = false,
  bool isVip = true,
}) async {
  final router = GoRouter(
    initialLocation: '/requests',
    routes: <RouteBase>[
      GoRoute(
        path: '/requests',
        name: RouteNames.requests,
        builder: (_, __) => const RequestsScreen(),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        myRequestsProvider.overrideWith((ref) async {
          if (fail) {
            throw Exception('boom');
          }
          return data ?? const <AppRequestItem>[];
        }),
        currentUserMeetingAccessProvider.overrideWith(
          (ref) => MeetingAccess(speaker: isVip, delegation: isVip),
        ),
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
  group('RequestsScreen (Figma 1408:9726)', () {
    testWidgets('renders a request card with its type headline and subtitle',
        (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.participationDocument,
            id: '1',
            title: 'Official attendance certificate',
          ),
        ],
      );
      expect(find.text('Participation document request'), findsOneWidget);
      expect(find.text('Official attendance certificate'), findsOneWidget);
      expect(find.text('New request'), findsOneWidget);
    });

    testWidgets('shows the empty state when there are no requests',
        (tester) async {
      await _pump(tester);
      expect(find.text('You have no requests yet'), findsOneWidget);
    });

    testWidgets('the status chips filter the cards', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.participationDocument,
            id: '1',
            title: 'Certificate',
          ),
          _item(
            kind: AppRequestKind.badgeUpdate,
            id: '2',
            title: 'Commander',
            status: AppRequestStatus.accepted,
          ),
        ],
      );

      expect(find.text('Participation document request'), findsOneWidget);
      expect(find.text('Badge update request'), findsOneWidget);

      await tester.tap(find.text('Accepted (1)'));
      await tester.pumpAndSettle();

      expect(find.text('Badge update request'), findsOneWidget);
      expect(find.text('Participation document request'), findsNothing);
    });

    testWidgets('السجل resets the status-chip filter to all', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.participationDocument,
            id: '1',
            title: 'Certificate',
          ),
          _item(
            kind: AppRequestKind.badgeUpdate,
            id: '2',
            title: 'Commander',
            status: AppRequestStatus.accepted,
          ),
        ],
      );

      // A status chip filters to accepted only...
      await tester.tap(find.text('Accepted (1)'));
      await tester.pumpAndSettle();
      expect(find.text('Badge update request'), findsOneWidget);
      expect(find.text('Participation document request'), findsNothing);

      // ...then السجل (Log) — the gold "all" button — resets to every card.
      await tester.tap(find.text('Log'));
      await tester.pumpAndSettle();
      expect(find.text('Participation document request'), findsOneWidget);
      expect(find.text('Badge update request'), findsOneWidget);
    });

    testWidgets('the card date carries the year (Figma 1408:9782)',
        (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.badgeUpdate,
            id: '1',
            title: 'Commander',
          ),
        ],
      );
      expect(find.text('10 Jan 2026'), findsOneWidget);
    });

    testWidgets('shows the error state on a wire failure', (tester) async {
      await _pump(tester, fail: true);
      expect(find.text('Could not load your requests'), findsOneWidget);
    });

    testWidgets('D-729: hides "New request" for a non-VIP viewer',
        (tester) async {
      await _pump(tester, isVip: false);
      // The VIP-only new-meeting-request button is gone; the log/filter stays.
      expect(find.text('New request'), findsNothing);
      expect(find.text('Log'), findsOneWidget);
    });
  });
}
