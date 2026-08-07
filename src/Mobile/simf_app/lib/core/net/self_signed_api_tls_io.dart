import 'dart:io';

/// PoC accommodation (D-394, extended D-444): the SIMF API was served over a
/// **self-signed** certificate issued for the server's machine name rather than
/// its public hostname, so the native `HttpClient` rejected every request
/// (untrusted issuer + hostname mismatch). The original blocker was that the
/// old hostname contained an underscore (D-376) and no public CA will issue for
/// one. A host-scoped accept was tried first and did not hold on the device, so
/// per owner request (2026-06-19) this accepts **any** server certificate on
/// native (Android / iOS) builds.
///
/// **That blocker is gone.** D-868 moved the hosts to `api.simrsnf.com` /
/// `cp.simrsnf.com` / `web.simrsnf.com`, which are valid public-CA subjects, so
/// a real certificate is now obtainable (win-acme / Let's Encrypt). Once one is
/// installed this whole file should be deleted — not narrowed — because the
/// system trust store then validates the API on its own.
///
/// SECURITY: until then this stays a deliberate, owner-approved **trust-all**.
/// It removes MITM protection app-wide (web is unaffected — the browser owns
/// TLS there). **It MUST be gone before the production publish / NCA
/// handover** — see `SIMF-Security-Assessment-2026-06-20.md` C2/H2, where it
/// is the open critical that this rename finally unblocks.
void installSelfSignedApiTlsBypass() {
  HttpOverrides.global = _AcceptAnyCertOverrides();
}

class _AcceptAnyCertOverrides extends HttpOverrides {
  @override
  HttpClient createHttpClient(SecurityContext? context) {
    return super.createHttpClient(context)
      ..badCertificateCallback =
          (X509Certificate cert, String host, int port) => true;
  }
}
