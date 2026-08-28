import 'dart:convert';
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
      // with the namespace, e.g. com.simrsnf.simf_old.
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

    test('the manifest keeps the store-facing permission decisions', () {
      // Three deliberate decisions in the one file `flutter create` overwrites
      // silently. Each is a Play listing fact, so losing one is not a build
      // break — it is a wrong permission shipped to users.
      final manifest =
          File('android/app/src/main/AndroidManifest.xml').readAsStringSync();

      // INTERNET reaches the release build only via ML Kit's transitive
      // play-services graph. Declared here so dropping that dependency cannot
      // take network access with it.
      expect(manifest, contains('android.permission.INTERNET'));

      // camera_android_camerax contributes RECORD_AUDIO; this app never
      // records, so it is rejected at merge time.
      expect(
        manifest,
        matches(
          RegExp(
            r'RECORD_AUDIO"\s*\n?\s*tools:node="remove"',
            multiLine: true,
          ),
        ),
        reason: 'RECORD_AUDIO must stay removed — the app opens no microphone.',
      );

      // The merger implies READ_EXTERNAL_STORAGE with NO cap, which would
      // request storage on every API level.
      expect(
        manifest,
        matches(
          RegExp(
            r'READ_EXTERNAL_STORAGE"\s*\n?\s*android:maxSdkVersion="32"',
            multiLine: true,
          ),
        ),
        reason: 'READ_EXTERNAL_STORAGE must stay capped at API 32.',
      );

      // camera_android_camerax injects camera.any with no android:required,
      // which DEFAULTS TO TRUE and made the shipped bundle camera-mandatory on
      // Play — the opposite of the required="false" on the two features above.
      // tools:replace is load-bearing: android:required uses an OR merge, so a
      // plain required="false" loses to the library's implied true and changes
      // nothing in the merged manifest.
      expect(
        manifest,
        matches(
          RegExp(
            r'camera\.any"\s+android:required="false"\s*\n?\s*'
            'tools:replace="android:required"',
            multiLine: true,
          ),
        ),
        reason: 'camera.any must stay optional, and only tools:replace wins.',
      );

      // androidx.biometric contributes USE_FINGERPRINT uncapped; it has been
      // deprecated since API 28 in favour of USE_BIOMETRIC.
      expect(
        manifest,
        matches(
          RegExp(
            r'USE_FINGERPRINT"\s*\n?\s*android:maxSdkVersion="28"',
            multiLine: true,
          ),
        ),
        reason: 'USE_FINGERPRINT must stay capped at API 28.',
      );

      // allowBackup defaults to TRUE, and auto-backup copies plaintext
      // SharedPreferences — including the AES-256 offline badge key — to the
      // user's Drive, where the "armed only during the event" control that
      // justifies storing it in the clear no longer applies.
      expect(
        manifest,
        contains('android:allowBackup="false"'),
        reason: 'auto-backup must stay off — it would copy the badge key out.',
      );
    });

    test('no camera in the app enables audio capture', () {
      // The justification for removing RECORD_AUDIO above, made enforceable:
      // if a CameraController ever turns audio on, the manifest edit becomes a
      // silent bug rather than a correct simplification.
      final dartSources = Directory('lib')
          .listSync(recursive: true)
          .whereType<File>()
          .where((file) => file.path.endsWith('.dart'));
      for (final source in dartSources) {
        expect(
          source.readAsStringSync(),
          isNot(contains('enableAudio: true')),
          reason: '${source.path} enables audio, but RECORD_AUDIO is removed.',
        );
      }
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
      // TRACKED, not "exists". These files are SUPPOSED to be on a machine
      // that builds a signed release - android/key.properties is exactly what
      // build.gradle.kts reads, and the pipeline writes one on the agent. They
      // are git-ignored, and ignored is what makes them safe.
      //
      // This asserted non-existence until 2026-08-22, when the owner generated
      // the upload key and did precisely the right thing: keystore outside the
      // repo, key.properties local and ignored. The build went red for it. A
      // guard that fails on correct behaviour trains people to disable it.
      expect(
        tracked('android/key.properties'),
        isEmpty,
        reason: 'android/key.properties must stay git-ignored, never tracked.',
      );
      expect(tracked('android/app/google-services.json'), isEmpty);
      expectNoCommittedSecrets(
        'android',
        const <String>['.jks', '.keystore'],
        reason: 'a signing keystore must never be committed (NCA A11-16)',
      );
    });

    test('the iOS project is present', () {
      // `flutter create` rewrites android/ as well as ios/, which is exactly
      // what the group above forbids. This project was therefore generated in
      // a scratch directory and only ios/ was copied in; it is hand-edited
      // from here, like android/.
      expect(
        Directory('ios').existsSync(),
        isTrue,
        reason: 'ios/ is the hand-edited iOS project - it must be committed, '
            'never regenerated with `flutter create` in this directory.',
      );
      expect(
        File('ios/Runner.xcodeproj/project.pbxproj').existsSync(),
        isTrue,
      );
      expect(File('ios/Runner/Info.plist').existsSync(), isTrue);
      // CocoaPods only runs on a Mac, so `flutter create` never writes a
      // Podfile. Ours carries the two non-default lines ML Kit needs, so
      // losing it means the first Mac build quietly takes the dynamic default.
      expect(
        File('ios/Podfile').existsSync(),
        isTrue,
        reason: 'ios/Podfile is authored, not generated - see '
            'docs/dev/Mobile-iOS-Release-Build.md.',
      );
      // Existence is not the point; this line is. Asserting only the file
      // would pass against a Podfile regenerated with the dynamic default,
      // which is the exact regression committing it exists to prevent.
      expect(
        File('ios/Podfile').readAsStringSync(),
        contains(':linkage => :static'),
        reason: 'GoogleMLKit ships static frameworks and does not link under '
            'the default dynamic use_frameworks!.',
      );
    });

    test('the iOS bundle id tracks the Android applicationId', () {
      // One identity across both stores (D-940). Play locks applicationId at
      // first upload and App Store Connect locks the bundle id at the first
      // app record, so drift here becomes permanent on whichever ships second.
      final gradle = File('android/app/build.gradle.kts')
          .readAsLinesSync()
          .where((line) => !line.trimLeft().startsWith('//'))
          .join('\n');
      final applicationId = RegExp(
        r'^\s*applicationId\s*=\s*"([\w.]+)"\s*$',
        multiLine: true,
      ).firstMatch(gradle)?.group(1);
      expect(applicationId, isNotNull);

      final pbxproj =
          File('ios/Runner.xcodeproj/project.pbxproj').readAsStringSync();
      final ids = RegExp(r'PRODUCT_BUNDLE_IDENTIFIER = ([\w.]+);')
          .allMatches(pbxproj)
          .map((match) => match.group(1)!)
          .toSet();

      expect(
        ids,
        equals(<String>{applicationId!, '$applicationId.RunnerTests'}),
        reason: 'every PRODUCT_BUNDLE_IDENTIFIER must be $applicationId or '
            'its .RunnerTests companion, matching the Android id.',
      );
    });

    test('every iOS permission the app uses has a purpose string', () {
      // iOS kills the process the first time a permission is used with no
      // purpose string, and App Review rejects the build before it gets that
      // far. Each key is tied to the plugin that forces it, so dropping the
      // plugin is the only honest way to drop the key.
      final plist = File('ios/Runner/Info.plist').readAsStringSync();
      const required = <String, String>{
        'NSCameraUsageDescription': 'camera + the flutter_zxing scanner',
        'NSPhotoLibraryUsageDescription': 'image_picker profile / ID upload',
        'NSFaceIDUsageDescription': 'local_auth biometric sign-in',
        'NSMicrophoneUsageDescription':
            'the camera plugin links the symbol (ITMS-90683)',
        'NSCalendarsUsageDescription': 'add_2_calendar session reminders',
        'NSCalendarsWriteOnlyAccessUsageDescription':
            'add_2_calendar on iOS 17 and later',
      };
      for (final entry in required.entries) {
        expect(
          plist,
          matches(
            // The tail is a RAW literal, adjacent-concatenated: in a normal
            // Dart string `\s` is not an escape the language knows, so it
            // silently degrades to a literal `s` and the pattern stops being
            // a regex at all. The head has to stay interpolated for the key.
            RegExp(
              '<key>${entry.key}</key>'
              r'\s*<string>\s*\S[^<]*</string>',
            ),
          ),
          reason: '${entry.key} needs a NON-EMPTY purpose string; it is '
              'required by ${entry.value}. An empty <string> satisfies '
              "Apple's static check and shows the user a blank prompt.",
        );
      }

      // The launcher name is a non-default too - `flutter create` wrote
      // "Simf App". Android's bilingual label is already ratcheted (D-699);
      // this is the iOS half, minus the Arabic variant, which needs an Xcode
      // variant group (see the iOS release note, section 2d).
      expect(
        plist,
        contains('<key>CFBundleDisplayName</key>\n\t<string>SIMF</string>'),
        reason: 'the iOS launcher name must be SIMF, not the flutter-create '
            'default "Simf App".',
      );
    });

    test('the Podfile and the Xcode project agree on the iOS floor', () {
      // ML Kit sets the floor and two files carry it. A mismatch surfaces as a
      // link failure during the archive, on a machine nobody on this team owns.
      final podfile = File('ios/Podfile').readAsStringSync();
      final declared =
          RegExp("platform :ios, '([0-9.]+)'").firstMatch(podfile)?.group(1);
      expect(declared, isNotNull, reason: 'no `platform :ios` line in Podfile');

      // The Podfile states the floor TWICE - at the top, and again in the
      // post_install override that forces it onto every pod. Reading only the
      // first lets the two drift, which fails on the Mac and nowhere else.
      final overrides = RegExp(
        r"IPHONEOS_DEPLOYMENT_TARGET'\] = '([0-9.]+)'",
      ).allMatches(podfile).map((match) => match.group(1)!).toSet();
      expect(
        overrides,
        equals(<String>{declared!}),
        reason: "the Podfile's post_install override must match its own "
            '`platform :ios` line ($declared); found $overrides.',
      );

      final pbxproj =
          File('ios/Runner.xcodeproj/project.pbxproj').readAsStringSync();
      final targets = RegExp('IPHONEOS_DEPLOYMENT_TARGET = ([0-9.]+);')
          .allMatches(pbxproj)
          .map((match) => match.group(1)!)
          .toSet();

      expect(
        targets,
        equals(<String>{declared}),
        reason: 'Runner.xcodeproj must use exactly the Podfile floor '
            '($declared); found $targets.',
      );
    });

    test('no iOS signing material is committed', () {
      // The Android half of this rule is above. iOS leaks the same way through
      // its own file types, and a provisioning profile also names the team.
      //
      // `.p8` is the one that matters most and is the easiest to miss: it is
      // the App Store Connect API key, it is an UNENCRYPTED private key, and
      // unlike the .p12 it carries no passphrase of its own. Anyone holding it
      // can upload builds as this team.
      expectNoCommittedSecrets(
        'ios',
        const <String>[
          '.mobileprovision',
          '.p12',
          '.cer',
          '.certSigningRequest',
          '.p8',
          '.pem',
          '.key',
        ],
        reason: 'iOS signing material belongs in Azure Secure Files, never in '
            'the repository (NCA A11-16)',
      );
    });

    test('no hand-dropped credential note sits in a native folder', () {
      // The suffix scans above only catch the formats someone thought of. On
      // 2026-08-22 a plain `android/Key.txt` appeared in the working tree while
      // the upload key was being generated - untracked, matched by no ignore
      // rule and by no suffix here, so one `git add -A` would have committed
      // the keystore password. This catches that shape by CONTENT instead.
      //
      // Only note-shaped files are read: build.gradle.kts legitimately names
      // storePassword where it READS the properties file, and scanning source
      // would fire on that every time.
      const noteTypes = <String>['.txt', '.properties', '.env', '.cfg', '.ini'];
      const markers = <String>['password', 'passphrase', 'secret', 'begin '];

      final offenders = <String>[];
      for (final path in [...tracked('android'), ...tracked('ios')]) {
        if (!noteTypes.any(path.toLowerCase().endsWith)) {
          continue;
        }
        final text = File(path).readAsStringSync().toLowerCase();
        if (markers.any(text.contains)) {
          // The PATH only. Never the contents - this runs in CI logs.
          offenders.add(path);
        }
      }

      expect(
        offenders,
        isEmpty,
        reason: 'a file in a native folder reads like a credential note. Move '
            'it out of the repository: signing material belongs in Azure '
            'Secure Files (NCA A11-16). Contents deliberately not printed; '
            'paths: $offenders',
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

/// Fails if any file under [directory] ends with one of [suffixes].
///
/// Both platform folders ban a list of credential file types and the only
/// thing that differs is the directory and the list, so this is one function
/// rather than the two near-identical scans that were here first.
void expectNoCommittedSecrets(
  String directory,
  List<String> suffixes, {
  required String reason,
}) {
  final files = tracked(directory);

  // Prove the scan can SEE something before concluding it found nothing. If
  // `git ls-files` ever returns empty here - wrong working directory, a
  // detached checkout, git missing from the agent - every secret rule below
  // passes vacuously, which is the one failure mode a security guard must not
  // have.
  expect(
    files,
    isNotEmpty,
    reason: 'git tracks no files under $directory/, so this scan proves '
        'nothing. Fix the scan, do not ignore the empty result.',
  );

  final offenders = files.where((path) => suffixes.any(path.endsWith)).toList();

  expect(offenders, isEmpty, reason: '$reason: $offenders');
}

/// The files git TRACKS under [pathspec], relative to this package root.
///
/// Every secret rule here is about what reaches the repository, not about what
/// sits on the machine running the test. A developer with a configured release
/// signing setup, and the CI agent mid-build, both legitimately have a
/// key.properties on disk - and neither has one in git.
List<String> tracked(String pathspec) {
  final result = Process.runSync('git', <String>['ls-files', '--', pathspec]);
  if (result.exitCode != 0) {
    fail('git ls-files failed for "$pathspec": ${result.stderr}');
  }
  return const LineSplitter()
      .convert(result.stdout as String)
      .map((line) => line.trim())
      .where((line) => line.isNotEmpty)
      .toList();
}
