import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/notifications/notifications_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

NotificationItem _item({
  String id = 'n1',
  String title = 'Session starts soon',
  String kind = 'SessionReminder',
  bool isRead = false,
  NotificationSeverity severity = NotificationSeverity.warning,
  String? relatedEntityType,
  String? relatedEntityId,
  String? clickUrl,
  String? group,
}) {
  return NotificationItem(
    id: id,
    kind: kind,
    title: title,
    titleArabic: '',
    body: 'Hall A in 15 minutes.',
    bodyArabic: '',
    severity: severity,
    isRead: isRead,
    relatedEntityType: relatedEntityType,
    relatedEntityId: relatedEntityId,
    clickUrl: clickUrl,
    group: group,
  );
}

/// A fake repository (via `implements`, so it needs no real [SimfApiClient]):
/// returns a configurable list (or throws), and records the read calls.
class _FakeNotificationsRepository implements NotificationsRepository {
  _FakeNotificationsRepository({
    this.items = const <NotificationItem>[],
    this.fail = false,
    this.failMarkAll = false,
  });

  List<NotificationItem> items;
  bool fail;

  /// Lets a test reach a genuinely UNREAD row: the inbox marks everything read
  /// on open (#13), so without a failing mark-all there is nothing left to tap.
  bool failMarkAll;
  int listCalls = 0;
  int markAllCalls = 0;
  final List<String> readIds = <String>[];

  @override
  Future<int> getUnreadCount() async => items.where((n) => !n.isRead).length;

  @override
  Future<List<NotificationItem>> getNotifications(
      {int skip = 0, int top = 50,}) async {
    listCalls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return items;
  }

  @override
  Future<bool> markRead(String id) async {
    readIds.add(id);
    return true;
  }

