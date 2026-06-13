/// No-op on web — the browser owns TLS, so a self-signed API cannot be
/// bypassed from a web build (and the web channel is dev/PoC-only anyway).
/// The native implementation lives in `self_signed_api_tls_io.dart`, selected
/// by the conditional import in `main.dart`.
void installSelfSignedApiTlsBypass() {}
