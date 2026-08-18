import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';
import 'package:simf_app/features/sessions/widgets/session_speaker_card.dart';

SessionSpeaker _speaker({
  required SessionSpeakerRole role,
  String? title = 'Professor of Maritime Studies',
}) =>
    SessionSpeaker(
      id: 'sp1',
      name: 'Dr. Ali Al-Harbi',
      nameArabic: 'د. علي الحربي',
      displayOrder: 0,
      role: role,
      title: title,
    );

Future<void> _pumpCard(WidgetTester tester, SessionSpeaker speaker) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SessionSpeakerCard(
          speaker: speaker,
          isArabic: false,
          hostLabel: 'Host',
          baseUrl: 'http://test.local/api/v1',
          onTap: () {},
        ),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  // PAR-P4a — the host marker on the session-detail speaker card is the gold
  // star glyph beside the host label, which is what the speakers-list card's
  // own doc comment (D-432) already promised the detail renders.
  group('SessionSpeakerCard host marker (PAR-P4a)', () {
    testWidgets('a HOST renders the star glyph beside the host label',
        (tester) async {
      await _pumpCard(tester, _speaker(role: SessionSpeakerRole.host));

      expect(find.byIcon(Icons.star_rounded), findsOneWidget);
      expect(find.text('Host'), findsOneWidget);
      expect(find.text('Professor of Maritime Studies'), findsOneWidget);
    });

    testWidgets('a plain SPEAKER renders no star and no host label',
        (tester) async {
      await _pumpCard(tester, _speaker(role: SessionSpeakerRole.speaker));

      expect(find.byIcon(Icons.star_rounded), findsNothing);
      expect(find.text('Host'), findsNothing);
      expect(find.text('Professor of Maritime Studies'), findsOneWidget);
    });

    testWidgets('a HOST with no rank still renders the star + host label',
        (tester) async {
      await _pumpCard(
        tester,
        _speaker(role: SessionSpeakerRole.host, title: null),
      );

      expect(find.byIcon(Icons.star_rounded), findsOneWidget);
      expect(find.text('Host'), findsOneWidget);
    });

    testWidgets('a plain SPEAKER with no rank renders no sub-line at all',
        (tester) async {
      await _pumpCard(
        tester,
        _speaker(role: SessionSpeakerRole.speaker, title: null),
      );

      expect(find.byIcon(Icons.star_rounded), findsNothing);
      expect(find.text('Host'), findsNothing);
      expect(find.text('Dr. Ali Al-Harbi'), findsOneWidget);
    });
  });
}
