import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';

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
    // A server envelope error already carries a localized message; fall back to
    // the generic localized string when it is empty.
    final serverMessage = message.trim();
    return serverMessage.isEmpty ? l10n.errorGenericBody : serverMessage;
  }
}