  @override
  Future<bool> markAllRead() async {
    markAllCalls++;
    if (failMarkAll) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return true;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required NotificationsRepository repo,
}) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        notificationsRepositoryProvider.overrideWithValue(repo),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: NotificationsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('NotificationsScreen (Page 033 — KSA frame 223:4264)', () {
    testWidgets('renders the notification list', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item()],
        ),
      );
      expect(find.text('Session starts soon'), findsOneWidget);
      expect(find.text('Hall A in 15 minutes.'), findsOneWidget);
      // The frame chrome: search + the three filter chips.
      expect(find.text('All'), findsOneWidget);
      expect(find.text('Sessions'), findsOneWidget);
      expect(find.text('VIP'), findsOneWidget);
    });

    testWidgets('the Sessions chip filters to session-kind items',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[
            _item(id: 's1', title: 'Seat confirmed', kind: 'BookingConfirmed'),
            _item(id: 'v1', title: 'VIP invitation', kind: 'VipBroadcast'),
          ],
        ),
      );
      expect(find.text('Seat confirmed'), findsOneWidget);
      expect(find.text('VIP invitation'), findsOneWidget);

      await tester.tap(find.text('Sessions'));
      await tester.pumpAndSettle();
      expect(find.text('Seat confirmed'), findsOneWidget);
      expect(find.text('VIP invitation'), findsNothing);
    });

    testWidgets('search filters by title', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[
            _item(id: 'a', title: 'Opening ceremony'),
            _item(id: 'b', title: 'Lunch break'),
          ],
        ),
      );
      await tester.enterText(find.byType(TextField), 'lunch');
      await tester.pumpAndSettle();
      expect(find.text('Lunch break'), findsOneWidget);
      expect(find.text('Opening ceremony'), findsNothing);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeNotificationsRepository());
      expect(find.text('No notifications yet'), findsOneWidget);
    });

    testWidgets('error shows retry, which re-fetches', (tester) async {
      final repo = _FakeNotificationsRepository(fail: true);
      await _pump(tester, repo: repo);
      expect(find.text('Could not load your notifications.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.listCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('opening marks every unread notification read (#13/#14)',
        (tester) async {
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[_item()],
      );
      await _pump(tester, repo: repo);
      // The backend has no separate "seen" state, so opening the inbox marks
      // everything read: the item stays listed, but the unread affordance
      // clears and the explicit "Mark all read" button is no longer offered.
      expect(repo.markAllCalls, 1);
      expect(find.text('Session starts soon'), findsOneWidget);
      expect(find.text('Mark all read'), findsNothing);
    });

    testWidgets('opening an all-read inbox does not call mark-all',
        (tester) async {
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[_item(isRead: true)],
      );
      await _pump(tester, repo: repo);
      expect(repo.markAllCalls, 0);
      expect(find.text('Mark all read'), findsNothing);
    });

    testWidgets('tapping an unread item flips it read WITHOUT re-fetching',
        (tester) async {
      // The inbox marks everything read on open (#13), so a failing mark-all is
      // how a genuinely unread row survives to be tapped. The point is the
      // second assertion: the row flips from the screen's own read overlay and
      // the list is NOT re-fetched to get there — a reload per tap is exactly
      // what that overlay exists to avoid.
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[_item(id: 'fresh', title: 'Fresh one')],
        failMarkAll: true,
      );
      await _pump(tester, repo: repo);
      final listCallsAfterOpen = repo.listCalls;

      await tester.tap(find.text('Fresh one'));
      await tester.pumpAndSettle();

      expect(repo.readIds, contains('fresh'));
      expect(
        repo.listCalls,
        listCallsAfterOpen,
        reason: 'marking one item read must not re-fetch the whole list',
      );
    });

    testWidgets(
        'tapping a read SessionRatingRequest deep-links to the Session rate '
        'form', (tester) async {
      // Regression: the inbox auto-marks everything read on open, so an
      // actionable notification is already read by the time it is tapped. The
      // card must stay tappable (not gated on `unread`) and deep-link to the
      // per-session rating form — otherwise the end-of-session prompt is
      // unreachable.
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[
          _item(
            id: 'sr1',
            title: 'Rate this session',
            kind: 'SessionRatingRequest',
            isRead: true,
            severity: NotificationSeverity.info,
            relatedEntityType: 'Session',
            relatedEntityId: 'sess-7',
          ),
        ],
      );
      final router = GoRouter(
        initialLocation: '/',
        routes: <RouteBase>[
          GoRoute(
            name: RouteNames.notifications,
            path: '/',
            builder: (context, state) => const NotificationsScreen(),
          ),
          GoRoute(
            name: RouteNames.rate,
            path: '/rate',
            builder: (context, state) => Text(
              'RATE code=${state.uri.queryParameters['code']} '
              'target=${state.uri.queryParameters['targetId']}',
            ),
          ),
        ],
      );
      await tester.pumpWidget(
        simfTestScope(
          overrides: <Override>[
            notificationsRepositoryProvider.overrideWithValue(repo),
          ],
          child: MaterialApp.router(
            locale: const Locale('en'),
            supportedLocales: AppL10n.supportedLocales,
            localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
              ...AppL10n.localizationsDelegates,
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            routerConfig: router,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Rate this session'), findsOneWidget);

      await tester.tap(find.text('Rate this session'));
      await tester.pumpAndSettle();

      // Deep-linked into the reusable rate screen with the Session code + the
      // notification's session id.
      expect(find.text('RATE code=Session target=sess-7'), findsOneWidget);
    });

    testWidgets('tapping a BookingConfirmed notification opens the badge QR',
        (tester) async {
      // A confirmed booking mints the visitor's entry badge — the notification
      // must deep-link to the personal QR badge scanned at the gate (owner:
      // "on notification a QR is created, on click open the QR").
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[
          _item(
            id: 'bc1',
            title: 'Seat confirmed',
            kind: 'BookingConfirmed',
            isRead: true,
          ),
        ],
      );
      final router = GoRouter(
        initialLocation: '/',
        routes: <RouteBase>[
          GoRoute(
            name: RouteNames.notifications,
            path: '/',
            builder: (context, state) => const NotificationsScreen(),
          ),
          GoRoute(
            name: RouteNames.badge,
            path: '/badge',
            builder: (context, state) => const Text('BADGE QR'),
          ),
        ],
      );
      await tester.pumpWidget(
        simfTestScope(
          overrides: <Override>[
            notificationsRepositoryProvider.overrideWithValue(repo),
          ],
          child: MaterialApp.router(
            locale: const Locale('en'),
            supportedLocales: AppL10n.supportedLocales,
            localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
              ...AppL10n.localizationsDelegates,
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            routerConfig: router,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Seat confirmed'), findsOneWidget);

      await tester.tap(find.text('Seat confirmed'));
      await tester.pumpAndSettle();

      expect(find.text('BADGE QR'), findsOneWidget);
    });

    testWidgets('tapping a clickUrl notification pushes that in-app location',
        (tester) async {
      // Backend-driven deep-link (D-678): a Day-rating prompt carries a
      // clickUrl, pushed verbatim because its path is allowlisted.
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[
          _item(
            id: 'day1',
            title: 'Rate today',
            kind: 'DayRatingRequest',
            isRead: true,
            clickUrl: '/rate?code=Day&targetId=day-7',
            group: 'Ratings',
          ),
        ],
      );
      final router = GoRouter(
        initialLocation: '/',
        routes: <RouteBase>[
          GoRoute(
            name: RouteNames.notifications,
            path: '/',
            builder: (context, state) => const NotificationsScreen(),
          ),
          GoRoute(
            path: '/rate',
            builder: (context, state) => Text(
              'RATE code=${state.uri.queryParameters['code']} '
              'target=${state.uri.queryParameters['targetId']}',
            ),
          ),
        ],
      );
      await tester.pumpWidget(
        simfTestScope(
          overrides: <Override>[
            notificationsRepositoryProvider.overrideWithValue(repo),
          ],
          child: MaterialApp.router(
            locale: const Locale('en'),
            supportedLocales: AppL10n.supportedLocales,
            localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
              ...AppL10n.localizationsDelegates,
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            routerConfig: router,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Rate today'), findsOneWidget);

      await tester.tap(find.text('Rate today'));
      await tester.pumpAndSettle();

      expect(find.text('RATE code=Day target=day-7'), findsOneWidget);
    });

    testWidgets('the Sessions chip includes the new Ratings group (D-678)',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[
            _item(
              id: 'r1',
              title: 'Rate today',
              kind: 'DayRatingRequest',
              group: 'Ratings',
            ),
            _item(
                id: 'v1',
                title: 'VIP invite',
                kind: 'VipBroadcast',
                group: 'Vip',),
          ],
        ),
      );
      expect(find.text('Rate today'), findsOneWidget);
      expect(find.text('VIP invite'), findsOneWidget);

      await tester.tap(find.text('Sessions'));
      await tester.pumpAndSettle();
      // Ratings is inside the Sessions chip; Vip is not.
      expect(find.text('Rate today'), findsOneWidget);
      expect(find.text('VIP invite'), findsNothing);
    });
  });

  // The category icon is styled per notification kind (Figma 758:2491), not per
  // severity. The colour of the 40x40 circle behind a given glyph.
  Color iconColor(WidgetTester tester, IconData glyph) {
    final container = tester.widget<Container>(
      find
          .ancestor(of: find.byIcon(glyph), matching: find.byType(Container))
          .first,
    );
    return (container.decoration! as BoxDecoration).color!;
  }

  group('category icon — per-kind colour + glyph (Figma 758:2491)', () {
    testWidgets('AccountApproved → gold ticket', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item(kind: 'AccountApproved')],
        ),
      );
      expect(find.byIcon(Icons.confirmation_number_rounded), findsOneWidget);
      expect(
        iconColor(tester, Icons.confirmation_number_rounded),
        SimfTokens.accent,
      );
    });

    testWidgets('DeviceKeyEnrolled → gold fingerprint, not the severity mark',
        (tester) async {
      // A biometric credential was bound to the account. It sits with the
      // credential-info kinds rather than the green completions, because the
      // row exists for the case where the owner did NOT do it. Seeing the
      // warning fallback (priority_high) here would mean the kind is unmapped,
      // which is how it first shipped.
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item(kind: 'DeviceKeyEnrolled')],
        ),
      );
      expect(find.byIcon(Icons.fingerprint_rounded), findsOneWidget);
      expect(find.byIcon(Icons.priority_high_rounded), findsNothing);
      expect(
        iconColor(tester, Icons.fingerprint_rounded),
        SimfTokens.accent,
      );
    });

    testWidgets('SessionReminder → green check', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item()],
        ),
      );
      expect(find.byIcon(Icons.check_circle_rounded), findsOneWidget);
      expect(
        iconColor(tester, Icons.check_circle_rounded),
        SimfTokens.notifGreen,
      );
    });

    testWidgets('MeetingScheduled → green card', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item(kind: 'MeetingScheduled')],
        ),
      );
      expect(find.byIcon(Icons.credit_card_rounded), findsOneWidget);
      expect(
        iconColor(tester, Icons.credit_card_rounded),
        SimfTokens.notifGreen,
      );
    });

    testWidgets('VIP invitation → coral (star, not the mockup ✕)',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item(kind: 'VipBroadcast')],
        ),
      );
      expect(find.byIcon(Icons.star_rounded), findsOneWidget);
      expect(iconColor(tester, Icons.star_rounded), SimfTokens.notifCoral);
    });

    testWidgets('a rejection → coral alert glyph', (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[_item(kind: 'BookingRejected')],
        ),
      );
      expect(find.byIcon(Icons.event_busy_rounded), findsOneWidget);
      expect(
        iconColor(tester, Icons.event_busy_rounded),
        SimfTokens.notifCoral,
      );
    });

    testWidgets('an unknown/future kind falls back to its severity style',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeNotificationsRepository(
          items: <NotificationItem>[
            _item(kind: 'SomeFutureKind', severity: NotificationSeverity.error),
          ],
        ),
      );
      // Severity fallback: error → danger + close glyph (not a per-kind
      // colour).
      expect(find.byIcon(Icons.close_rounded), findsOneWidget);
      expect(iconColor(tester, Icons.close_rounded), SimfTokens.danger);
    });
  });
}
