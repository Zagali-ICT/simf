import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/requests/data/request_models.dart';

/// The requests screen's chip rule, which used to live inside its build path.
///
/// The interesting case is the FALLBACK: a chip that drops to zero items must
/// not strand the user on a chip-less "no results" view. That is a real product
/// rule and it had no test.
AppRequestItem _item(String id, AppRequestStatus status) => AppRequestItem(
      kind: AppRequestKind.speakerMeeting,
      id: id,
      title: 'Request $id',
      titleArabic: 'طلب $id',
      status: status,
      createdAt: DateTime.utc(2026, 1, 10),
      canCancel: false,
    );

void main() {
  final pending = _item('p', AppRequestStatus.pending);
  final accepted = _item('a', AppRequestStatus.accepted);

  group('effectiveRequestFilter', () {
    test('null selection stays null — the All chip', () {
      expect(
        effectiveRequestFilter(<AppRequestItem>[pending], null),
        isNull,
      );
    });

    test('keeps a selection that still has rows', () {
      expect(
        effectiveRequestFilter(
          <AppRequestItem>[pending, accepted],
          AppRequestStatus.pending,
        ),
        AppRequestStatus.pending,
      );
    });

    test('falls back to All when the selected chip has emptied', () {
      // The user cancelled their only pending request while it was selected.
      expect(
        effectiveRequestFilter(
          <AppRequestItem>[accepted],
          AppRequestStatus.pending,
        ),
        isNull,
      );
    });

    test('falls back to All on an empty list', () {
      expect(
        effectiveRequestFilter(
          const <AppRequestItem>[],
          AppRequestStatus.pending,
        ),
        isNull,
      );
    });
  });

  group('filterRequests', () {
    test('All returns every row, in order', () {
      expect(
        filterRequests(<AppRequestItem>[pending, accepted], null)
            .map((r) => r.id),
        <String>['p', 'a'],
      );
    });

    test('a live selection narrows to it', () {
      expect(
        filterRequests(
          <AppRequestItem>[pending, accepted],
          AppRequestStatus.pending,
        ).map((r) => r.id),
        <String>['p'],
      );
    });

    test('an emptied selection shows everything, not nothing', () {
      // Without the fallback this would return an empty list under a chip the
      // user cannot see is empty.
      expect(
        filterRequests(
          <AppRequestItem>[accepted],
          AppRequestStatus.pending,
        ).map((r) => r.id),
        <String>['a'],
      );
    });
  });
}
