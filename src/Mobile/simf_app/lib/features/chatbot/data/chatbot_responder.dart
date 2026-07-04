import 'package:flutter_riverpod/flutter_riverpod.dart';

/// The seam that turns a user prompt into an assistant reply.
///
/// There is **no backend chatbot endpoint** (verified — see Page_036 README and
/// DECISIONS_LOG). The default implementation is an honest interim stub that
/// returns a fixed bilingual canned message; it never calls the API or the
/// network. When a real assistant provider is procured server-side, swap the
/// implementation behind [chatbotResponderProvider] — the screen stays.
abstract class ChatbotResponder {
  Future<String> reply(String prompt, {required bool isArabic});
}

/// The interim canned responder: every prompt gets the same bilingual notice.
class CannedChatbotResponder implements ChatbotResponder {
  const CannedChatbotResponder();

  @override
  Future<String> reply(String prompt, {required bool isArabic}) async {
    return isArabic
        ? 'المساعد الذكي قيد التفعيل — سيتوفر الرد التلقائي قريباً.'
        : 'The AI assistant is being connected — automatic replies are coming soon.';
  }
}

/// Overridable seam so widget tests inject a fake responder (no network).
final chatbotResponderProvider = Provider<ChatbotResponder>(
  (ref) => const CannedChatbotResponder(),
);
