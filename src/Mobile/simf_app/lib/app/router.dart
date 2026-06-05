import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../core/widgets/coming_soon_screen.dart';
import '../features/auth/email_otp_verify_screen.dart';
import '../features/auth/forgot_password_screen.dart';
import '../features/auth/reset_password_screen.dart';
import '../features/auth/sign_in_screen.dart';
import '../features/auth/sign_up_email_verify_screen.dart';
import '../features/auth/sign_up_form_screen.dart';
import '../features/auth/sign_up_type_screen.dart';
import '../features/booths/booths_screen.dart';
import '../features/content/terms_screen.dart';
import '../features/home/home_screen.dart';
import '../features/myarea/my_area_screen.dart';
import '../features/onboarding/onboarding_screen.dart';
import '../features/venuemap/venue_map_screen.dart';
import '../features/profile/sign_up_visitor_screen.dart';
import '../features/registration/registration_status_screen.dart';
import '../features/registration/registration_success_screen.dart';
import '../features/sessions/my_seat_screen.dart';
import '../features/sessions/session_detail_screen.dart';
import '../features/sessions/sessions_screen.dart';
import '../features/speakers/speaker_profile_screen.dart';
import '../features/speakers/speakers_screen.dart';
import '../features/splash/splash_screen.dart';
import 'route_names.dart';
import 'route_resume.dart';

/// Holds the route metadata for one screen: the path, the route name, the
/// mockup screen number, and the Arabic + English label used by the
/// placeholder UI.
class _Route {
  const _Route({
    required this.number,
    required this.name,
    required this.path,
    required this.labelAr,
    required this.labelEn,
  });
  final int number;
  final String name;
  final String path;
  final String labelAr;
  final String labelEn;
}

