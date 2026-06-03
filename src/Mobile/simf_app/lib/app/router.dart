import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../core/widgets/coming_soon_screen.dart';
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

/// The 41 mockup screens declared once.
const List<_Route> _routes = <_Route>[
  // Section 1 — Start & entry (12 screens)
  _Route(number: 1, name: RouteNames.splash, path: '/splash', labelAr: 'البداية', labelEn: 'Splash'),
  _Route(number: 2, name: RouteNames.onboarding, path: '/onboarding', labelAr: 'التهيئة', labelEn: 'Onboarding'),
  _Route(number: 3, name: RouteNames.signIn, path: '/sign-in', labelAr: 'تسجيل الدخول', labelEn: 'Sign in'),
  _Route(number: 4, name: RouteNames.signUpType, path: '/sign-up/type', labelAr: 'إنشاء حساب — النوع', labelEn: 'Sign up — type'),
  _Route(number: 5, name: RouteNames.signUpForm, path: '/sign-up', labelAr: 'إنشاء حساب', labelEn: 'Sign up'),
  _Route(number: 6, name: RouteNames.emailOtp, path: '/sign-up/otp', labelAr: 'التحقق بالبريد', labelEn: 'Email verification'),
  _Route(number: 7, name: RouteNames.signUpVisitor, path: '/sign-up/visitor', labelAr: 'إنشاء حساب · زائر', labelEn: 'Sign up — visitor'),
  _Route(number: 8, name: RouteNames.signUpExhibitor, path: '/sign-up/exhibitor', labelAr: 'إنشاء حساب · جهة عارضة', labelEn: 'Sign up — exhibitor'),
  _Route(number: 9, name: RouteNames.terms, path: '/terms', labelAr: 'الشروط والأحكام', labelEn: 'Terms & conditions'),
  _Route(number: 10, name: RouteNames.registrationSuccess, path: '/registration/success', labelAr: 'تم التسجيل بنجاح', labelEn: 'Registration success'),
  _Route(number: 11, name: RouteNames.registrationStatus, path: '/registration/status', labelAr: 'حالة التسجيل', labelEn: 'Registration status'),
  _Route(number: 12, name: RouteNames.guestMode, path: '/guest', labelAr: 'وضع الضيف', labelEn: 'Guest mode'),

  // Section 2 — Core screens (8 screens)
  _Route(number: 13, name: RouteNames.home, path: '/', labelAr: 'الرئيسية', labelEn: 'Home'),
  _Route(number: 14, name: RouteNames.myArea, path: '/my-area', labelAr: 'منطقتي', labelEn: 'My area'),
  _Route(number: 15, name: RouteNames.venueMap, path: '/map', labelAr: 'الخريطة', labelEn: 'Venue map'),
  _Route(number: 16, name: RouteNames.agenda, path: '/agenda', labelAr: 'الأجندة', labelEn: 'Agenda'),
  _Route(number: 17, name: RouteNames.sessionDetail, path: '/agenda/:sessionId', labelAr: 'تفاصيل الجلسة', labelEn: 'Session detail'),
  _Route(number: 18, name: RouteNames.mySeat, path: '/agenda/:sessionId/my-seat', labelAr: 'مقعدي', labelEn: 'My seat'),
  _Route(number: 19, name: RouteNames.speakers, path: '/speakers', labelAr: 'المتحدثون', labelEn: 'Speakers'),
  _Route(number: 20, name: RouteNames.speakerProfile, path: '/speakers/:speakerId', labelAr: 'القبطان البحري', labelEn: 'Speaker profile'),

  // Section 3 — Content & activities (4 screens)
  _Route(number: 21, name: RouteNames.delegations, path: '/delegations', labelAr: 'الوفود', labelEn: 'Delegations'),
  _Route(number: 22, name: RouteNames.booths, path: '/booths', labelAr: 'الأجنحة', labelEn: 'Booths'),
  _Route(number: 23, name: RouteNames.sponsors, path: '/sponsors', labelAr: 'الرعاة', labelEn: 'Sponsors'),
  _Route(number: 24, name: RouteNames.archive, path: '/archive', labelAr: 'الأرشيف', labelEn: 'Archive'),

  // Section 4 — Live & Q&A (4 screens)
  _Route(number: 25, name: RouteNames.liveBroadcast, path: '/live', labelAr: 'البث المباشر', labelEn: 'Live broadcast'),
  _Route(number: 26, name: RouteNames.sendQuestion, path: '/live/question', labelAr: 'إرسال سؤال', labelEn: 'Send question'),
  _Route(number: 27, name: RouteNames.requestInterview, path: '/live/interview', labelAr: 'طلب مقابلة', labelEn: 'Request interview'),
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

  // Section 8 — Settings & legal (4 screens)
  _Route(number: 38, name: RouteNames.accessibility, path: '/settings/accessibility', labelAr: 'إمكانية الوصول', labelEn: 'Accessibility'),
  _Route(number: 39, name: RouteNames.cybersecurity, path: '/legal/cybersecurity', labelAr: 'الأمن السيبراني', labelEn: 'Cybersecurity policy'),
  _Route(number: 40, name: RouteNames.rate, path: '/rate', labelAr: 'تقييم', labelEn: 'Rate'),
  _Route(number: 41, name: RouteNames.more, path: '/more', labelAr: 'المزيد', labelEn: 'More'),
];

/// Auxiliary auth routes that aren't numbered in the mockup but live in
/// API-001 §12 (forgot/reset password) and the TOTP step (§12.3).
const List<_Route> _auxRoutes = <_Route>[
  _Route(number: 0, name: RouteNames.forgotPassword, path: '/auth/forgot-password', labelAr: 'استعادة كلمة المرور', labelEn: 'Forgot password'),
  _Route(number: 0, name: RouteNames.resetPassword, path: '/auth/reset-password', labelAr: 'تعيين كلمة مرور جديدة', labelEn: 'Reset password'),
  _Route(number: 0, name: RouteNames.verifyTotp, path: '/auth/verify-totp', labelAr: 'التحقق الثنائي', labelEn: 'Verify TOTP'),
];

/// Screen numbers that need a signed-in user (Visitor or higher). Until
/// SIMF-RPM-001 closes (SIMF-MAA-001 OI-3) this list is conservative —
/// Phase 1 only gates the few obvious cases. Phase 2 / Phase 3 may extend.
const Set<int> _authenticatedRoutes = <int>{
  14, // My area
  18, // My seat
  26, // Send question
  27, // Request interview
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

      // While the cold-start restore is still running, hold on the splash; the
      // splash itself routes out once auth resolves (Page_001 Logic L-5).
      if (authState is AuthStateInitial && goingTo != '/splash') {
        return '/splash';
      }

      if (routePathRequiresAuth(state.fullPath) && !isSignedIn) {
        return '/sign-in';
      }

      // A signed-in user has no business on the sign-in screen. (The splash is
      // left to route itself out, so a resume target is never clobbered here.)
      if (isSignedIn && goingTo == '/sign-in') {
        return '/';
      }

      return null;
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
          builder: (context, state) => ComingSoonScreen(
            screenNumber: r.number,
            screenLabelAr: r.labelAr,
            screenLabelEn: r.labelEn,
          ),
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
/// `state.fullPath` (e.g. `/agenda/:sessionId/my-seat`), or null. Matching the
/// pattern exactly (not a prefix of the concrete location) is what stops a
/// sub-route like My-seat (#18) from being shadowed by its parent
/// `/agenda/:sessionId` (#17) and silently losing its auth gate.
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

final routerProvider = Provider<GoRouter>(buildRouter);
