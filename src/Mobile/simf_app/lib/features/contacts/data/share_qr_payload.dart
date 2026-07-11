import 'dart:convert';

/// Dual-purpose contact-QR payload (D-737).
///
/// "Share my contact" encodes a vCard so any phone's native camera can offer
/// "add contact" (D-470). But the in-app contact scanner resolves a *share
/// token*, so scanning the app's own QR used to always 404. The fix keeps the
/// vCard and injects the share token as a private `X-SIMF-TOKEN` property — a
/// standard vCard extension (RFC 6350 §6.10) native parsers ignore — so both the
/// native camera and the in-app scanner work from one QR.

const String _tokenProperty = 'X-SIMF-TOKEN:';

/// True when [payload] looks like a vCard (starts with `BEGIN:VCARD`).
bool isVCardPayload(String payload) =>
    payload.trimLeft().toUpperCase().startsWith('BEGIN:VCARD');

/// Returns [vcard] with the share [token] injected as an `X-SIMF-TOKEN` property
/// just before `END:VCARD`. Returns [vcard] unchanged if the token is empty or
/// the input is not a vCard.
String buildShareQrPayload(String vcard, String token) {
  final trimmedToken = token.trim();
  if (trimmedToken.isEmpty) {
    return vcard;
  }
  final upper = vcard.toUpperCase();
  if (!upper.trimLeft().startsWith('BEGIN:VCARD')) {
    return vcard;
  }
  final end = upper.indexOf('END:VCARD');
  if (end < 0) {
    return vcard;
  }
  final eol = vcard.contains('\r\n') ? '\r\n' : '\n';
  return '${vcard.substring(0, end)}$_tokenProperty$trimmedToken$eol'
      '${vcard.substring(end)}';
}

/// Extracts the share token from a vCard [payload], or null if it carries none
/// (an old QR minted before D-737, or a foreign phone's vCard).
String? extractShareToken(String payload) {
  for (final line in LineSplitter.split(payload)) {
    final trimmed = line.trim();
    if (trimmed.toUpperCase().startsWith(_tokenProperty)) {
      return trimmed.substring(_tokenProperty.length).trim();
    }
  }
  return null;
}