/// The mockup screens declared once (39 exposed in-app; mockup screen 08
/// exhibitor sign-up + 39 cybersecurity are CP-only / removed — §9 / D-276).
const List<_Route> _routes = <_Route>[
  // Section 1 — Start & entry (11 screens; 08 exhibitor sign-up removed)
  _Route(number: 1, name: RouteNames.splash, path: '/splash', labelAr: 'البداية', labelEn: 'Splash'),
  _Route(number: 2, name: RouteNames.onboarding, path: '/onboarding', labelAr: 'التهيئة', labelEn: 'Onboarding'),
  _Route(number: 3, name: RouteNames.signIn, path: '/sign-in', labelAr: 'تسجيل الدخول', labelEn: 'Sign in'),
  _Route(number: 4, name: RouteNames.signUpType, path: '/sign-up/type', labelAr: 'إنشاء حساب — النوع', labelEn: 'Sign up — type'),
  _Route(number: 5, name: RouteNames.signUpForm, path: '/sign-up', labelAr: 'إنشاء حساب', labelEn: 'Sign up'),
  _Route(number: 6, name: RouteNames.emailOtp, path: '/sign-up/otp', labelAr: 'التحقق بالبريد', labelEn: 'Email verification'),
  _Route(number: 7, name: RouteNames.signUpVisitor, path: '/sign-up/visitor', labelAr: 'إنشاء حساب · زائر', labelEn: 'Sign up — visitor'),
  // Screen 08 (exhibitor self-sign-up) removed — exhibitors are CP-only (D-199 / §9).
  _Route(number: 9, name: RouteNames.terms, path: '/terms', labelAr: 'الشروط والأحكام', labelEn: 'Terms & conditions'),
  _Route(number: 10, name: RouteNames.registrationSuccess, path: '/registration/success', labelAr: 'تم التسجيل بنجاح', labelEn: 'Registration success'),
  _Route(number: 11, name: RouteNames.registrationStatus, path: '/registration/status', labelAr: 'حالة التسجيل', labelEn: 'Registration status'),
  _Route(number: 12, name: RouteNames.guestMode, path: '/guest', labelAr: 'وضع الضيف', labelEn: 'Guest mode'),

  // Section 2 — Core screens (8 screens)
  _Route(number: 13, name: RouteNames.home, path: '/', labelAr: 'الرئيسية', labelEn: 'Home'),
  _Route(number: 14, name: RouteNames.myArea, path: '/my-area', labelAr: 'منطقتي', labelEn: 'My area'),
  _Route(number: 15, name: RouteNames.venueMap, path: '/map', labelAr: 'الخريطة', labelEn: 'Venue map'),
  // §9 (D-276) — mockup screen 16 renamed Agenda → Sessions (route + path + label).
  _Route(number: 16, name: RouteNames.sessions, path: '/sessions', labelAr: 'الجلسات', labelEn: 'Sessions'),
  _Route(number: 17, name: RouteNames.sessionDetail, path: '/sessions/:sessionId', labelAr: 'تفاصيل الجلسة', labelEn: 'Session detail'),
  _Route(number: 18, name: RouteNames.mySeat, path: '/sessions/:sessionId/my-seat', labelAr: 'مقعدي', labelEn: 'My seat'),
  _Route(number: 19, name: RouteNames.speakers, path: '/speakers', labelAr: 'المتحدثون', labelEn: 'Speakers'),
  _Route(number: 20, name: RouteNames.speakerProfile, path: '/speakers/:speakerId', labelAr: 'القبطان البحري', labelEn: 'Speaker profile'),

  // Section 3 — Content & activities (3 screens; 21 delegations removed — D-277)
  _Route(number: 22, name: RouteNames.booths, path: '/booths', labelAr: 'الأجنحة', labelEn: 'Booths'),
  _Route(number: 23, name: RouteNames.sponsors, path: '/sponsors', labelAr: 'الرعاة', labelEn: 'Sponsors'),
  _Route(number: 24, name: RouteNames.archive, path: '/archive', labelAr: 'الأرشيف', labelEn: 'Archive'),

  // Section 4 — Live & Q&A (3 screens; 27 request-interview removed — D-278)
  _Route(number: 25, name: RouteNames.liveBroadcast, path: '/live', labelAr: 'البث المباشر', labelEn: 'Live broadcast'),
  _Route(number: 26, name: RouteNames.sendQuestion, path: '/live/question', labelAr: 'إرسال سؤال', labelEn: 'Send question'),
  _Route(number: 28, name: RouteNames.audienceComments, path: '/live/comments', labelAr: 'تعليقات الجمهور', labelEn: 'Audience comments'),

  // Section 5 — Media coverage (3 screens)
  _Route(number: 29, name: RouteNames.news, path: '/news', labelAr: 'الأخبار', labelEn: 'News'),
  _Route(number: 30, name: RouteNames.gallery, path: '/media', labelAr: 'معرض الصور والفيديوهات', labelEn: 'Media gallery'),
  _Route(number: 31, name: RouteNames.mediaPartners, path: '/media-partners', labelAr: 'الشركاء الإعلاميون', labelEn: 'Media partners'),

  // Section 6 — Badge & notifications (2 screens)
  _Route(number: 32, name: RouteNames.badge, path: '/badge', labelAr: 'بطاقة الدخول · QR', labelEn: 'Entry badge — QR'),
  _Route(number: 33, name: RouteNames.notifications, path: '/notifications', labelAr: 'الإشعارات', labelEn: 'Notifications'),

  // Section 7 — Smart features (4 screens)
  _Route(number: 34, name: RouteNames.aiSummary, path: '/ai-summary', labelAr: 'ملخص الجلسة بالذكاء الاصطناعي', labelEn: 'AI session summary'),
  _Route(number: 35, name: RouteNames.meetPeople, path: '/meet', labelAr: 'قابل أشخاص مثلك', labelEn: 'Meet people like you'),
  _Route(number: 36, name: RouteNames.chatbot, path: '/chatbot', labelAr: 'المساعد الذكي', labelEn: 'AI chatbot'),
  _Route(number: 37, name: RouteNames.aboutForum, path: '/about', labelAr: 'عن الملتقى — المحاور', labelEn: 'About the forum'),

  // Section 8 — Settings & legal (3 screens; 39 cybersecurity removed)
  _Route(number: 38, name: RouteNames.accessibility, path: '/settings/accessibility', labelAr: 'إمكانية الوصول', labelEn: 'Accessibility'),
  // Screen 39 (cybersecurity policy) removed from the app — §9 / D-276.
  _Route(number: 40, name: RouteNames.rate, path: '/rate', labelAr: 'تقييم', labelEn: 'Rate'),
  _Route(number: 41, name: RouteNames.more, path: '/more', labelAr: 'المزيد', labelEn: 'More'),
];

