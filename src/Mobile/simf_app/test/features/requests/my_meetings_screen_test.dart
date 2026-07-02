import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_app/features/requests/my_meetings_screen.dart';

AppRequestItem _item({
  required AppRequestKind kind,
  required String id,
  required String title,
  AppRequestStatus status = AppRequestStatus.pending,
  DateTime? eventDateUtc,
  String? subtitle,
}) =>
    AppRequestItem(
      kind: kind,
      id: id,
      title: title,
      titleArabic: title,
      status: status,
      eventDateUtc: eventDateUtc,
      createdAt: DateTime.utc(2026, 1, 12, 12),
      canCancel: false,
      subtitle: subtitle,
    );

Future<void> _pump(
  WidgetTester tester, {
  List<AppRequestItem>? data,
  bool fail = false,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/my-meetings',
    routes: <RouteBase>[
      GoRoute(
        path: '/my-meetings',
        name: RouteNames.myMeetings,
        builder: (_, __) => const MyMeetingsScreen(),
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
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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
  group('MyMeetingsScreen (المقابلات, Figma 1701:9406)', () {
    testWidgets('shows speaker + delegation meetings, excludes other kinds and '
        'cancelled', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Dr Ibrahim',
            status: AppRequestStatus.accepted,
          ),
          _item(
            kind: AppRequestKind.delegationMeeting,
            id: '2',
            title: 'Kingdom of X',
            status: AppRequestStatus.pending,
          ),
          // Not a meeting → excluded.
          _item(
            kind: AppRequestKind.sessionAttendance,
            id: '3',
            title: 'Opening · Hall A',
            status: AppRequestStatus.accepted,
          ),
          // A cancelled meeting → excluded (it lives on الطلبات).
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '4',
            title: 'Withdrawn Guy',
            status: AppRequestStatus.cancelled,
          ),
        ],
      );

      expect(find.text('Dr Ibrahim'), findsOneWidget);
      expect(find.text('Kingdom of X'), findsOneWidget);
      expect(find.text('Opening · Hall A'), findsNothing);
      expect(find.text('Withdrawn Guy'), findsNothing);
      // The card's meeting-type secondary line (no role field in the data).
      expect(find.text('Speaker meeting request'), findsOneWidget);
      expect(find.text('Delegation meeting request'), findsOneWidget);
      // Header + chips reflect only the two live meetings. الكل always shows;
      // Completed/Pending show because they are non-empty; Rejected is hidden
      // because its bucket is empty (Figma 1701:9406 three-chip parity).
      expect(find.text('All meetings (2)'), findsOneWidget);
      expect(find.text('All (2)'), findsOneWidget);
      expect(find.text('Completed (1)'), findsOneWidget);
      expect(find.text('Pending (1)'), findsOneWidget);
      expect(find.text('Rejected (0)'), findsNothing);
    });

    testWidgets('empty state when the feed has no meetings', (tester) async {
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
      expect(find.text('No meetings yet.'), findsOneWidget);
    });

    testWidgets('the status chips filter the meeting cards', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Accepted One',
            status: AppRequestStatus.accepted,
          ),
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '2',
            title: 'Pending One',
            status: AppRequestStatus.pending,
          ),
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '3',
            title: 'Rejected One',
            status: AppRequestStatus.rejected,
          ),
        ],
      );

      // الكل (All) is the default → every meeting.
      expect(find.text('Accepted One'), findsOneWidget);
      expect(find.text('Pending One'), findsOneWidget);
      expect(find.text('Rejected One'), findsOneWidget);

      await tester.tap(find.text('Completed (1)'));
      await tester.pumpAndSettle();
      expect(find.text('Accepted One'), findsOneWidget);
      expect(find.text('Pending One'), findsNothing);
      expect(find.text('Rejected One'), findsNothing);
      // The header shows the stable total (3), not the filtered subset.
      expect(find.text('All meetings (3)'), findsOneWidget);

      await tester.tap(find.text('Rejected (1)'));
      await tester.pumpAndSettle();
      expect(find.text('Rejected One'), findsOneWidget);
      expect(find.text('Accepted One'), findsNothing);
    });

    testWidgets('a speaker rank replaces the meeting-type secondary line',
        (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          // Speaker meeting WITH a rank → shows the rank (Figma 1701:9406).
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Dr Ibrahim',
            status: AppRequestStatus.accepted,
            subtitle: 'Environmental researcher',
          ),
          // Delegation meeting has no rank → falls back to the meeting type.
          _item(
            kind: AppRequestKind.delegationMeeting,
            id: '2',
            title: 'Kingdom of X',
            status: AppRequestStatus.pending,
          ),
        ],
      );

      expect(find.text('Environmental researcher'), findsOneWidget);
      // The speaker's meeting-type line is replaced by the rank; the delegation
      // (no rank) still shows its meeting-type line.
      expect(find.text('Speaker meeting request'), findsNothing);
      expect(find.text('Delegation meeting request'), findsOneWidget);
    });

    testWidgets('an accepted meeting shows the Confirmed badge', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Dr Ibrahim',
            status: AppRequestStatus.accepted,
          ),
        ],
      );
      expect(find.text('Confirmed'), findsOneWidget);
    });

    testWidgets('the card date line carries the slot date', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Dr Ibrahim',
            status: AppRequestStatus.accepted,
            eventDateUtc: DateTime.utc(2026, 1, 12, 12),
          ),
        ],
      );
      expect(find.textContaining('12 January'), findsOneWidget);
    });

    testWidgets('shows the error state on a wire failure', (tester) async {
      await _pump(tester, fail: true);
      expect(find.text('Could not load your requests'), findsOneWidget);
    });

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(
        tester,
        locale: const Locale('ar'),
        data: <AppRequestItem>[
          _item(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'د. ابراهيم',
            status: AppRequestStatus.accepted,
          ),
        ],
      );
      expect(find.text('المقابلات'), findsWidgets); // header title
      expect(find.text('مؤكدة'), findsOneWidget); // confirmed badge
      expect(
        Directionality.of(tester.element(find.text('د. ابراهيم'))),
        TextDirection.rtl,
      );
    });
  });
}
