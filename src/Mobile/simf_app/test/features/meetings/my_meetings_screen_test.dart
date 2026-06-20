import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/intl.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/meetings/data/my_meetings_repository.dart';
import 'package:simf_app/features/meetings/my_meetings_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

MyMeetingItem _item({
  MyMeetingKind kind = MyMeetingKind.speaker,
  String id = 'm1',
  String title = 'Captain Reef',
  String subject = 'Trade talk',
  MyMeetingStatus status = MyMeetingStatus.pending,
  DateTime? slotStartUtc,
}) {
  return MyMeetingItem(
    kind: kind,
    id: id,
    title: title,
    titleArabic: '',
    subject: subject,
    status: status,
    slotStartUtc: slotStartUtc,
    createdAt: DateTime.utc(2030, 1, 1, 9),
  );
}

/// A fake repository (via `implements`, so it needs no real [SimfApiClient]).
class _FakeMyMeetingsRepository implements MyMeetingsRepository {
  _FakeMyMeetingsRepository({
    this.items = const <MyMeetingItem>[],
    this.fail = false,
  });

  List<MyMeetingItem> items;
  bool fail;
  int listCalls = 0;

  @override
  Future<List<MyMeetingItem>> getMyMeetings() async {
    listCalls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return items;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required MyMeetingsRepository repo,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        myMeetingsRepositoryProvider.overrideWithValue(repo),
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
        home: const MyMeetingsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MyMeetingsScreen (D-479 — read-only my meetings)', () {
    testWidgets('renders the meeting list with kinds and a status pill',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeMyMeetingsRepository(
          items: <MyMeetingItem>[
            _item(),
            _item(
              kind: MyMeetingKind.delegation,
              id: 'm2',
              title: 'Egypt',
              subject: 'Naval cooperation',
              status: MyMeetingStatus.accepted,
            ),
          ],
        ),
      );
      expect(find.text('Captain Reef'), findsOneWidget);
      expect(find.text('Egypt'), findsOneWidget);
      expect(find.text('Naval cooperation'), findsOneWidget);
      expect(find.text('Speaker meeting'), findsOneWidget);
      expect(find.text('Delegation meeting'), findsOneWidget);
      // Status pills.
      expect(find.text('Pending'), findsOneWidget);
      expect(find.text('Accepted'), findsOneWidget);
    });

    testWidgets('a row with a slot shows the slot time, not the created time',
        (tester) async {
      final slot = DateTime.utc(2030, 6, 10, 13, 30);
      await _pump(
        tester,
        repo: _FakeMyMeetingsRepository(
          items: <MyMeetingItem>[_item(slotStartUtc: slot)],
        ),
      );
      // The screen formats (slotStartUtc ?? createdAt).toLocal(); compute the
      // expected local string the same way so the assertion is tz-independent.
      final expected = DateFormat('d MMM · hh:mm a').format(slot.toLocal());
      expect(find.text(expected), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeMyMeetingsRepository());
      expect(find.text('You have no meetings yet'), findsOneWidget);
    });

    testWidgets('error shows retry, which re-fetches', (tester) async {
      final repo = _FakeMyMeetingsRepository(fail: true);
      await _pump(tester, repo: repo);
      expect(find.text('Could not load your meetings'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.listCalls, greaterThanOrEqualTo(2));
    });
  });
}
