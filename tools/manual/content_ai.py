"""The knowledge and AI chapter: the FAQ, the prompts, and what runs them.

Five pages, one of which is plain content (the FAQ) and four of which control a
system that costs money per call and can send text to a third party. The chapter
is organised around the two questions an operator has to be able to answer
before touching any of them: what is running right now, and how do I turn one
off.

The second question has a wrong answer that looks right - leaving a prompt on
the offline Echo provider - and that is the trap this chapter leads to. Every
routing rule, quota and failure code below was read out of the routing helper,
the rate-limit options and the provider adapters.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_knowledge_ai():
    return {
        "id": "knowledge-ai",
        "title": t("The FAQ and the AI features",
                   "الأسئلة الشائعة وخصائص الذكاء الاصطناعي"),
        "blocks": [
            p("The FAQ is ordinary content. The other four pages are the "
              "controls for a set of features that call a language model, and "
              "they are read-and-configure pages rather than pages you work on "
              "daily. The order to learn them in is: what the FAQ feeds, then "
              "what is running, then how to change it, then how to see what it "
              "did.",
              "الأسئلة الشائعة محتوى عادي. أما الصفحات الأربع الأخرى فهي أدوات "
              "التحكم في مجموعة خصائص تستدعي نموذجًا لغويًّا، وهي صفحات قراءة "
              "وتهيئة لا صفحات عمل يومي. وترتيب تعلّمها: ما تغذّيه الأسئلة "
              "الشائعة، ثم ما الذي يعمل الآن، ثم كيف يُغيَّر، ثم كيف يُرى ما "
              "فعله."),

            h2("The FAQ has three readers", "للأسئلة الشائعة ثلاثة قرّاء"),
            p("Entries are grouped, ordered inside their group, and only the "
              "active ones are published. What is worth knowing is who reads "
              "them.",
              "المدخلات مجمَّعة، ومرتَّبة داخل مجموعتها، ولا يُنشر إلا المفعَّل "
              "منها. والجدير بالمعرفة هو من يقرؤها."),
            bullets(
                ["**The app's questions screen** reads them directly and shows "
                 "them as they are.",
                 "**The website's chat bubble** does not: it asks the AI to "
                 "answer the visitor's question, and the answer is composed "
                 "rather than quoted. A blank answer is reported as a failure "
                 "instead of appearing as an empty bubble.",
                 "**The in-app assistant** is given the whole FAQ, the programme "
                 "sessions and the booth list as its grounding, so it can only "
                 "talk about real, active data. That grounding is rebuilt about "
                 "once a minute: a FAQ edit reaches the assistant when the old "
                 "copy expires, not the instant it is saved."],
                ["**شاشة الأسئلة في التطبيق** تقرؤها مباشرة وتعرضها كما هي.",
                 "**فقاعة المحادثة في الموقع** لا تفعل ذلك: بل تطلب من الذكاء "
                 "الاصطناعي أن يجيب سؤال الزائر، فتكون الإجابة مؤلَّفة لا "
                 "منقولة. والإجابة الفارغة يُبلَّغ عنها كإخفاق بدل أن تظهر فقاعة "
                 "خالية.",
                 "**المساعد داخل التطبيق** يُعطى الأسئلة الشائعة كاملةً وجلسات "
                 "البرنامج وقائمة الأجنحة أساسًا له، فلا يستطيع الحديث إلا عن "
                 "بيانات حقيقية مفعَّلة. ويُعاد بناء ذلك الأساس كل دقيقة تقريبًا: "
                 "فتعديل سؤال شائع يصل إلى المساعد حين تنتهي صلاحية النسخة "
                 "القديمة، لا لحظة حفظه."]),
            figure("cp-admin-faq-default",
                   "The FAQ. Groups and their entries, in the order the app "
                   "shows them.",
                   "الأسئلة الشائعة. المجموعات ومدخلاتها بالترتيب الذي يعرضه "
                   "التطبيق."),

            h2("What is running: services and prompts",
               "ما الذي يعمل: الخدمات والموجّهات"),
            p("A service is one AI feature — the question filter, the FAQ "
              "answering, the assistant, translation, live translation, live "
              "sign language, session summaries and the Control Panel's own help "
              "assistant. The services page is read-only: it collects, for every "
              "feature, which prompt is active on it, which provider and model "
              "that prompt uses, and whether that provider is hosted outside the "
              "organisation. It exists so the question \"what runs the "
              "translation, and where?\" has one place to be answered. Nothing is "
              "changed there.",
              "الخدمة هي خاصية ذكاء اصطناعي واحدة — مرشّح الأسئلة، والإجابة عن "
              "الأسئلة الشائعة، والمساعد، والترجمة، والترجمة الفورية، ولغة "
              "الإشارة الفورية، وملخصات الجلسات، ومساعد لوحة التحكم نفسه. وصفحة "
              "الخدمات للقراءة فقط: إذ تجمع لكل خاصية الموجّه المفعَّل عليها، "
              "والمزوّد والنموذج الذي يستخدمه ذلك الموجّه، وهل ذلك المزوّد مستضاف "
              "خارج الجهة. وهي موجودة ليكون لسؤال «ما الذي يشغّل الترجمة، وأين؟» "
              "موضع واحد يُجاب فيه. ولا يُغيَّر فيها شيء."),
            note("The page warns when a sensitive feature is routed to a provider "
                 "that is definitely in the cloud. Sensitive here means the four "
                 "that carry the audience's own words or a session's contents: "
                 "session summaries, the assistant, live translation and live "
                 "sign language. One provider is shown as depending on its "
                 "endpoint rather than flagged, because it can be pointed at an "
                 "internal address and the row alone cannot tell.",
                 "تحذّر الصفحة حين تُوجَّه خاصية حساسة إلى مزوّد في السحابة قطعًا. "
                 "والحساس هنا هو الخصائص الأربع التي تحمل كلام الجمهور نفسه أو "
                 "محتوى جلسة: ملخصات الجلسات، والمساعد، والترجمة الفورية، ولغة "
                 "الإشارة الفورية. ويُعرض أحد المزودين على أنه رهنٌ بعنوان نقطته "
                 "بدل تعليمه بتحذير، لأنه يمكن توجيهه إلى عنوان داخلي ولا يستطيع "
                 "السجل وحده أن يعرف."),
            figure("cp-admin-ai-services-default",
                   "The services view: one row per AI feature, with the prompt, "
                   "provider and model actually serving it.",
                   "عرض الخدمات: سجل لكل خاصية ذكاء اصطناعي، ومعه الموجّه "
                   "والمزوّد والنموذج الذي يخدمها فعلًا."),

            h3("A prompt is the whole unit of behaviour",
               "الموجّه هو وحدة السلوك كاملةً"),
            p("Everything about how a feature behaves lives on its prompt: the "
              "key that callers reference, which feature it serves, its provider "
              "and model, the system prompt, the user template whose placeholders "
              "are filled from the request, the temperature, the output ceiling "
              "and whether it is active. Saving increments its version. Editing a "
              "prompt changes what the system does immediately — there is no "
              "deployment and no restart.",
              "كل ما يتعلق بسلوك الخاصية يقيم في موجّهها: المفتاح الذي يشير إليه "
              "المستدعون، والخاصية التي يخدمها، ومزوّده ونموذجه، وموجّه النظام، "
              "وقالب المستخدم الذي تُملأ مواضعه من الطلب، ودرجة التنويع، وسقف "
              "المخرجات، وهل هو مفعَّل. والحفظ يزيد رقم إصداره. وتحرير الموجّه "
              "يغيّر ما يفعله النظام فورًا — بلا نشر ولا إعادة تشغيل."),
            note("More than one prompt may serve the same feature; the caller "
                 "names the key it wants. That is how two wordings are compared "
                 "against each other without either one being deleted.",
                 "قد يخدم أكثر من موجّه الخاصية نفسها؛ ويسمّي المستدعي المفتاح "
                 "الذي يريده. وهكذا تُقارن صياغتان إحداهما بالأخرى دون حذف أيٍّ "
                 "منهما."),
            figure("cp-admin-ai-prompts-default",
                   "The prompt catalogue. The key, the feature, the provider and "
                   "the active flag are the four columns that decide behaviour.",
                   "فهرس الموجّهات. المفتاح والخاصية والمزوّد وراية التفعيل هي "
                   "الأعمدة الأربعة التي تحدد السلوك."),

            h2("Turning a feature off — and the way that does not work",
               "إيقاف خاصية — والطريقة التي لا تعمل"),
            p("Every prompt ships pinned to Echo, an offline provider that makes "
              "no outbound call at all: it returns the text it was given, behind "
              "a marker naming itself. That is what makes a fresh installation "
              "safe to run with no provider account of any kind.",
              "يُشحن كل موجّه مثبَّتًا على Echo، وهو مزوّد غير متصل لا يجري أي "
              "استدعاء خارجي إطلاقًا: بل يعيد النص الذي أُعطي له خلف علامة تسمّي "
              "نفسها. وهذا ما يجعل التثبيت الجديد آمنًا للتشغيل بلا حساب مزوّد من "
              "أي نوع."),
            note("**Echo is not an off switch once a real provider is "
                 "configured.** Setting the estate's default provider redirects "
                 "every prompt still pinned to Echo onto that provider — which is "
                 "exactly what makes it the single setting that turns AI on, and "
                 "exactly why it is not a mute. Only a prompt pinned to a named "
                 "provider is left alone. To stop a feature, deactivate its "
                 "prompt: it then answers with a plain \"this feature is "
                 "disabled\" and calls nothing.",
                 "**ليس Echo مفتاح إيقاف بعد تهيئة مزوّد حقيقي.** فضبط المزوّد "
                 "الافتراضي للمنشأة يحوّل كل موجّه ما زال مثبَّتًا على Echo إلى ذلك "
                 "المزوّد — وهو بالضبط ما يجعله الإعداد الوحيد الذي يشغّل الذكاء "
                 "الاصطناعي، وبالضبط سبب أنه ليس كتمًا. ولا يُترك على حاله إلا "
                 "الموجّه المثبَّت على مزوّد مسمّى. ولإيقاف خاصية، عطّل موجّهها: "
                 "فيجيب حينئذ بأن هذه الخاصية معطَّلة ولا يستدعي شيئًا."),
            p("After a real provider is wired, the check that it took effect is "
              "that answers stop arriving with the Echo marker in front of them. "
              "A session summary drafted while Echo was serving carries a "
              "do-not-publish sentinel, and the publishing step looks for it "
              "again before approving — so a stub draft cannot reach an audience "
              "even if somebody forgets which provider produced it.",
              "وبعد توصيل مزوّد حقيقي، فإن الدليل على أن ذلك أخذ مفعوله هو أن "
              "تكفّ الإجابات عن الوصول وعلامة Echo أمامها. والملخص الذي صيغ بينما "
              "كان Echo يخدم يحمل علامة «لا يُنشر»، وتبحث خطوة النشر عنها مجددًا "
              "قبل الاعتماد — فلا تصل مسودة صورية إلى جمهور ولو نسي أحدهم أي "
              "مزوّد أنتجها."),

            h3("The two ways a call fails",
               "الطريقتان اللتان يفشل بهما الاستدعاء"),
            table(
                ["What happened", "What the caller sees", "Where to look"],
                ["ما الذي حدث", "ماذا يرى المستدعي", "أين تنظر"],
                [
                    ["The provider is chosen but has no key",
                     "The service is unavailable, refused before any call goes "
                     "out",
                     "The provider's key in the deployment configuration — see "
                     "the configuration chapter"],
                    ["The provider is not wired up at all",
                     "The service is unavailable, and a failed call is recorded "
                     "in the invocations log first",
                     "The invocations page, which will hold the row with its "
                     "error code"],
                    ["The prompt is deactivated",
                     "The feature reports itself disabled", "The prompt "
                     "catalogue"],
                ],
                [
                    ["المزوّد مختار ولا مفتاح له",
                     "الخدمة غير متاحة، ويُرفض الطلب قبل خروج أي استدعاء",
                     "مفتاح المزوّد في تهيئة النشر — انظر فصل التهيئة"],
                    ["المزوّد غير موصول أصلًا",
                     "الخدمة غير متاحة، ويُسجَّل الاستدعاء الفاشل في سجل "
                     "الاستدعاءات أولًا",
                     "صفحة الاستدعاءات، وفيها السجل مع رمز خطئه"],
                    ["الموجّه معطَّل", "تُبلِّغ الخاصية بأنها معطَّلة", "فهرس "
                     "الموجّهات"],
                ]),

            h2("Seeing what it did", "رؤية ما فعله"),
            p("Every AI call writes one row, whether it succeeded or failed: "
              "which prompt and feature, which provider and model, the inputs "
              "after personal data and secrets have been stripped out, the output "
              "text, how many tokens went in and came out, how long it took, an "
              "error code when there was one, and who the caller was — anonymous, "
              "a visitor, staff, an administrator or a moderator. The dashboard "
              "is the same data summarised.",
              "يكتب كل استدعاء للذكاء الاصطناعي سجلًّا واحدًا، نجح أم فشل: أي "
              "موجّه وأي خاصية، وأي مزوّد وأي نموذج، والمدخلات بعد تجريدها من "
              "البيانات الشخصية والأسرار، ونص المخرجات، وكم رمزًا دخل وخرج، وكم "
              "استغرق، ورمز الخطأ إن وُجد، ومن كان المستدعي — مجهولًا أو زائرًا أو "
              "موظفًا أو مسؤولًا أو مشرف جلسة. ولوحة المؤشرات هي البيانات نفسها "
              "ملخَّصة."),
            note("The prompt text itself is not on those rows. Prompt wording is "
                 "kept separately: every edit is stored in full in the prompt's "
                 "own history, which is never pruned, and the audit trail records "
                 "a fingerprint of the wording before and after each save so a "
                 "security reviewer can tell that a prompt changed without the "
                 "text being copied into a second place.",
                 "ونص الموجّه نفسه ليس في تلك السجلات. فصياغة الموجّه تُحفظ على "
                 "حدة: إذ يُخزَّن كل تعديل كاملًا في تاريخ الموجّه الخاص، وهو لا "
                 "يُقلَّم أبدًا، ويسجّل أثر التدقيق بصمةً للصياغة قبل كل حفظ وبعده، "
                 "فيتبيّن لمراجع الأمن أن الموجّه تغيّر دون نسخ النص إلى موضع "
                 "ثانٍ."),
            figure("cp-admin-ai-invocations-default",
                   "The invocations log. Failures are here too — that is the "
                   "point of it.",
                   "سجل الاستدعاءات. والإخفاقات هنا أيضًا — وهذا هو المقصود "
                   "منه."),
            figure("cp-admin-ai-default",
                   "The AI dashboard: the same calls, rolled up.",
                   "لوحة مؤشرات الذكاء الاصطناعي: الاستدعاءات نفسها مجمَّعة."),

            h2("How often you may test a prompt",
               "كم مرة يجوز لك اختبار موجّه"),
            p("The Test button on a prompt is limited to twenty calls an hour, "
              "counted per administrator on a rolling window rather than per "
              "computer — so two administrators sharing an office each get their "
              "own twenty, and one administrator moving between machines does not "
              "get forty. The Control Panel's own help assistant has a separate, "
              "larger allowance of forty an hour.",
              "زر الاختبار في الموجّه محدود بعشرين استدعاءً في الساعة، تُحسب لكل "
              "مسؤول على نافذة متحركة لا لكل جهاز — فمسؤولان يتشاركان مكتبًا "
              "لكلٍّ منهما عشرونه، ومسؤول واحد ينتقل بين الأجهزة لا يحصل على "
              "أربعين. ولمساعد لوحة التحكم نفسه مخصَّص منفصل أكبر: أربعون في "
              "الساعة."),
        ],
    }
