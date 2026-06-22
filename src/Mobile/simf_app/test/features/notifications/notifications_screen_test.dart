import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/notifications/notifications_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

NotificationItem _item({
  String id = 'n1',
  String title = 'Session starts soon',
  String kind = 'SessionReminder',
  bool isRead = false,
  NotificationSeverity severity = NotificationSeverity.warning,
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
  );
}

/// A fake repository (via `implements`, so it needs no real [SimfApiClient]):
/// returns a configurable list (or throws), and records the read calls.
class _FakeNotificationsRepository implements NotificationsRepository {
  _FakeNotificationsRepository({
    this.items = const <NotificationItem>[],
    this.fail = false,
  });

  List<NotificationItem> items;
  bool fail;
  int listCalls = 0;
  int markAllCalls = 0;
  final List<String> readIds = <String>[];

  @override
  Future<int> getUnreadCount() async => items.where((n) => !n.isRead).length;

  @override
  Future<List<NotificationItem>> getNotifications({int skip = 0, int top = 50}) async {
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
    return true;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required NotificationsRepository repo,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        notificationsRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const NotificationsScreen(),
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

    testWidgets('opening an all-read inbox does not call mark-all', (tester) async {
      final repo = _FakeNotificationsRepository(
        items: <NotificationItem>[_item(isRead: true)],
      );
      await _pump(tester, repo: repo);
      expect(repo.markAllCalls, 0);
      expect(find.text('Mark all read'), findsNothing);
    });
  });
}
