/// Animation and timing durations, named by what they are for.
///
/// These are NOT design tokens in the `SimfTokens` sense: they are behaviour,
/// not appearance. A colour change and an animation-length change are different
/// decisions made by different people, so they live in different files.
///
/// Each name says the ROLE, never the value. `carouselSlide` survives the day
/// someone decides 450ms feels slow; `duration450` would not.
abstract final class MotionDurations {
  /// A page dot fading between active and inactive.
  static const Duration dotFade = Duration(milliseconds: 250);

  /// The onboarding step dots, marginally quicker than [dotFade] so they settle
  /// before the page itself finishes moving.
  static const Duration onboardingDotFade = Duration(milliseconds: 200);

  /// A carousel or hero banner animating to the next slide.
  static const Duration carouselSlide = Duration(milliseconds: 450);

  /// The QR scanner's sweep line crossing the viewfinder once.
  static const Duration scannerSweep = Duration(milliseconds: 2200);

  /// How long the splash brand mark holds before the app routes on.
  static const Duration splashHold = Duration(milliseconds: 1200);

  /// Typing settle time before a lookup fires, so a search is not sent per
  /// keystroke.
  static const Duration searchDebounce = Duration(milliseconds: 350);
}

/// Deadlines after which the app stops waiting and shows a fallback.
///
/// Separate from [MotionDurations] because these are policy: exceeding one is a
/// failure path, not a visual effect.
abstract final class TimeoutPolicy {
  /// The OS biometric prompt. Past this the app falls back to password entry.
  static const Duration biometricPrompt = Duration(seconds: 8);

  /// A single QR scan attempt before the manual-entry fallback is offered.
  static const Duration qrScan = Duration(seconds: 8);

  /// The longest the splash screen blocks on startup work before continuing
  /// without it.
  static const Duration splashStartup = Duration(seconds: 8);
}
