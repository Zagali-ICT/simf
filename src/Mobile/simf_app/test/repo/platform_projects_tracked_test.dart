import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Repo-hygiene ratchet for BUG-010, BUG-009 and STALE-GOLDEN-ARTIFACTS.
///
/// BUG-010 — `.gitignore` used to ignore EVERY native platform folder
/// (`android/ ios/ linux/ macos/ windows/ web/`) plus `pubspec.lock`, so a
/// clean clone had no buildable mobile artefact at all. `flutter create` cannot
/// restore what that lost: the CAMERA + USE_BIOMETRIC permissions (D-404 /
/// D-738), the launcher mipmaps (D-373), the localized launcher name (D-699)
/// and the SIMF-branded web shell. These tests fail the build the moment any of
/// that stops being tracked again. The D-768 pinned certificate and its
/// network-security config were part of that list until D-872 deleted them
/// along with the TLS bypass they existed to support.
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
    });

    test('the Kotlin package tracks the Gradle namespace', () {
      // Gradle resolves MainActivity through `namespace`, so the source
      // directory has to spell the same thing. Asserting a literal path let a
      // rename land half-done: the id moved off the com.example scaffold
      // default and this test still pointed at the directory it came from.
      //
      // Comment lines are dropped first. The real assignments sit under a
      // comment block that talks about applicationId, and an unanchored
      // firstMatch would happily read a commented-out value instead of the
      // live one — which is precisely the half-rename this guards against.
      final gradle = File('android/app/build.gradle.kts')
          .readAsLinesSync()
          .where((line) => !line.trimLeft().startsWith('//'))
          .join('\n');

      String? assigned(String key) => RegExp(
            '^\\s*$key\\s*=\\s*"([\\w.]+)"\\s*\$',
            multiLine: true,
          ).firstMatch(gradle)?.group(1);

      final namespace = assigned('namespace');
      final applicationId = assigned('applicationId');

      expect(
        namespace,
        isNotNull,
        reason: 'no live `namespace = "..."` in build.gradle.kts',
      );
      expect(applicationId, equals(namespace));

      final activity = File(
        'android/app/src/main/kotlin/'
        '${namespace!.replaceAll('.', '/')}/MainActivity.kt',
      );
      expect(
        activity.existsSync(),
        isTrue,
        reason: 'MainActivity.kt must sit under the namespace directory '
            '($namespace), or Gradle cannot find it.',
      );
      // Anchored: `contains` would also accept a package that merely STARTS
      // with the namespace, e.g. dod.simf.visitor_app_old.
      expect(
        RegExp('^package ${RegExp.escape(namespace)};?\\s*\$', multiLine: true)
            .hasMatch(activity.readAsStringSync()),
        isTrue,
        reason: 'MainActivity.kt must declare exactly `package $namespace`.',
      );

      // Google Play rejects the scaffold default outright, and applicationId
      // is immutable once a listing exists — so it must never come back.
      expect(namespace, isNot(startsWith('com.example')));
      for (final root in <String>['kotlin', 'java']) {
        expect(
          Directory('android/app/src/main/$root/com/example').existsSync(),
          isFalse,
          reason: 'a stale com/example tree survives under src/main/$root.',
        );
      }
    });

    test('the manifest keeps the CAMERA + USE_BIOMETRIC permissions', () {
      final manifest =
          File('android/app/src/main/AndroidManifest.xml').readAsStringSync();

      expect(manifest, contains('android.permission.CAMERA'));
      expect(manifest, contains('android.permission.USE_BIOMETRIC'));
    });

    test('no self-signed TLS escape hatch has come back (D-872)', () {
      // The app once shipped a blanket badCertificateCallback => true, which
      // removed MITM protection on every native connection (security
      // assessment C2). It existed only because the old hostname contained an
      // underscore and no public CA will issue for one; the API now has a real
      // certificate, so the bypass is gone. This asserts it stays gone: the
      // easiest way to "fix" a future TLS error is to reintroduce it, and that
      // would silently undo the control rather than fail loudly.
      expect(
        File('lib/core/net/self_signed_api_tls.dart').existsSync(),
        isFalse,
        reason: 'the self-signed TLS bypass was deleted under D-872.',
      );
      expect(
        File('lib/core/net/self_signed_api_tls_io.dart').existsSync(),
        isFalse,
      );

      // A custom network-security config is how the same hole is reopened on
      // the Android side, by re-adding a private trust anchor for the API host.
      final manifest =
          File('android/app/src/main/AndroidManifest.xml').readAsStringSync();
      expect(manifest, isNot(contains('networkSecurityConfig')));

      final dartSources = Directory('lib')
          .listSync(recursive: true)
          .whereType<File>()
          .where((file) => file.path.endsWith('.dart'));
      for (final source in dartSources) {
        expect(
          source.readAsStringSync(),
          isNot(contains('badCertificateCallback')),
          reason: '${source.path} accepts bad certificates — that is C2.',
        );
      }
    });

    test('the localized launcher name is tracked (D-699)', () {
      // This was decided once and then lost, because android/ was git-ignored
      // at the time and nothing outside a README remembered it. The label came
      // back as the flutter-create default. Assert the resources, not just the
      // manifest attribute, or the reference resolves to nothing.
      final manifest =
          File('android/app/src/main/AndroidManifest.xml').readAsStringSync();
      expect(manifest, contains('android:label="@string/app_name"'));

      final english = File('android/app/src/main/res/values/strings.xml');
      final arabic = File('android/app/src/main/res/values-ar/strings.xml');
      expect(english.existsSync(), isTrue);
      expect(arabic.existsSync(), isTrue);
      expect(english.readAsStringSync(), contains('>SIMF<'));
      expect(arabic.readAsStringSync(), contains('>الملتقى البحري<'));
    });

    test('no private key has been committed with a certificate', () {
      // The D-768 trust anchor is gone with the bypass (D-872), but the rule it
      // carried outlives it: if a .pem is ever added to the app again, it must
      // be a public certificate and never a key.
      final pems = Directory('android')
          .listSync(recursive: true)
          .whereType<File>()
          .where((file) => file.path.endsWith('.pem'));

      for (final pem in pems) {
        expect(
          pem.readAsStringSync(),
          isNot(contains('PRIVATE KEY')),
          reason: '${pem.path} contains a private key.',
        );
      }
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

  group('VENDORED PACKAGES — every file they import is in the repo', () {
    // Same defect as BUG-010, one directory deeper. `.gitignore` carried
    // `*.g.dart` for code build_runner regenerates locally, and it also matched
    // third_party/video_player_android/lib/src/messages.g.dart — a Pigeon file
    // that ships WITH the vendored package and that three tracked files in it
    // import. pubspec.yaml pins that package through `dependency_overrides`, so
    // every build resolves it: the app compiled for everyone who already had
    // the file on disk and could not compile from a clean clone at all.
    //
    // Asserting on imports rather than on one filename, so the next vendored
    // file an ignore rule swallows fails here too.
    test('every relative import under third_party resolves to a real file', () {
      final root = Directory('third_party');
      if (!root.existsSync()) {
        return;
      }

      final missing = <String>[];
      // Relative imports only — a `package:` or `dart:` import is resolved by
      // pub, not by a file sitting next to this one.
      final pattern = RegExp(r"""import\s+['"]([^:'"]+\.dart)['"]""");

      for (final file in root
          .listSync(recursive: true)
          .whereType<File>()
          .where((f) => f.path.endsWith('.dart'))) {
        final dir = file.parent.path;
        for (final match in pattern.allMatches(file.readAsStringSync())) {
          if (!File('$dir/${match.group(1)}').existsSync()) {
            missing.add('${file.path} imports ${match.group(1)}');
          }
        }
      }

      expect(
        missing,
        isEmpty,
        reason: 'A vendored file imports something that is not in the '
            'repository, so the app cannot build from a clean clone. Check '
            '.gitignore — a rule written for locally GENERATED code (*.g.dart, '
            '*.freezed.dart) also matches generated sources that ship inside a '
            'vendored package. Missing: ${missing.join(', ')}',
      );
    });
  });
}
