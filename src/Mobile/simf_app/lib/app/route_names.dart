/// The 41 mockup screens as named go_router routes.
///
/// Names are stable — the router declares them once here; widgets and the
/// Phase 3 `mkp_*` screens reference [RouteNames.xxx] rather than literal
/// strings. The mapping screen-number → route-name follows `Mockup.html`.
class RouteNames {
  RouteNames._();

  // Section 1 — Start & entry (12 screens)
  static const String splash = 'splash';
  static const String onboarding = 'onboarding';
  static const String signIn = 'signIn';
  static const String signUpType = 'signUpType';
  static const String signUpForm = 'signUpForm';
  static const String emailOtp = 'emailOtp';
  static const String signUpVisitor = 'signUpVisitor';
  static const String signUpExhibitor = 'signUpExhibitor';
  static const String terms = 'terms';
  static const String registrationSuccess = 'registrationSuccess';
  static const String registrationStatus = 'registrationStatus';
  static const String guestMode = 'guestMode';

  // Section 2 — Core screens (8 screens)
  static const String home = 'home';
  static const String myArea = 'myArea';
  static const String venueMap = 'venueMap';
  static const String agenda = 'agenda';
  static const String sessionDetail = 'sessionDetail';
  static const String mySeat = 'mySeat';
  static const String speakers = 'speakers';
  static const String speakerProfile = 'speakerProfile';

  // Section 3 — Content & activities (4 screens)
  static const String delegations = 'delegations';
  static const String booths = 'booths';
  static const String sponsors = 'sponsors';
  static const String archive = 'archive';

  // Section 4 — Live & Q&A (4 screens)
  static const String liveBroadcast = 'liveBroadcast';
  static const String sendQuestion = 'sendQuestion';
  static const String requestInterview = 'requestInterview';
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

  // Section 8 — Settings & legal (4 screens)
  static const String accessibility = 'accessibility';
  static const String cybersecurity = 'cybersecurity';
  static const String rate = 'rate';
  static const String more = 'more';

  // Auxiliary auth routes (not numbered in the mockup but in API-001 §12)
  static const String forgotPassword = 'forgotPassword';
  static const String resetPassword = 'resetPassword';
  static const String verifyTotp = 'verifyTotp';
}
