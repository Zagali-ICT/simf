import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Repo-hygiene ratchet for BUG-010, BUG-009 and STALE-GOLDEN-ARTIFACTS.
///
/// BUG-010 — `.gitignore` used to ignore EVERY native platform folder
/// (`android/ ios/ linux/ macos/ windows/ web/`) plus `pubspec.lock`, so a
/// clean clone had no buildable mobile artefact at all. `flutter create` cannot
/// restore what that lost: the CAMERA + USE_BIOMETRIC permissions (D-404 /
/// D-738), the
/// `android:networkSecurityConfig` + the pinned `res/raw/simf_api_cert.pem`
/// (D-768), the launcher mipmaps (D-373) and the SIMF-branded web shell. These
/// tests fail the build the moment any of that stops being tracked again.
///
/// BUG-009 — a stale DUPLICATE of the two local packages sat at
/// `src/Mobile/packages/`. The pubspec resolves `packages/simf_{auth,data}_pkg`
/// relative to this app, so the outer copy was orphaned and had diverged. It
/// was deleted; this test stops it coming back.
///
/// STALE-GOLDEN-ARTIFACTS — 48 golden-comparison PNGs were committed under
/// `test/golden/failures/`. That directory is what `flutter test` writes when a
/// golden FAILS, so the committed set was frozen debris from a superseded
/// revision while the suite ran green. Deleted + ignored; these tests stop it
/// coming back.
///
/// The working directory for `flutter test` is the package root
/// (`src/Mobile/simf_app`), so every path below is relative to that.
void main() {
  group('BUG-010 — the native platform projects are tracked', () {
    test('the Android project is present', () {
      expect(
        Directory('android').existsSync(),
        isTrue,
        reason: 'android/ is the hand-edited Android project — it must be '
            'committed, never regenerated with `flutter create`.',
      );
      expect(File('android/settings.gradle.kts').existsSync(), isTrue);
      expect(File('android/app/build.gradle.kts').existsSync(), isTrue);
      expect(
        File('android/app/src/main/kotlin/com/example/simf_app/MainActivity.kt')
            .existsSync(),
        isTrue,
      );
    });

    test('the manifest keeps the CAMERA + USE_BIOMETRIC permissions and the '
        'network-security config', () {
      final manifest =
          File('android/app/src/main/AndroidManifest.xml').readAsStringSync();

      expect(manifest, contains('android.permission.CAMERA'));
      expect(manifest, contains('android.permission.USE_BIOMETRIC'));
      expect(
        manifest,
        contains(
          'android:networkSecurityConfig='
          '"@xml/network_security_config"',
        ),
      );
    });

    test('the D-768 pinned certificate and its trust config are tracked', () {
      final config =
          File('android/app/src/main/res/xml/network_security_config.xml');
      final cert = File('android/app/src/main/res/raw/simf_api_cert.pem');

      expect(config.existsSync(), isTrue);
      expect(cert.existsSync(), isTrue);
      expect(config.readAsStringSync(), contains('@raw/simf_api_cert'));

      final pem = cert.readAsStringSync();
      expect(pem, contains('BEGIN CERTIFICATE'));
      // A private key must never be committed: the anchor is public-only.
      expect(pem, isNot(contains('PRIVATE KEY')));
    });

    test('the launcher icons are tracked (D-373)', () {
      for (final density in <String>[
        'mdpi',
        'hdpi',
        'xhdpi',
        'xxhdpi',
        'xxxhdpi',
      ]) {
        expect(
          File('android/app/src/main/res/mipmap-$density/ic_launcher.png')
              .existsSync(),
          isTrue,
          reason: 'mipmap-$density/ic_launcher.png is missing.',
        );
      }
    });

    test('no signing secret is committed', () {
      expect(File('android/key.properties').existsSync(), isFalse);
      expect(
        File('android/app/google-services.json').existsSync(),
        isFalse,
      );
      final keystores = Directory('android')
          .listSync(recursive: true)
          .whereType<File>()
          .map((f) => f.path)
          .where((p) => p.endsWith('.jks') || p.endsWith('.keystore'))
          .toList();
      expect(
        keystores,
        isEmpty,
        reason: 'A signing keystore must never be committed '
            '(NCA A11-16): $keystores',
      );
    });

    test('the SIMF-branded web shell is tracked', () {
      final index = File('web/index.html');
      expect(index.existsSync(), isTrue);
      expect(index.readAsStringSync(), contains('SIMF'));
      expect(File('web/manifest.json').existsSync(), isTrue);
    });

    test('pubspec.lock is tracked so a clean clone resolves the same versions',
        () {
      expect(File('pubspec.lock').existsSync(), isTrue);
    });
  });

  group('STALE-GOLDEN-ARTIFACTS — golden failure output is not committed', () {
    test('the repo .gitignore keeps the failures directory out', () {
      // The ignore rule — not an "is the directory empty" check — is the
      // durable guard, and the only one that is deterministic: a golden that
      // fails in THIS very run writes into test/golden/failures/ while the
      // suite is executing, so asserting the directory is empty would turn one
      // red golden into two failures and point at the wrong cause.
      // Ignored ⇒ untracked ⇒ the artefacts cannot be committed again.
      //
      // The working directory is src/Mobile/simf_app, so the repo root is 3 up.
      final ignore = File('../../../.gitignore');
      expect(ignore.existsSync(), isTrue);
      expect(
        ignore.readAsStringSync(),
        contains('src/Mobile/simf_app/test/golden/failures/'),
        reason: 'test/golden/failures/ holds the four diff PNGs `flutter test` '
            'writes for each FAILING golden — output, never input. 48 were '
            'committed and had gone stale against a superseded revision, so '
            'anyone grepping the directory read obsolete debris. Without this '
            'ignore rule the next failing run re-commits them on the next '
            '`git add`.',
      );
    });

    test('the golden MASTERS are still tracked (the right directory was '
        'deleted)', () {
      final masters = Directory('test/golden/goldens');
      expect(masters.existsSync(), isTrue);
      expect(
        masters.listSync().whereType<File>().where(
              (f) => f.path.endsWith('.png'),
            ),
        isNotEmpty,
        reason: 'test/golden/goldens/ holds the golden masters — the INPUT to '
            'every golden comparison. Only test/golden/failures/ (the output of '
            'a failing run) was removed.',
      );
    });
  });

  group('BUG-009 — the duplicate package tree is gone', () {
    test('the consumed packages live under simf_app/packages', () {
      expect(Directory('packages/simf_auth_pkg').existsSync(), isTrue);
      expect(Directory('packages/simf_data_pkg').existsSync(), isTrue);
    });

    test('the orphaned src/Mobile/packages copy no longer exists', () {
      expect(
        Directory('../packages').existsSync(),
        isFalse,
        reason: 'src/Mobile/packages/ is a stale duplicate nothing resolves — '
            'the pubspec points at src/Mobile/simf_app/packages/.',
      );
    });
  });
}
