import 'dart:io';

import '../env/build_config.dart';

/// PoC accommodation (D-394): the SIMF API host is served over a **self-signed**
/// certificate — its underscore hostname (`simf_api.…`) cannot get a public-CA
/// cert (D-376), and the cert is in fact issued for the server's machine name,
/// not the public hostname — so the native `HttpClient` rejects every request
/// (untrusted issuer + hostname mismatch). Accept a bad certificate **only for
/// the configured API host**; every other host keeps full TLS validation, so
/// the blast radius is exactly that one self-signed backend.
///
/// SECURITY: this trades away MITM protection for the API host. It is a
/// deliberate, owner-requested trade-off while the backend serves a self-signed
/// cert. **Remove it once the server presents a valid certificate for its
/// hostname** (e.g. host renamed to `simf-api.…` with a Let's-Encrypt cert, or
/// a self-signed cert whose SAN is the real hostname).
void installSelfSignedApiTlsBypass() {
  final host = Uri.tryParse(BuildConfig.apiBaseUrl)?.host;
  if (host == null || host.isEmpty) {
    return;
  }
  HttpOverrides.global = _SelfSignedApiHostOverrides(host);
}

class _SelfSignedApiHostOverrides extends HttpOverrides {
  _SelfSignedApiHostOverrides(this._apiHost);

  /// The single host whose (self-signed) certificate is accepted.
  final String _apiHost;

  @override
  HttpClient createHttpClient(SecurityContext? context) {
    return super.createHttpClient(context)
      ..badCertificateCallback =
          (X509Certificate cert, String host, int port) => host == _apiHost;
  }
}
