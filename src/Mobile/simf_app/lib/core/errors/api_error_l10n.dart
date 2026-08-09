import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Maps an [ApiFailure] to a user-safe message in the active language.
///
/// Envelope (server) failures already carry a bilingual message picked by the
/// app's locale ([ApiFailure.fromEnvelope]). Client-synthesized failures
/// ([ApiErrorCodes.clientNetwork] / `clientTimeout` / `clientMalformedResponse`
/// / `clientCancelled`) carry a raw English developer string, so those are
/// mapped to [AppL10n] here — no screen should render [ApiFailure.message] (or
/// an `AuthFailure`'s wrapped `source.message`) directly, or an Arabic user
/// would see English.
extension ApiFailureL10n on ApiFailure {
  String localizedMessage(AppL10n l10n) {
    switch (code) {
      case ApiErrorCodes.clientNetwork:
      case ApiErrorCodes.clientTimeout:
        return l10n.networkErrorBody;
      case ApiErrorCodes.clientMalformedResponse:
        return l10n.errorServerUnavailable;
      case ApiErrorCodes.clientCancelled:
        return l10n.errorGenericBody;
    }
    // A field-validation failure carries the specific per-field reason(s) in
    // `details`, while the envelope's top-level message is only the generic
    // "one or more fields are invalid". Surface the specific reason(s) so the
    // user knows exactly what to fix — e.g. which password rule failed — rather
    // than a generic message that leaves them stuck.
    if (code == ApiErrorCodes.validationFailed && details.isNotEmpty) {
      final specifics = details
          .map((detail) => detail.localized(l10n.isArabic).trim())
          .where((text) => text.isNotEmpty)
          .toList();
      if (specifics.isNotEmpty) {
        return specifics.join('\n');
      }
    }
    // A server envelope error already carries a localized message; fall back to
    // the generic localized string when it is empty.
    final serverMessage = message.trim();
    return serverMessage.isEmpty ? l10n.errorGenericBody : serverMessage;
  }
}
