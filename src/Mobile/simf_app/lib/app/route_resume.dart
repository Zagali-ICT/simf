/// Decides whether a navigated location should be remembered as the "last
/// screen" to resume to on the next cold start (Page_001 Logic L-5).
///
/// Only signed-in **content** destinations are resumable. Transient bootstrap,
/// auth and onboarding routes (splash, the sign-in / sign-up flow, the email
/// OTP step, terms, registration success / status, guest entry and the
/// auxiliary `/auth/*` routes) are never resumed — dropping the user back into
/// the middle of one of those flows on relaunch would strand them.
///
/// The live-camera **action** routes (the contact / visitor / gate scanners and
/// the share-my-contact QR) are excluded for the same reason (D-426): they are
/// one-shot actions, not destinations, and cold-starting straight into a camera
/// screen — where on some devices the camera surface swallows on-screen taps —
/// strands the user on launch.
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
  // Transient one-shot action routes (cameras / share QR) — D-426.
  '/contacts/scan',
  '/contacts/share',
  '/exhibitor/scan',
  '/gates/scan',
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
