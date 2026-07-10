import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';

void main() {
  group('tryParseVersion (D-736)', () {
    test('parses plain and v-prefixed semver, trims whitespace', () {
      expect(tryParseVersion('1.2.0'), isNotNull);
      expect(tryParseVersion('v1.2.0'), isNotNull);
      expect(tryParseVersion('V1.2.0'), isNotNull);
      expect(tryParseVersion(' 1.2.0 '), isNotNull);
    });

    test('null on garbage, blanks and non-semver', () {
      expect(tryParseVersion(null), isNull);
      expect(tryParseVersion(''), isNull);
      expect(tryParseVersion('   '), isNull);
      expect(tryParseVersion('1.2'), isNull);
      expect(tryParseVersion('latest'), isNull);
    });

    test('orders semantically, not lexically', () {
      expect(
        tryParseVersion('1.9.0')!.compareTo(tryParseVersion('1.10.0')!),
        lessThan(0),
      );
    });
  });

  group('usableStoreUrl (D-736 / D-467)', () {
    test('accepts absolute http(s) only', () {
      expect(usableStoreUrl('https://play.google.com/x'), isNotNull);
      expect(usableStoreUrl('http://example.com'), isNotNull);
    });

    test('rejects blanks, relative and non-http schemes', () {
      expect(usableStoreUrl(null), isNull);
      expect(usableStoreUrl(''), isNull);
      expect(usableStoreUrl('   '), isNull);
      expect(usableStoreUrl('play.google.com/x'), isNull);
      expect(usableStoreUrl('javascript:alert(1)'), isNull);
      expect(usableStoreUrl('itms-apps://itunes.apple.com/app/id1'), isNull);
    });
  });

  group('evaluateVersionPolicy (D-736)', () {
    const url = 'https://play.google.com/store/apps/details?id=sa.simf.app';

    AppUpdateStatus eval(
      String installed, {
      String? min,
      String? latest,
      String? storeUrl = url,
    }) =>
        evaluateVersionPolicy(
          installedVersion: installed,
          policy: PlatformVersionPolicy(
            minVersion: min,
            latestVersion: latest,
            storeUrl: storeUrl,
          ),
        );

    test('below the minimum → forced', () {
      expect(eval('1.0.0', min: '1.2.0'), AppUpdateStatus.forced);
    });

    test('below the minimum but NO store URL → up to date (anti-brick)', () {
      expect(
        eval('1.0.0', min: '1.2.0', storeUrl: null),
        AppUpdateStatus.upToDate,
      );
      expect(
        eval('1.0.0', min: '1.2.0', storeUrl: 'not a url'),
        AppUpdateStatus.upToDate,
      );
    });

    test('at/above the minimum, below the latest → optional', () {
      expect(
        eval('1.2.0', min: '1.2.0', latest: '1.4.0'),
        AppUpdateStatus.optional,
      );
    });

    test('at the latest → up to date', () {
      expect(
        eval('1.4.0', min: '1.2.0', latest: '1.4.0'),
        AppUpdateStatus.upToDate,
      );
    });

    test('no policy configured → up to date (fail-open)', () {
      expect(eval('1.0.0'), AppUpdateStatus.upToDate);
    });

    test('an unparseable knob is ignored, not fatal', () {
      expect(eval('1.0.0', min: 'two'), AppUpdateStatus.upToDate);
      expect(
        eval('1.0.0', min: 'two', latest: '1.1.0'),
        AppUpdateStatus.optional,
      );
    });

    test('an unparseable installed version → up to date (fail-open)', () {
      expect(eval('', min: '1.2.0'), AppUpdateStatus.upToDate);
      expect(eval('dev', min: '1.2.0'), AppUpdateStatus.upToDate);
    });

    test('v-prefixed policy values compare correctly', () {
      expect(eval('1.0.0', min: 'v1.2.0'), AppUpdateStatus.forced);
    });

    test('build metadata is ignored — no permanent brick (SemVer §10)', () {
      // The natural pubspec form "X.Y.Z+build" must NOT outrank the same
      // X.Y.Z: the store build is also X.Y.Z, so a "+build" minimum would
      // force the user forever. Same version → up to date.
      expect(eval('1.0.0', min: '1.0.0+42'), AppUpdateStatus.upToDate);
      expect(eval('1.0.0+7', min: '1.0.0+42'), AppUpdateStatus.upToDate);
      // A genuinely higher minimum still forces, build metadata notwithstanding.
      expect(eval('1.0.0', min: '1.1.0+9'), AppUpdateStatus.forced);
      // Build metadata on latest doesn't fabricate an optional update.
      expect(eval('1.0.0', latest: '1.0.0+99'), AppUpdateStatus.upToDate);
    });

    test('pre-release ordering is preserved when build metadata is stripped',
        () {
      // A pre-release is lower precedence than its release (SemVer §11).
      expect(eval('1.0.0-rc+5', min: '1.0.0'), AppUpdateStatus.forced);
    });
  });

  group('tryParseVersion build metadata (D-736)', () {
    test('strips build but keeps major.minor.patch and pre-release', () {
      expect(tryParseVersion('1.2.3+45').toString(), '1.2.3');
      expect(tryParseVersion('1.2.3-rc.1+45').toString(), '1.2.3-rc.1');
      expect(tryParseVersion('1.2.3').toString(), '1.2.3');
    });
  });

  group('AppVersionPolicy.fromJson (D-736 wire shape)', () {
    test('decodes both platforms', () {
      final policy = AppVersionPolicy.fromJson(const <String, dynamic>{
        'android': <String, dynamic>{
          'minVersion': '1.2.0',
          'latestVersion': '1.4.0',
          'storeUrl': 'https://play.google.com/x',
        },
        'ios': <String, dynamic>{'minVersion': null},
      });

      expect(policy.android.minVersion, '1.2.0');
      expect(policy.android.latestVersion, '1.4.0');
      expect(policy.android.storeUrl, 'https://play.google.com/x');
      expect(policy.ios.minVersion, isNull);
      expect(policy.ios.storeUrl, isNull);
    });

    test('missing platforms/fields decode to nulls, never throw', () {
      final policy = AppVersionPolicy.fromJson(const <String, dynamic>{});
      expect(policy.android.minVersion, isNull);
      expect(policy.ios.latestVersion, isNull);
    });
  });
}
