package com.simrsnf.simf

import android.os.Bundle
import android.view.WindowManager
import androidx.activity.enableEdgeToEdge
import io.flutter.embedding.android.FlutterFragmentActivity

// FlutterFragmentActivity (not FlutterActivity) is REQUIRED by local_auth: its
// BiometricPrompt attaches to a FragmentActivity. With a plain FlutterActivity
// authenticate() throws no_fragment_activity and Face-ID sign-in silently fails.
class MainActivity : FlutterFragmentActivity() {
    // A11-6 (NCA Secure App-Dev Standard) — set FLAG_SECURE for the whole app so
    // screenshots, screen recording and the app-switcher snapshot are blocked.
    // The badge QR, profile and OTP screens all carry protected info, so the
    // app-wide flag is the simplest robust defence (no per-screen channel).
    override fun onCreate(savedInstanceState: Bundle?) {
        // Call the AndroidX Activity API before Flutter creates its content so
        // Android 10-14 use the same inset path Android 15+ enforces.
        enableEdgeToEdge()
        window.setFlags(
            WindowManager.LayoutParams.FLAG_SECURE,
            WindowManager.LayoutParams.FLAG_SECURE,
        )
        super.onCreate(savedInstanceState)
    }
}
