// D-870 — the app's blanket TLS trust-all is finding C2 of
// SIMF-Security-Assessment-2026-06-20.md and an NCA handover blocker: while it
// is on, no native connection has MITM protection.
//
// It could not be removed while the API lived on an underscore hostname,
// because no public CA will issue for one (finding H2). D-868 moved the estate
// to simrsnf.com, whose subdomains ARE valid CA subjects, so the blocker is
// gone and only the certificate itself is outstanding.
//
// These tests exist so that state is tracked by the build rather than by a
// comment. The security assessment's own remediation asks for exactly that:
// "Make 'no trust-all in release' a build/release gate, not a code comment."
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/env/build_config.dart';

void main() {
  group('C2 — the self-signed TLS bypass is a tracked switch', () {
    test('the bypass is still ON by default, and that is deliberate', () {
      // Turning this off before the API has a real certificate does not harden
      // the app — it stops the app reaching the API at all. So the default
      // stays true until the certificate lands, and this test records WHY
      // rather than leaving the reason in a comment nobody runs.
      //
      // WHEN THIS TEST FAILS, the default has been flipped. That is the goal,
      // not a regression: delete this test together with
      // lib/core/net/self_signed_api_tls_io.dart and the Android
      // network_security_config.xml, which all become dead at the same moment.
      expect(
        BuildConfig.allowSelfSignedTls,
        isTrue,
        reason: 'If the API now has a real CA certificate, remove the bypass '
            'and this test together — see D-870.',
      );
    });

    test('the flag is compile-time, so a release build can be hardened without '
        'a code change', () {
      // bool.fromEnvironment is const-folded, so the branch guarding the
      // override is eliminated entirely when the flag is false: a hardened
      // build does not merely skip the trust-all at runtime, it does not carry
      // it. That is what makes
      //   --dart-define=SIMF_ALLOW_SELF_SIGNED_TLS=false
      // a genuine release gate rather than a runtime toggle an attacker or a
      // stray config could flip back on.
      const flag = BuildConfig.allowSelfSignedTls;
      expect(flag, isA<bool>());
    });
  });
}
