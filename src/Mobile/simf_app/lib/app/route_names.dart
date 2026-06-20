/// The mockup screens as named go_router routes.
///
/// Names are stable — the router declares them once here; widgets and the
/// Phase 3 `mkp_*` screens reference [RouteNames.xxx] rather than literal
/// strings. The mapping screen-number → route-name follows `Mockup.html`.
///
/// §9 (D-276): mockup screens 08 (exhibitor self-sign-up) and 39 (cybersecurity
/// policy) are NOT exposed in the app — exhibitors are a Control-Panel concept
/// (D-199), so the app declares 39 numbered routes (the screen numbers keep
/// their mockup values; 08 and 39 are simply absent). Mockup screen 16 was
/// renamed Agenda → Sessions.
class RouteNames {
  RouteNames._();

  // Section 1 — Start & entry (11 screens; 08 exhibitor sign-up removed)
  static const String splash = 'splash';
  static const String onboarding = 'onboarding';
  static const String signIn = 'signIn';
  // signUpType removed (D-332) — the invented account-type screen is not in the
  // mockup; sign-up goes signIn → signUpForm directly.
  static const String signUpForm = 'signUpForm';
  static const String emailOtp = 'emailOtp';
  static const String signUpVisitor = 'signUpVisitor';
  // Page 007‑01 — the interests step, split out of signUpVisitor (D-332). Owns
  // the single profile-upsert save.
  static const String signUpInterests = 'signUpInterests';
  static const String terms = 'terms';
  static const String registrationSuccess = 'registrationSuccess';
  static const String registrationStatus = 'registrationStatus';
  static const String guestMode = 'guestMode';

  // Section 2 — Core screens (8 screens)
  static const String home = 'home';
  static const String myArea = 'myArea';
  static const String venueMap = 'venueMap';
  // §9 (D-276) — mockup screen 16 renamed Agenda → Sessions.
  static const String sessions = 'sessions';
  static const String sessionDetail = 'sessionDetail';
  static const String mySeat = 'mySeat';
  static const String speakers = 'speakers';
  static const String speakerProfile = 'speakerProfile';

  // Section 3 — Content & activities (3 screens; 21 delegations removed — D-277)
  static const String booths = 'booths';
  static const String sponsors = 'sponsors';
  static const String archive = 'archive';

  // Section 4 — Live & Q&A (3 screens; 27 request-interview removed — D-278)
  static const String liveBroadcast = 'liveBroadcast';
  static const String sendQuestion = 'sendQuestion';
  static const String audienceComments = 'audienceComments';

  // Section 5 — Media coverage (3 screens)
  static const String news = 'news';
  static const String gallery = 'gallery';
  static const String mediaPartners = 'mediaPartners';

  // Section 6 — Badge & notifications (2 screens)
  static const String badge = 'badge';
  static const String notifications = 'notifications';

  // Section 7 — Smart features (4 screens)
  static const String aiSummary = 'aiSummary';
  static const String meetPeople = 'meetPeople';
  static const String chatbot = 'chatbot';
  static const String aboutForum = 'aboutForum';

  // Section 8 — Settings & legal (3 screens; 39 cybersecurity removed)
  static const String accessibility = 'accessibility';
  static const String rate = 'rate';
  static const String more = 'more';

  // المزيد hub entries that have no screen yet (D-464; Figma 1129:17224) — they
  // render the ComingSoon placeholder until built. FAQ + presentations have
  // backend (D-218 / D-228); owner chose "parity now, ComingSoon for unbuilt".
  static const String forumGuide = 'forumGuide';
  static const String faq = 'faq';
  static const String sessionPresentations = 'sessionPresentations';
  static const String contactUs = 'contactUs';

  // D-479 (#11 follow-up) — read-only "My meetings" list (additive, reached from
  // My Area; Approved account). Delegation meetings are managed on the CP.
  static const String myMeetings = 'myMeetings';

  // Owner batch (2026-06-21) — entry points whose feature is not designed/built
  // yet, so they render the ComingSoon placeholder (sentinel numbers 200+):
  //   • bilateralMeetings (اللقاءات الثنائية) — home tile, not designed yet (#5).
  //   • savedSessions / savedMeetings — My Area stat tiles, not built yet (#8).
  static const String bilateralMeetings = 'bilateralMeetings';
  static const String savedSessions = 'savedSessions';
  static const String savedMeetings = 'savedMeetings';

  // FDS-014 visitor contact sharing (D-286 API; additive, not mockup-numbered).
  // Reached from More / My Area; all three require an Approved account.
  static const String shareMyContact = 'shareMyContact';
  static const String scanContact = 'scanContact';
  static const String myContacts = 'myContacts';
  // D-426 exhibitor ("Other") lead capture: scan a visitor badge → My Visitors.
  static const String scanVisitor = 'scanVisitor';
  static const String myVisitors = 'myVisitors';
  // Guided face-capture / liveness for the avatar (D-404; additive, reached from
  // My Area; signed-in + Approved).
  static const String identityVerification = 'identityVerification';
  // Moderator (محاور) per-session Q&A desk (D-405; additive, reached from a
  // session's detail; role-gated to Moderator+, server enforces per-session grant).
  static const String sessionModerate = 'sessionModerate';
  // Staff gate-operator scanner (D-406; additive, drawer entry; role-gated to
  // Staff, server enforces the Gates.Operate grant).
  static const String gateScanner = 'gateScanner';

  // Auxiliary auth routes (not numbered in the mockup but in API-001 §12)
  static const String forgotPassword = 'forgotPassword';
  static const String resetPassword = 'resetPassword';

  // Part B (D-430) — badge-QR sign-in / activation: scan the printed-badge QR
  // at login; a passwordless account sets its first password.
  static const String badgeSignIn = 'badgeSignIn';
  static const String badgeActivation = 'badgeActivation';

  /// Visitor email-OTP second factor at sign-in (the app has no TOTP).
  static const String verifyOtp = 'verifyOtp';
}
