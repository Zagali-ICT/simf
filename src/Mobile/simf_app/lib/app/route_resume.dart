/// Decides whether a navigated location should be remembered as the "last
/// screen" to resume to on the next cold start (Page_001 Logic L-5).
///
/// Only signed-in **content** destinations are resumable. Transient bootstrap,
/// auth and onboarding routes (splash, the sign-in / sign-up flow, the email
/// OTP step, terms, registration success / status, guest entry and the
/// auxiliary `/auth/*` routes) are never resumed — dropping the user back into
/// the middle of one of those flows on relaunch would strand them.
library;

/// Location prefixes that must never be resumed into.
const Set<String> _nonResumablePrefixes = <String>{
  '/splash',
  '/onboarding',
  '/sign-in',
  '/sign-up',
  '/terms',
  '/registration',
  '/guest',
  '/auth',
};

/// True when [location] is a content route worth resuming to. A location is
/// resumable unless it is (or sits under) one of the transient prefixes above.
bool isResumableLocation(String location) {
  if (location.isEmpty) {
    return false;
  }
  for (final prefix in _nonResumablePrefixes) {
    if (location == prefix || location.startsWith('$prefix/')) {
      return false;
    }
  }
  return true;
}
