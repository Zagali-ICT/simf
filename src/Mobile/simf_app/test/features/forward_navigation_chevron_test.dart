import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';
import 'package:simf_app/features/forum_guide/widgets/forum_guide_cards.dart';
import 'package:simf_app/features/more/widgets/more_profile_card.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/hub_row.dart';
import 'package:simf_app/features/sessions/widgets/session_reservation_card.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/widgets/speaker_sessions.dart';

/// Every forward caret must come from [SimfForwardChevron], which flips in LTR
/// only — a bare `SimfSvgIcon` never mirrors, so one screen shows carets both
/// ways.
void main() {
  final rows = <String, Widget>{
    'MoreProfileCard': MoreProfileCard(
      name: 'رائد السالم',
      tier: 'VIP',
      onTap: () {},
    ),
    'ForumGuideStep': const ForumGuideStep(
      number: 1,
      title: 'الخطوة',
      body: 'الوصف',
    ),
    'HubRow': HubRow(title: 'العنوان', subtitle: 'الوصف', onTap: () {}),
    'SessionReservationCard': SessionReservationCard(
      cell: const SeatCell(
        rowLabel: 'A',
        seatNumber: 3,
        kind: SeatReservationKind.userBooking,
      ),
      l10n: const AppL10n(Locale('en')),
      onView: () {},
    ),
    'SpeakerSessionRow': SpeakerSessionRow(
      session: SpeakerSession(
        id: 's1',
        code: 'S-1',
        title: 'Opening',
        titleArabic: 'الافتتاح',
        hallName: 'Main hall',
        hallNameArabic: 'القاعة الرئيسية',
        start: DateTime.utc(2026, 8, 20, 9),
        end: DateTime.utc(2026, 8, 20, 10),
      ),
      isArabic: false,
    ),
  };

  for (final row in rows.entries) {
    testWidgets('${row.key} renders its forward caret through the shared '
        'chevron', (tester) async {
      // MoreProfileCard's avatar is a ConsumerWidget, so the rows need a scope.
      await tester.pumpWidget(
        ProviderScope(
          child: MaterialApp(home: Scaffold(body: row.value)),
        ),
      );

      expect(find.byType(SimfForwardChevron), findsOneWidget);
    });
  }
}