/// Auxiliary auth routes that aren't numbered in the mockup but live in
/// API-001 §12 (forgot/reset password) and the TOTP step (§12.3).
const List<_Route> _auxRoutes = <_Route>[
  _Route(number: 0, name: RouteNames.forgotPassword, path: '/auth/forgot-password', labelAr: 'استعادة كلمة المرور', labelEn: 'Forgot password'),
  _Route(number: 0, name: RouteNames.resetPassword, path: '/auth/reset-password', labelAr: 'تعيين كلمة مرور جديدة', labelEn: 'Reset password'),
  _Route(number: 0, name: RouteNames.verifyOtp, path: '/auth/verify-otp', labelAr: 'رمز التحقق', labelEn: 'Verify OTP'),
];

/// Screen numbers that need a signed-in user (Visitor or higher). Until
/// SIMF-RPM-001 closes (SIMF-MAA-001 OI-3) this list is conservative —
/// Phase 1 only gates the few obvious cases. Phase 2 / Phase 3 may extend.
const Set<int> _authenticatedRoutes = <int>{
  7, // Sign up — visitor (profile completion; AUTH-only, Page_007 L-1)
  10, // Registration success (signed-in, pending; Page_010)
  11, // Registration status (signed-in, not-yet-approved gate; Page_011 L-1)
  14, // My area
  18, // My seat
  26, // Send question
  32, // Badge / QR
  33, // Notifications
  35, // Meet people
};

/// Builds the go_router instance.
///
/// The redirect logic implements the auth gate (SIMF-MAA-001 §8): a request
/// for a protected route while signed out gets redirected to sign-in. The
/// router refreshes on every auth-state change ([refreshListenable]) so the
/// gate re-runs when the cold-start restore resolves or the session ends.
GoRouter buildRouter(Ref ref) {
  final prefs = ref.read(simfPrefsStorageProvider);
  final authRefresh = _AuthRefreshNotifier(ref);
  // The last location written to prefs, so the same value is not rewritten on
  // every redirect pass.
  String? lastRecorded;

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: authRefresh,
    redirect: (context, state) {
      final authState = ref.read(authControllerProvider);
      final goingTo = state.matchedLocation;
      final isSignedIn = authState is AuthStateSignedIn;

      // Remember the last signed-in content location so the next cold start can
      // resume to it (Page_001 Logic L-5). The splash owns the read.
      if (isSignedIn &&
          isResumableLocation(goingTo) &&
          goingTo != lastRecorded) {
        lastRecorded = goingTo;
        unawaited(prefs.setString(StorageKeys.lastRoute, goingTo));
      }

      return redirectDecision(
        isInitial: authState is AuthStateInitial,
        isSignedIn: isSignedIn,
        goingTo: goingTo,
        fullPath: state.fullPath,
      );
    },
    routes: <RouteBase>[
      for (final r in _routes)
        GoRoute(
          name: r.name,
          path: r.path,
          builder: (context, state) {
            // Page 001 (splash) is a real screen; every other route still
            // renders the ComingSoonScreen placeholder until it is built. The
            // state + API are wired through the packages; the visuals are
            // explicitly a placeholder (SIMF-MAA-001 §12.1).
            if (r.name == RouteNames.splash) {
              return const SplashScreen();
            }
            if (r.name == RouteNames.onboarding) {
              return const OnboardingScreen();
            }
            if (r.name == RouteNames.signIn) {
              return const SignInScreen();
            }
            if (r.name == RouteNames.signUpType) {
              return const SignUpTypeScreen();
            }
            if (r.name == RouteNames.signUpForm) {
              return const SignUpFormScreen();
            }
            if (r.name == RouteNames.emailOtp) {
              return SignUpEmailVerifyScreen(
                email: state.uri.queryParameters['email'] ?? '',
              );
            }
            if (r.name == RouteNames.signUpVisitor) {
              return const SignUpVisitorScreen();
            }
            if (r.name == RouteNames.terms) {
              // `?consent=1` shows the in-flow accept gate; standalone reads omit it.
              return TermsScreen(
                requireConsent:
                    state.uri.queryParameters['consent'] == '1',
              );
            }
            if (r.name == RouteNames.registrationSuccess) {
              return const RegistrationSuccessScreen();
            }
            if (r.name == RouteNames.registrationStatus) {
              return const RegistrationStatusScreen();
            }
            if (r.name == RouteNames.home) {
              return const HomeScreen();
            }
            if (r.name == RouteNames.myArea) {
              return const MyAreaScreen();
            }
            if (r.name == RouteNames.venueMap) {
              return const VenueMapScreen();
            }
            if (r.name == RouteNames.sessions) {
              return const SessionsScreen();
            }
            if (r.name == RouteNames.sessionDetail) {
              return SessionDetailScreen(
                sessionId: state.pathParameters['sessionId'] ?? '',
              );
            }
            if (r.name == RouteNames.mySeat) {
              return MySeatScreen(
                sessionId: state.pathParameters['sessionId'] ?? '',
              );
            }
            if (r.name == RouteNames.speakers) {
              return const SpeakersScreen();
            }
            if (r.name == RouteNames.speakerProfile) {
              return SpeakerProfileScreen(
                speakerId: state.pathParameters['speakerId'] ?? '',
              );
            }
            if (r.name == RouteNames.booths) {
              return const BoothsScreen();
            }
            return ComingSoonScreen(
              screenNumber: r.number,
              screenLabelAr: r.labelAr,
              screenLabelEn: r.labelEn,
            );
          },
        ),
      for (final r in _auxRoutes)
        GoRoute(
          name: r.name,
          path: r.path,
          builder: (context, state) {
            if (r.name == RouteNames.forgotPassword) {
              return const ForgotPasswordScreen();
            }
            if (r.name == RouteNames.resetPassword) {
              return ResetPasswordScreen(
                email: state.uri.queryParameters['email'] ?? '',
              );
            }
            if (r.name == RouteNames.verifyOtp) {
              return const EmailOtpVerifyScreen();
            }
            return ComingSoonScreen(
              screenNumber: r.number,
              screenLabelAr: r.labelAr,
              screenLabelEn: r.labelEn,
            );
          },
        ),
    ],
  );
}

