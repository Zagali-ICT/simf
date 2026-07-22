import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/meetings/meetings_screen.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

AppRequestItem _meeting({
  required AppRequestKind kind,
  required String id,
  required String title,
  AppRequestStatus status = AppRequestStatus.accepted,
  DateTime? eventDateUtc,
  String? subtitle,
  String? speakerId,
  int? countryId,
}) =>
    AppRequestItem(
      kind: kind,
      id: id,
      title: title,
      titleArabic: title,
      status: status,
      createdAt: DateTime.utc(2026),
      eventDateUtc: eventDateUtc,
      canCancel: false,
      subtitle: subtitle,
      speakerId: speakerId,
      countryId: countryId,
    );

Future<void> _pump(
  WidgetTester tester, {
  List<AppRequestItem>? data,
  bool fail = false,
  bool isVip = true,
}) async {
  final router = GoRouter(
    initialLocation: '/meetings',
    routes: <RouteBase>[
      GoRoute(
        path: '/meetings',
        name: RouteNames.meetings,
        builder: (_, __) => const MeetingsScreen(),
      ),
      GoRoute(
        path: '/requests',
        name: RouteNames.requests,
        builder: (_, __) => const Scaffold(body: Text('HISTORY')),
      ),
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, __) => const Scaffold(body: Text('SPEAKER')),
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
        simfDataConfigProvider.overrideWithValue(_config),
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
  group('MeetingsScreen (Figma 1408:9726)', () {
    testWidgets('renders an approved upcoming speaker meeting with its actions',
        (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Dr Mohammed Al-Omari',
            subtitle: 'Marine researcher',
            eventDateUtc: DateTime.utc(2035),
            speakerId: 's1',
            countryId: 682,
          ),
        ],
      );
      // The card headline (kind) + the gold speaker name + the rank sub-line.
      expect(find.text('Speaker meeting request'), findsOneWidget);
      expect(find.text('Dr Mohammed Al-Omari'), findsOneWidget);
      expect(find.text('Marine researcher'), findsOneWidget);
      // The two request buttons (both flags on) + the history pill.
      expect(find.text('Request a speaker meeting'), findsOneWidget);
      expect(find.text('Request a delegation meeting'), findsOneWidget);
      expect(find.text('Log'), findsOneWidget);
    });

    testWidgets('shows the empty state when there are no upcoming meetings',
        (tester) async {
      await _pump(tester);
      expect(find.text('No meetings yet.'), findsOneWidget);
      // The request buttons still show so an entitled user can start a meeting.
      expect(find.text('Request a speaker meeting'), findsOneWidget);
    });

    testWidgets('only approved + upcoming meetings appear (pending/past/rejected '
        'excluded)', (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Future Speaker',
            eventDateUtc: DateTime.utc(2035),
          ),
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '2',
            title: 'Past Speaker',
            eventDateUtc: DateTime.utc(2020),
          ),
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '3',
            title: 'Pending Speaker',
            status: AppRequestStatus.pending,
            eventDateUtc: DateTime.utc(2035),
          ),
          // A non-meeting kind never belongs on this page.
          _meeting(
            kind: AppRequestKind.badgeUpdate,
            id: '4',
            title: 'Badge Change',
          ),
        ],
      );
      expect(find.text('Future Speaker'), findsOneWidget);
      expect(find.text('Past Speaker'), findsNothing);
      expect(find.text('Pending Speaker'), findsNothing);
      expect(find.text('Badge Change'), findsNothing);
    });

    testWidgets('an accepted meeting with no slot yet still counts as upcoming',
        (tester) async {
      await _pump(
        tester,
        data: <AppRequestItem>[
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Undated Speaker',
          ),
        ],
      );
      expect(find.text('Undated Speaker'), findsOneWidget);
    });

    testWidgets("a meeting dated today survives once its start time passes "
        "(date filter, not instant)", (tester) async {
      // Today at 00:00 local: its DATE is today but its instant is already past,
      // so an instant filter would wrongly drop it — a date filter keeps it.
      final now = DateTime.now();
      final earlierToday = DateTime(now.year, now.month, now.day).toUtc();
      await _pump(
        tester,
        data: <AppRequestItem>[
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Today Speaker',
            eventDateUtc: earlierToday,
          ),
        ],
      );
      expect(find.text('Today Speaker'), findsOneWidget);
    });

    testWidgets('السجل (Log) opens the requests-history page', (tester) async {
      await _pump(tester);
      await tester.tap(find.text('Log'));
      await tester.pumpAndSettle();
      expect(find.text('HISTORY'), findsOneWidget);
    });

    testWidgets('a non-VIP sees the VIP-only state, not the list',
        (tester) async {
      await _pump(
        tester,
        isVip: false,
        data: <AppRequestItem>[
          _meeting(
            kind: AppRequestKind.speakerMeeting,
            id: '1',
            title: 'Should Not Show',
            eventDateUtc: DateTime.utc(2035),
          ),
        ],
      );
      expect(
        find.text(
          'Bilateral meetings are available to authorised accounts only',
        ),
        findsOneWidget,
      );
      expect(find.text('Should Not Show'), findsNothing);
      expect(find.text('Request a speaker meeting'), findsNothing);
    });

    testWidgets('shows the error state on a wire failure', (tester) async {
      await _pump(tester, fail: true);
      expect(find.text('Could not load your requests'), findsOneWidget);
    });
  });
}
