import 'dart:io';

/// PoC accommodation (D-394, extended D-444): the SIMF API host is served over
/// a **self-signed** certificate — its underscore hostname (`simf_api.…`)
/// cannot get a public-CA cert (D-376), and the cert is issued for the server's
/// machine name, not the public hostname — so the native `HttpClient` rejects
/// every request (untrusted issuer + hostname mismatch).
///
/// The earlier host-scoped accept did not hold on the device, so per owner
/// request (2026-06-19) this accepts **any** server certificate on native
/// (Android / iOS) builds.
///
/// SECURITY: this is a deliberate, owner-approved **trust-all** while the
/// backend serves a self-signed certificate. It removes MITM protection
/// app-wide (web is unaffected — the browser owns TLS there). **It MUST be
/// reverted to proper TLS validation — host-scoped + a real CA certificate for
/// the API's hostname — before the production publish / NCA handover.**
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