/// Bridges the Riverpod auth state to go_router's [Listenable]-based refresh:
/// every change to [authControllerProvider] re-runs the router's redirect, so
/// the cold-start restore resolving (and a later sign-out) is reflected in the
/// gate without a manual navigation.
class _AuthRefreshNotifier extends ChangeNotifier {
  _AuthRefreshNotifier(Ref ref) {
    ref.listen<AuthState>(authControllerProvider, (_, __) {
      notifyListeners();
    });
  }
}

/// The mockup screen number for a matched route **pattern** — go_router's
/// `state.fullPath` (e.g. `/sessions/:sessionId/my-seat`), or null. Matching the
/// pattern exactly (not a prefix of the concrete location) is what stops a
/// sub-route like My-seat (#18) from being shadowed by its parent
/// `/sessions/:sessionId` (#17) and silently losing its auth gate.
int? routeNumberForPath(String? fullPath) {
  if (fullPath == null) {
    return null;
  }
  for (final r in _routes) {
    if (r.path == fullPath) {
      return r.number;
    }
  }
  return null;
}

/// Whether the matched route pattern needs a signed-in user (the auth gate).
bool routePathRequiresAuth(String? fullPath) =>
    _authenticatedRoutes.contains(routeNumberForPath(fullPath));

/// The pure auth-gate redirect decision (testable in isolation, like
/// [routePathRequiresAuth]). Returns the path to redirect to, or null to allow
/// the navigation. [goingTo] is the matched location; [fullPath] is the matched
/// route *pattern*.
///
/// A signed-in user landing on `/sign-in` is intentionally **not** bounced here
/// (D-295). Post-sign-in routing belongs to `SignInScreen._routeAfterSignIn`,
/// which sends a profile-incomplete visitor to the profile screen (Page_007). A
/// blunt `/sign-in -> /` redirect fired on the auth-state change, disposed the
/// SignInScreen before its post-sign-in router could run, and stranded
/// incomplete profiles on Home.
String? redirectDecision({
  required bool isInitial,
  required bool isSignedIn,
  required String goingTo,
  required String? fullPath,
}) {
  // Hold *protected* routes on the splash while the cold-start restore resolves
  // (Page_001 L-5 / D-295); the splash routes itself out to a public entry
  // screen once auth resolves, so a public route is never pinned here —
  // otherwise a stalled restore would strand the user on the splash (L-6).
  if (isInitial && goingTo != '/splash' && routePathRequiresAuth(fullPath)) {
    return '/splash';
  }
  // The auth gate (SIMF-MAA-001 §8): a protected route while signed out goes to
  // the sign-in entry.
  if (routePathRequiresAuth(fullPath) && !isSignedIn) {
    return '/sign-in';
  }
  return null;
}

final routerProvider = Provider<GoRouter>(buildRouter);
