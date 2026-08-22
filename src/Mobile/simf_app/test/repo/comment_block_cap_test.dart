import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Caps how long a single comment block may be.
///
/// The sibling `comment_signal_ratchet_test` counts signal and asserts it never
/// FALLS, which protects documentation from a deletion sweep but permits
/// unlimited growth. That is the hole an audit walked through in August 2026,
/// adding 1,365 comment lines against 83 removed — blocks up to 45 lines
/// narrating what a fix used to do, when it broke and how it was found — with
/// every gate green throughout. The instruction had been the opposite: keep
/// comments brief, because the code should describe itself.
///
/// Length is the measure because length is the defect's shape. A one-to-five
/// line comment carrying a decision id or a non-obvious trap is what the repo
/// wants; a 40-line essay is a commit message in the wrong file.
void main() {
  const cap = 12;

  // Files already over the cap, each pinned at its current worst block.
  // Entries come OFF as blocks are shortened and are NEVER added: a new
  // essay must be shortened, not registered.
  const pinned = <String, int>{
  'app/localization/app_l10n.dart': 15,
  'app/router.dart': 16,
  'app/widgets/qr_scan_view.dart': 15,
  'app/widgets/simf_avatar.dart': 13,
  'app/widgets/simf_logo_image.dart': 18,
  'app/widgets/simf_page_shell.dart': 24,
  'app/widgets/simf_scanner_body.dart': 16,
  'app/widgets/simf_scanner_frame.dart': 14,
  'core/env/build_config.dart': 13,
  'core/responsive/grid_columns.dart': 36,
  'core/session/session_activity.dart': 17,
  'core/session/session_guard.dart': 19,
  'core/sharing/content_sharer.dart': 13,
  'core/utils/saudi_time.dart': 24,
  'core/utils/scan_gate.dart': 19,
  'core/validation/field_limits.dart': 17,
  'features/accessibility/data/accessibility_preferences_repository.dart': 14,
  'features/account/data/device_label.dart': 19,
  'features/account/data/profile_models.dart': 22,
  'features/account/data/profile_repository.dart': 24,
  'features/account/data/sign_up_visitor_form.dart': 13,
  'features/account/sign_up_visitor_submit.dart': 15,
  'features/badge/badge_screen.dart': 20,
  'features/badge/data/badge_identity_provider.dart': 18,
  'features/booths/widgets/booth_logo_tile.dart': 17,
  'features/delegations/widgets/delegation_meeting_request_sheet.dart': 13,
  'features/exhibition/widgets/entity_logo_image.dart': 14,
  'features/gates/data/gates_repository.dart': 13,
  'features/gates/data/offline_badge.dart': 15,
  'features/home/widgets/hero_background_video.dart': 18,
  'features/live/widgets/caption_strip.dart': 13,
  'features/meetings/widgets/meeting_card.dart': 14,
  'features/myarea/data/liveness.dart': 18,
  'features/onboarding/widgets/onboarding_background.dart': 14,
  'features/sessions/data/seat_map_models.dart': 13,
  'features/sessions/data/session_detail_eligibility.dart': 13,
  'features/sessions/data/session_lifecycle.dart': 15,
  'features/sessions/widgets/hall_seat_map.dart': 28,
  'features/sessions/widgets/programme_day_strip.dart': 13,
  'features/sessions/widgets/session_arrival_action.dart': 23,
  'features/speakers/widgets/meeting_request_form.dart': 31,
  'features/speakers/widgets/meeting_request_sheet.dart': 18,
  'features/staff/data/register_visitor_validators.dart': 14,
  'features/visitor_profile/data/visitor_profile_completeness.dart': 13,
  'features/visitor_profile/data/visitor_profile_form_state.dart': 14,
  'features/visitor_profile/data/visitor_profile_validators.dart': 20,
  };

  test('no comment block runs longer than the cap', () {
    final offenders = <String>[];

    for (final file in Directory('lib')
        .listSync(recursive: true)
        .whereType<File>()
        .where((f) => f.path.endsWith('.dart'))) {
      final relative =
          file.path.replaceAll(r'\', '/').replaceFirst('lib/', '');

      var run = 0;
      var worst = 0;
      for (final line in file.readAsLinesSync()) {
        if (line.trimLeft().startsWith('//')) {
          run++;
          if (run > worst) worst = run;
        } else {
          run = 0;
        }
      }

      final allowed = pinned[relative] ?? cap;
      if (worst > allowed) {
        offenders.add('  $relative: $worst lines (allowed $allowed)');
      }
    }

    expect(
      offenders,
      isEmpty,
      reason: 'A comment block is longer than this file may carry:\n'
          '${offenders.join('\n')}\n\n'
          'Split it, cut it to the fact the code cannot state, or move the '
          'history to the commit message. Do NOT add the file to the pinned '
          'list: it only shrinks.',
    );
  });

  test('every pinned entry still exists and is still over the cap', () {
    final stale = <String>[];

    for (final entry in pinned.entries) {
      final file = File('lib/${entry.key}');
      if (!file.existsSync()) {
        stale.add('  ${entry.key}: gone — remove it from `pinned`');
        continue;
      }

      var run = 0;
      var worst = 0;
      for (final line in file.readAsLinesSync()) {
        if (line.trimLeft().startsWith('//')) {
          run++;
          if (run > worst) worst = run;
        } else {
          run = 0;
        }
      }

      if (worst < entry.value) {
        stale.add('  ${entry.key}: now $worst, pinned at ${entry.value}');
      }
    }

    expect(
      stale,
      isEmpty,
      reason: 'These pins are looser than the code needs, so they would let a '
          'block grow back:\n${stale.join('\n')}\n\n'
          'Lower each to its real value, or drop the entry if it is now '
          'under the cap. That is what makes the list a ratchet.',
    );
  });
}
