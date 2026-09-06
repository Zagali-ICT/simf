import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Account deletion must be reachable from every screen that HOLDS an account.
///
/// Apple rejected this app twice under App Store guideline 5.1.1(v) saying it
/// "supports account creation but does not include an option to initiate
/// account deletion". Both times it did have one. Both times the reviewer was
/// on a screen that could not reach it.
///
/// The trap is that an account exists from sign-up onward - before email
/// verification, before the profile is complete, before the organiser approves
/// it - while the bottom navigation that leads to My Area appears only once the
/// account is approved. `post_auth_route` and `splash_controller` then send an
/// incomplete profile back to the sign-up form on EVERY launch, so for a
/// half-registered account those gate screens are the whole app.
///
/// So the rule is not "My Area has a delete link". It is: every screen that can
/// hold an account offers one. This pins the mount sites, because the same
/// defect has now been shipped twice by fixing one screen and assuming the
/// others were covered.
void main() {
  /// Screen (or the widget that builds its body) -> why an account can be held
  /// there with no path to My Area.
  const holdsAnAccount = <String, String>{
    'lib/features/account/sign_up_visitor_screen.dart':
        'profileComplete == false is routed here on every launch '
            '(post_auth_route.dart, splash_controller.dart)',
    'lib/features/account/widgets/sign_up_interests_body.dart':
        'step two of profile completion; the account already exists',
    'lib/features/registration/registration_status_screen.dart':
        'awaiting organiser approval; offers sign-out, which is not deletion',
    'lib/features/myarea/my_area_screen.dart':
        'the pending branch (_buildLimited) has no dashboard',
    'lib/features/myarea/widgets/my_area_more_section.dart':
        'the approved dashboard',
  };

  test('every screen that can hold an account offers deletion', () {
    final missing = <String>[];
    holdsAnAccount.forEach((path, reason) {
      final file = File(path);
      if (!file.existsSync()) {
        missing.add('$path: FILE MISSING (renamed or deleted?) - $reason');
        return;
      }
      final source = file.readAsStringSync();
      final offers = source.contains('AccountDeletionFooter') ||
          source.contains('DeleteAccountTile');
      if (!offers) {
        missing.add('$path: $reason');
      }
    });

    expect(
      missing,
      isEmpty,
      reason: 'A screen that holds an account has no way to delete it. Apple '
          'rejects this under 5.1.1(v), and has already done so twice:\n  '
          '${missing.join('\n  ')}',
    );
  });

  test('the deletion widgets still do what their name says', () {
    // A guard that only greps for a name would pass on an empty widget.
    final footer =
        File('lib/core/widgets/account_deletion_footer.dart').readAsStringSync();
    expect(footer.contains('DeleteAccountTile'), isTrue);

    final tile = File('lib/features/myarea/widgets/delete_account_tile.dart')
        .readAsStringSync();
    expect(
      tile.contains('deleteMyAccount'),
      isTrue,
      reason: 'the tile must still call the erase endpoint',
    );
    expect(
      tile.contains('SimfConfirmDialog'),
      isTrue,
      reason: 'deletion is irreversible and must stay behind a confirmation',
    );
  });
}
