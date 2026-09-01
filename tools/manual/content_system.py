"""The system chapter: settings, the edition, the logs and the workers.

Ten pages sit under System, and four of them are settings pages that look
interchangeable and are not: a raw key/value store, a typed form whose values
have since moved to a different record, two singleton toggles, and a set of
overrides layered on top of deployment configuration. Confusing them is the
commonest way to edit something that nothing reads.

One page on this menu is genuinely destructive - opening an event year clears
every attendee badge - and the chapter says so plainly rather than burying it
under the fields. The deployment configuration chapter is a different subject
and is cross-referenced rather than repeated.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_system():
    return {
        "id": "system",
        "title": t("System settings, the event year, and the logs",
                   "إعدادات النظام وسنة الفعالية والسجلات"),
        "blocks": [
            p("Two warnings before the detail. First, the page called "
              "Configuration on this menu is not the deployment configuration "
              "described later in this manual: that one is environment variables "
              "on a server, and this one is a small table inside the "
              "application. Second, opening a new event year cancels every badge "
              "in the system. Both are covered below.",
              "تحذيران قبل التفصيل. أولًا، الصفحة المسماة «التهيئة» في هذه "
              "القائمة ليست تهيئة النشر الموصوفة لاحقًا في هذا الدليل: فتلك "
              "متغيّرات بيئة على خادم، وهذه جدول صغير داخل التطبيق. وثانيًا، فتح "
              "سنة فعالية جديدة يُلغي كل شارة في النظام. وكلاهما مشروح أدناه."),

            h2("Four settings pages, four different mechanisms",
               "أربع صفحات إعدادات، وأربع آليات مختلفة"),
            table(
                ["Page", "What it holds", "What it is for"],
                ["الصفحة", "ماذا تحمل", "الغرض منها"],
                [
                    ["Configuration", "Free key/value rows: a machine key, a "
                     "value, and a description for whoever edits it next",
                     "Whatever the organisation decides to keep there, on top of "
                     "the six app-update rows it ships with: the minimum and "
                     "latest app version and the store link, for Android and "
                     "for iOS. They arrive empty, and an empty value leaves "
                     "that rule off"],
                    ["Site settings", "A typed form: the bilingual registration "
                     "welcome message and the partner-directory switch",
                     "The handful of settings the app and the website read "
                     "through a public endpoint"],
                    ["Operations", "Two switches: whether registration is open, "
                     "with an optional automatic closing time, and whether the "
                     "archive is publicly visible",
                     "The two things most likely to be turned on and off during "
                     "an event"],
                    ["Walk-in mode", "Two overrides on top of what the servers "
                     "were deployed with, plus a read-only master state",
                     "Relaxing or tightening the on-site desk mid-event without "
                     "a deployment"],
                ],
                [
                    ["التهيئة", "سجلات مفتاح/قيمة حرة: مفتاح آلي، وقيمة، ووصف لمن "
                     "يحرّرها بعدك",
                     "ما تقرر الجهة الاحتفاظ به هناك، فوق ستة سجلات تحديث "
                     "التطبيق التي تُشحن معها: أدنى إصدار وأحدث إصدار ورابط "
                     "المتجر، لـ Android ولـ iOS. وتصل فارغة، والقيمة الفارغة "
                     "تُبقي تلك القاعدة معطّلة"],
                    ["إعدادات الموقع", "نموذج محدَّد الحقول: رسالة ترحيب التسجيل "
                     "بلغتين، ومفتاح دليل الشركاء",
                     "الإعدادات القليلة التي يقرؤها التطبيق والموقع عبر نقطة "
                     "قراءة عامة"],
                    ["العمليات", "مفتاحان: هل التسجيل مفتوح، مع وقت إغلاق تلقائي "
                     "اختياري، وهل الأرشيف ظاهر للعموم",
                     "أكثر أمرين ترجيحًا للتشغيل والإيقاف أثناء الفعالية"],
                    ["وضع الحضور المباشر", "تجاوزان فوق ما نُشرت به الخوادم، مع "
                     "حالة رئيسية للقراءة فقط",
                     "تخفيف ضبط مكتب الموقع أو تشديده أثناء الفعالية دون نشر"],
                ]),

            h3("Site settings no longer live where their name suggests",
               "إعدادات الموقع لم تعد تقيم حيث يوحي اسمها"),
            p("The registration welcome message and the social links used to be "
              "rows in the Configuration table. They are not any more: they were "
              "moved onto the organisation profile so there is one copy of each "
              "fact, and the social links are now edited on the Organisation "
              "profile page rather than here. The public reading of them is "
              "unchanged, so nothing downstream noticed the move — which is "
              "exactly why an operator can still type a registration-message key "
              "into the Configuration table and watch it change nothing.",
              "كانت رسالة ترحيب التسجيل وروابط التواصل الاجتماعي سجلات في جدول "
              "التهيئة. ولم تعد كذلك: فقد نُقلت إلى ملف الجهة ليكون لكل حقيقة "
              "نسخة واحدة، وصارت روابط التواصل تُحرَّر في صفحة ملف الجهة لا هنا. "
              "ولم تتغير قراءتها العامة، فلم يلحظ أحد في المصبّ النقل — وهو "
              "بالضبط سبب أن يظل المشغّل قادرًا على كتابة مفتاح رسالة تسجيل في "
              "جدول التهيئة ثم يرى أنه لم يغيّر شيئًا."),
            note("A social link is only published if it is a complete web "
                 "address. Anything else is dropped rather than shown as a broken "
                 "link.",
                 "لا يُنشر رابط التواصل إلا إذا كان عنوان ويب كاملًا. وما عدا ذلك "
                 "يُسقَط بدل عرضه رابطًا مكسورًا."),
            figure("cp-admin-site-settings-default",
                   "Site settings. The social links moved to the organisation "
                   "profile; what remains here is the registration message and "
                   "the partner-directory switch.",
                   "إعدادات الموقع. انتقلت روابط التواصل إلى ملف الجهة؛ والباقي "
                   "هنا رسالة التسجيل ومفتاح دليل الشركاء."),

            h3("Walk-in mode overrides configuration; it does not replace it",
               "وضع الحضور المباشر يتجاوز التهيئة ولا يحل محلها"),
            p("The two switches here sit on top of what the servers were deployed "
              "with. Until this page is saved the deployed setting applies to "
              "both. Saving writes an override for both — not only the switch "
              "you moved — and from then on your setting wins. Nothing on the "
              "page says where a value came from: a switch that agrees with the "
              "deployment looks exactly like one overriding it. To see the "
              "difference, or to hand a mode back to the deployment, go to the "
              "Configuration page and find the walkInMode.quickRegister and "
              "walkInMode.autoApprove rows. An active row holding true or false "
              "is an override; untick Active on it, or use Delete there — which "
              "deactivates the row rather than removing it — and the deployed "
              "setting applies again. The value itself cannot be blanked: that "
              "form refuses an empty value.",
              "المفتاحان هنا يجلسان فوق ما نُشرت به الخوادم. وحتى تُحفظ هذه "
              "الصفحة يسري الإعداد المنشور على كليهما. والحفظ يكتب تجاوزًا "
              "لكليهما — لا للمفتاح الذي حرّكته وحده — ومن ثَمّ يغلب إعدادك. ولا "
              "تقول الصفحة من أين جاءت القيمة: فالمفتاح الموافق للنشر يبدو "
              "تمامًا كالمفتاح المتجاوز له. ولترى الفرق، أو لتعيد وضعًا إلى ما "
              "نُشرت به الخوادم، اذهب إلى صفحة التهيئة وابحث عن سجلَّي "
              "walkInMode.quickRegister و walkInMode.autoApprove. فالسجل المفعّل "
              "الذي يحمل true أو false تجاوزٌ؛ فأزل عنه علامة «مُفعّل»، أو استخدم "
              "الحذف هناك — وهو يعطّل السجل ولا يزيله — فيعود الإعداد المنشور. "
              "أما القيمة نفسها فلا يمكن إفراغها: فذلك النموذج يرفض القيمة "
              "الفارغة."),
            note("The master switch — whether walk-in mode is armed at all, and "
                 "the window it is armed in — is deliberately not editable here. "
                 "An administrator may turn automatic approval off in the middle "
                 "of a rush, but cannot arm walk-in registration on an estate "
                 "that never enabled it; that still costs server access. When the "
                 "mode is not armed the page says so, rather than showing a "
                 "switch that would do nothing.",
                 "أما المفتاح الرئيس — هل وضع الحضور المباشر مسلَّح أصلًا، وفي أي "
                 "نافذة — فغير قابل للتحرير هنا عمدًا. فللمسؤول أن يوقف الاعتماد "
                 "التلقائي في وسط الزحام، ولا يستطيع تسليح تسجيل الحضور المباشر "
                 "في منشأة لم تفعّله قط؛ فذلك ما زال يستلزم صلاحية على الخادم. "
                 "وحين لا يكون الوضع مسلَّحًا تقول الصفحة ذلك، بدل عرض مفتاح لا "
                 "يفعل شيئًا."),
            figure("cp-admin-walk-in-mode-default",
                   "Walk-in mode. The armed state at the top is read-only; the "
                   "two switches below it are the overrides.",
                   "وضع الحضور المباشر. حالة التسليح في الأعلى للقراءة فقط؛ "
                   "والمفتاحان تحتها هما التجاوزان."),
            figure("cp-admin-operations-default",
                   "The operations toggles: the registration gate and the "
                   "archive's public visibility.",
                   "مفاتيح العمليات: بوابة التسجيل وظهور الأرشيف للعموم."),

            h2("Opening an event year", "فتح سنة فعالية"),
            p("The forum recurs. A year is opened, registrations and content "
              "accumulate against it, and then the next year is opened and that "
              "one becomes history. The page holds the open year, when it was "
              "opened, when a year was last closed, and how many badges the last "
              "opening re-issued.",
              "الملتقى يتكرر. تُفتح سنة، ويتراكم عليها التسجيل والمحتوى، ثم تُفتح "
              "السنة التالية فتصير تلك تاريخًا. وتحمل الصفحة السنةَ المفتوحة، "
              "ومتى فُتحت، ومتى أُغلقت آخر سنة، وكم شارة أعاد آخر فتحٍ إصدارها."),
            note("**Opening a year cancels every badge that exists.** Every "
                 "attendee's badge is cleared and a fresh one is issued in its "
                 "place, in one operation. Printed badges, emailed badges and "
                 "badges on a phone all stop opening gates the moment it "
                 "completes, and every attendee carried forward needs their new "
                 "one sent or printed. This is not a side effect: the gate "
                 "refuses a badge that does not carry the open year, so the "
                 "re-issue is what keeps returning attendees able to get in at "
                 "all. The confirmation states the consequence before you agree, "
                 "and says plainly that it cannot be undone. It carries no "
                 "number: the count is not known until the server has done the "
                 "work, and it is reported the moment the operation finishes.",
                 "**فتح سنة يُلغي كل شارة قائمة.** فتُمسح شارة كل حاضر وتُصدر له "
                 "شارة جديدة مكانها، في عملية واحدة. والشارات المطبوعة والمرسلة "
                 "بالبريد والموجودة على الهواتف كلها تكفّ عن فتح البوابات لحظة "
                 "اكتمال العملية، ويحتاج كل حاضر مُرحَّل إلى إرسال شارته الجديدة "
                 "أو طباعتها. وليس هذا أثرًا جانبيًّا: فالبوابة ترفض الشارة التي لا "
                 "تحمل السنة المفتوحة، وإعادة الإصدار هي ما يُبقي العائدين قادرين "
                 "على الدخول أصلًا. ويذكر التأكيد العاقبة قبل أن توافق، ويقول "
                 "صراحةً إن الإجراء لا رجعة فيه. ولا يحمل عددًا: فالعدد لا يُعرف "
                 "حتى ينتهي الخادم من العمل، ويُذكر لحظة اكتمال العملية."),
            bullets(
                ["The year must be four digits between 2000 and 2999. A typed "
                 "\"202\" or \"20265\" is refused, because the year is printed "
                 "into the badge and read by a scanner that has no other way to "
                 "ask.",
                 "The year already open is refused.",
                 "A year earlier than the open one is refused, permanently. "
                 "Re-opening a closed year would make every badge issued since "
                 "valid again, which is the opposite of what closing it meant.",
                 "The re-issue count is written down afterwards. It is the only "
                 "evidence that the re-issue actually ran, and it is the first "
                 "thing you will be asked for when a returning attendee finds "
                 "their badge dead."],
                ["يجب أن تكون السنة من أربعة أرقام بين 2000 و2999. ويُرفض ما "
                 "يُكتب مثل «202» أو «20265»، لأن السنة تُطبع في الشارة ويقرؤها "
                 "ماسحٌ لا سبيل له إلى السؤال بغيرها.",
                 "وتُرفض السنة المفتوحة أصلًا.",
                 "وتُرفض السنة الأقدم من المفتوحة رفضًا دائمًا. فإعادة فتح سنة "
                 "مغلقة تُعيد صلاحية كل شارة أُصدرت منذ ذلك الحين، وهو نقيض "
                 "المقصود من إغلاقها.",
                 "ويُدوَّن عدد إعادة الإصدار بعد ذلك. وهو الدليل الوحيد على أن "
                 "إعادة الإصدار جرت فعلًا، وأول ما ستُسأل عنه حين يجد حاضرٌ عائد "
                 "شارته ميتة."]),
            p("The year an attendee belongs to is stamped on their record the "
              "moment it is created, whichever way it was created — a sign-up, a "
              "desk, a bulk order, an approval. Opening a year moves that stamp "
              "forward only for the attendees who actually held a badge; somebody "
              "who never held one keeps the year they registered for, which is "
              "what makes the year worth reporting on.",
              "وتُختم السنة التي ينتمي إليها الحاضر على سجله لحظة إنشائه، بأي "
              "طريق أُنشئ — تسجيل ذاتي أو مكتب أو طلب جماعي أو اعتماد. وفتح سنة "
              "يقدّم ذلك الختم للحاضرين الذين حملوا شارة فعلًا فحسب؛ أما من لم "
              "يحمل شارة قط فيبقى على السنة التي سجّل لها، وهو ما يجعل السنة "
              "جديرة بأن تُبنى عليها التقارير."),
            figure("cp-admin-editions-default",
                   "The event edition page. Almost all of it exists to state the "
                   "consequence before the button is pressed.",
                   "صفحة دورة الفعالية. جلّها موجود لبيان العاقبة قبل ضغط الزر."),

            h2("The two logs are for two different people",
               "السجلّان لشخصين مختلفين"),
            table(
                ["", "Operation log", "Logs"],
                ["", "سجل العمليات", "السجلات"],
                [
                    ["Answers", "Who did what, to whom, and did it succeed",
                     "What the software did, technically, at 14:32"],
                    ["Holds", "One row per business or security event: the time "
                     "in local Saudi time, the event, the outcome, the subject, "
                     "the administrator who acted, the address they acted from, "
                     "and an error code when it failed",
                     "The applications' own log files, one per project per day"],
                    ["Can be edited", "No. Nobody, including an administrator, "
                     "can change or remove an entry through the application",
                     "Not applicable — they are files, and can be downloaded"],
                ],
                [
                    ["يجيب عن", "من فعل ماذا، بمن، وهل نجح",
                     "ما الذي فعله البرنامج تقنيًّا عند الساعة 14:32"],
                    ["يحمل", "سجلًّا لكل حدث عملي أو أمني: الوقت بتوقيت السعودية "
                     "المحلي، والحدث، والنتيجة، والمعنيّ، والمسؤول الذي نفّذ، "
                     "والعنوان الذي نفّذ منه، ورمز الخطأ عند الإخفاق",
                     "ملفات سجلات التطبيقات نفسها، ملف لكل مشروع في كل يوم"],
                    ["هل يمكن تحريره", "لا. ولا أحد، ولا المسؤول، يستطيع تغيير "
                     "مدخل أو إزالته من خلال التطبيق",
                     "لا ينطبق — فهي ملفات ويمكن تنزيلها"],
                ]),
            note("The operation log stores the subject's name as it was at the "
                 "time, so an entry still reads correctly after somebody is "
                 "renamed. Its detail line never carries a secret.",
                 "يحفظ سجل العمليات اسم المعنيّ كما كان حينها، فيظل المدخل صحيحًا "
                 "بعد تغيير اسم المعنيّ. ولا يحمل سطر تفصيله سرًّا أبدًا."),
            p("The Logs page lists the log files it can find, tails the one you "
              "pick every few seconds, and offers each as a download. It "
              "distinguishes between failing to reach the log service and finding "
              "no files, which are opposite facts when something is wrong.",
              "تعرض صفحة السجلات ملفاتِ السجلات التي تجدها، وتتابع آخر ما يُكتب "
              "في الملف الذي تختاره كل بضع ثوانٍ، وتتيح كلًّا منها للتنزيل. وتميّز "
              "بين تعذّر الوصول إلى خدمة السجلات وعدم وجود ملفات، وهما حقيقتان "
              "متضادتان حين يكون هناك خلل."),
            figure("cp-admin-operation-log-default",
                   "The operation log: the business and security audit trail.",
                   "سجل العمليات: أثر التدقيق العملي والأمني."),
            figure("cp-admin-logs-default",
                   "The logs viewer: the applications' own files, per project "
                   "and per day.",
                   "عارض السجلات: ملفات التطبيقات نفسها، لكل مشروع ولكل يوم."),

            h2("Background services", "خدمات الخلفية"),
            p("Thirteen background workers do the things nobody presses a button "
              "for. Fourteen services report their health to this page: those "
              "thirteen, plus the email sender. It shows whether each is "
              "running, when it last ran, how many times it has run and failed, "
              "and its last error. It refreshes itself and has no actions — it "
              "is a health display, not a control panel.",
              "ثلاثة عشر عاملًا في الخلفية تؤدي ما لا يضغط أحد زرًّا لأجله. وأربع "
              "عشرة خدمة تُبلّغ هذه الصفحة بحالتها: هذه الثلاثة عشر، ومرسل البريد "
              "معها. وتُظهر هل يعمل كلٌّ منها، ومتى عمل آخر مرة، وكم مرة عمل "
              "وأخفق، وآخر خطأ له. وهي تحدّث نفسها ولا إجراءات فيها — فهي عرض "
              "حالة لا لوحة تحكم."),
            table(
                ["Worker", "What it does"],
                ["العامل", "ماذا يفعل"],
                [
                    ["Registration gate auto-close", "Closes registration the "
                     "first time the automatic closing time passes"],
                    ["Session reminder", "\"Your session starts soon\" to "
                     "everybody holding a seat"],
                    ["Meeting reminder", "The same, a quarter of an hour before "
                     "a confirmed meeting, by app and email"],
                    ["Awaiting-speaker expiry", "Returns a meeting request to "
                     "the queue when the speaker's links expire, freeing the "
                     "held slot"],
                    ["No-show seat release", "Releases seats a few minutes "
                     "before a session starts when the holder never arrived"],
                    ["Not-attended reminder", "\"The session started and you are "
                     "not here\", after a grace period"],
                    ["Match recommendation push", "\"You match this attendee\" "
                     "for strong matches, in batches"],
                    ["Session rating prompt", "Asks for a rating — of the people "
                     "who checked into the hall, not everybody who booked"],
                    ["Programme rating prompt", "The end-of-day and "
                     "end-of-programme rating prompts"],
                    ["Hall attendance close-out", "Closes attendance records "
                     "left open when a session ends"],
                    ["Announcement queue", "Sends the queued announcements"],
                    ["Dormant account sweep", "Disables accounts unused for the "
                     "configured number of days; does nothing until that number "
                     "is set"],
                    ["Retention sweep", "The daily purge of expired security "
                     "artefacts"],
                ],
                [
                    ["الإغلاق التلقائي لبوابة التسجيل", "يغلق التسجيل أول مرة "
                     "يمضي فيها وقت الإغلاق التلقائي"],
                    ["تذكير الجلسة", "«تبدأ جلستك قريبًا» لكل من يحمل مقعدًا"],
                    ["تذكير اللقاء", "الشيء نفسه قبل ربع ساعة من لقاء مؤكَّد، "
                     "بالتطبيق والبريد"],
                    ["انتهاء انتظار المتحدث", "يعيد طلب اللقاء إلى الطابور حين "
                     "تنتهي صلاحية روابط المتحدث، فيحرّر الموعد المحجوز"],
                    ["تحرير مقاعد المتخلفين", "يحرّر المقاعد قبل دقائق من بدء "
                     "الجلسة إذا لم يحضر صاحبها"],
                    ["تذكير عدم الحضور", "«بدأت الجلسة ولست هنا»، بعد مهلة"],
                    ["دفع توصيات المطابقة", "«هذا الحاضر يوافقك» للمطابقات "
                     "القوية، على دفعات"],
                    ["طلب تقييم الجلسة", "يطلب التقييم — ممن سجّل دخوله إلى "
                     "القاعة، لا من كل من حجز"],
                    ["طلب تقييم البرنامج", "طلبا التقييم في نهاية اليوم ونهاية "
                     "البرنامج"],
                    ["إقفال حضور القاعة", "يقفل سجلات الحضور المتروكة مفتوحة عند "
                     "انتهاء الجلسة"],
                    ["طابور الإعلانات", "يرسل الإعلانات المصفوفة في الطابور"],
                    ["مسح الحسابات الخاملة", "يعطّل الحسابات غير المستخدمة "
                     "للمدة المهيَّأة؛ ولا يفعل شيئًا حتى تُضبط تلك المدة"],
                    ["مسح الاحتفاظ", "التنقية اليومية للعناصر الأمنية منتهية "
                     "الصلاحية"],
                ]),
            note("Each of these runs on exactly one server, whatever the size of "
                 "the estate, because two copies would send everything twice. The "
                 "email sender is the exception and runs everywhere on purpose: "
                 "its queue lives inside each server, so a server that is not "
                 "sending is a server whose emails never leave. It hands a "
                 "message to the mail relay three times at most — one send and "
                 "two retries — and only while the failure looks temporary; a "
                 "relay that refuses the message outright, for an unknown "
                 "recipient or bad credentials, ends it on the first failure. It "
                 "can also be configured to email an operations address when a "
                 "send fails.",
                 "يعمل كلٌّ من هذه على خادم واحد بالضبط مهما كان حجم المنشأة، لأن "
                 "نسختين ترسلان كل شيء مرتين. ومرسل البريد استثناء ويعمل في كل "
                 "مكان عمدًا: إذ يقيم طابوره داخل كل خادم، فالخادم الذي لا يرسل "
                 "خادمٌ لا يغادره بريده. ويسلّم الرسالة إلى مُرحِّل البريد ثلاث "
                 "مرات على الأكثر — إرسالة وإعادتان — وذلك ما دام الإخفاق يبدو "
                 "مؤقتًا؛ أما المُرحِّل الذي يرفض الرسالة رفضًا صريحًا، لمستقبِل "
                 "مجهول أو بيانات اعتماد خاطئة، فينتهي أمرها عند أول إخفاق. "
                 "ويمكن تهيئته أيضًا ليراسل عنوان عمليات عند إخفاق الإرسال."),
            figure("cp-admin-ops-services-default",
                   "The background services monitor. No actions — the value is "
                   "in the last-run and failure columns.",
                   "مراقب خدمات الخلفية. لا إجراءات فيه — والقيمة في عمودي آخر "
                   "تشغيل والإخفاقات."),

            h2("Email templates", "قوالب البريد"),
            p("Ten emails can be reworded here: the sign-in code, email "
              "verification, the account-already-exists notice, password reset, "
              "badge activation, the biometric step-up code, the bulk-badge "
              "cover note, email-change verification, the email-changed alert, "
              "and the exhibitor lead-capture message. There is no Add and no "
              "Delete, because the set is fixed by the code that sends them; the "
              "only action is Edit, and every template can be reset to the "
              "wording it shipped with.",
              "عشر رسائل بريد يمكن إعادة صياغتها هنا: رمز تسجيل الدخول، وتوثيق "
              "البريد، وإشعار وجود الحساب مسبقًا، وإعادة تعيين كلمة المرور، "
              "وتفعيل الشارة، ورمز التحقق الحيوي الإضافي، ورسالة الشارات "
              "الجماعية، وتوثيق تغيير البريد، وتنبيه تغيّر البريد، ورسالة التقاط "
              "العملاء للعارض. ولا إضافة ولا حذف، لأن المجموعة مثبَّتة بالشيفرة "
              "التي ترسلها؛ والإجراء الوحيد هو التحرير، ويمكن إرجاع كل قالب إلى "
              "صياغته الأصلية."),
            bullets(
                ["The database holds only your changes. A template you never "
                 "touched is sent from the wording built into the application, "
                 "so a fresh installation sends correct emails with an empty "
                 "table.",
                 "The tokens are per template and are offered as chips beside "
                 "the editor — the code and its expiry for the code emails, the "
                 "count and generation time for the bulk-badge note, the new "
                 "address for the change-of-email alert. A token that does not "
                 "belong to the template is rejected on save.",
                 "**The notification emails are not here.** Registration approved "
                 "or rejected and badge ready are written in the code and cannot "
                 "be reworded from the Control Panel. Two messages are yours to "
                 "write per send instead: the announcement composer, and the "
                 "notify-VIPs message on the VIPs page — each takes a bilingual "
                 "title and body and sends exactly what you type. Two others "
                 "reach nobody by email at all: the session reminder is sent "
                 "with email switched off, so it arrives only as a message "
                 "inside the app, and a booking confirmation goes out on no "
                 "channel — the kind exists in the app's notification list, and "
                 "nothing in the system sends it."],
                ["لا تحمل قاعدة البيانات إلا تغييراتك. فالقالب الذي لم تمسسه "
                 "يُرسل بالصياغة المضمَّنة في التطبيق، فيرسل التثبيت الجديد رسائل "
                 "صحيحة والجدول فارغ.",
                 "والرموز البديلة خاصة بكل قالب وتُعرض في صورة شرائح إلى "
                 "جانب المحرّر — الرمز ومدة صلاحيته في رسائل الرموز، والعدد "
                 "ووقت التوليد في رسالة الشارات الجماعية، والعنوان الجديد في "
                 "تنبيه تغيّر البريد. ويُرفض عند الحفظ أي رمز لا يخص القالب.",
                 "**ورسائل الإشعارات ليست هنا.** فاعتماد التسجيل أو رفضه "
                 "وجاهزية الشارة مكتوبان في الشيفرة ولا يمكن إعادة صياغتهما من "
                 "لوحة التحكم. ورسالتان تكتبهما أنت في كل إرسالة بدل ذلك: محرّر "
                 "الإعلانات، ورسالة إشعار كبار الشخصيات في صفحة كبار الشخصيات — "
                 "كلتاهما تأخذ عنوانًا ونصًّا بلغتين وترسل ما تكتبه بحرفه. "
                 "ورسالتان لا تصلان بالبريد أصلًا: فتذكير الجلسة يُرسل والبريد "
                 "مُطفأ، فلا يصل إلا رسالةً داخل التطبيق، وتأكيد الحجز لا يخرج "
                 "على أي قناة — فالنوع موجود في قائمة إشعارات التطبيق، ولا شيء "
                 "في النظام يرسله."]),
            figure("cp-admin-email-templates-default",
                   "The email templates. Edit is the only row action; the set "
                   "itself is fixed.",
                   "قوالب البريد. التحرير هو الإجراء الوحيد في السجل؛ والمجموعة "
                   "نفسها ثابتة."),

            h2("Organisation profile", "ملف الجهة"),
            p("This is the forum's own record, and it is edition-generic: the "
              "name, title, slogan and biography, the version and its dates, the "
              "status and year, the location and its coordinates, the contact "
              "details, the live-stream address, the social links, and the "
              "repeating lists of about-items and details. Opening it needs the "
              "view permission; saving it needs the manage permission, so a "
              "reader can be given the page without being able to change the "
              "forum's public identity.",
              "هذا سجل الملتقى نفسه، وهو عام لكل الدورات: الاسم والعنوان والشعار "
              "والنبذة، والإصدار وتواريخه، والحالة والسنة، والموقع وإحداثياته، "
              "وبيانات التواصل، وعنوان البث المباشر، وروابط التواصل الاجتماعي، "
              "والقوائم المتكررة لعناصر «عن» والتفاصيل. ويلزم لفتحه صلاحية العرض؛ "
              "ويلزم لحفظه صلاحية الإدارة، فيمكن منح القارئ الصفحة دون أن يقدر "
              "على تغيير الهوية العامة للملتقى."),
            figure("cp-admin-organization-profile-default",
                   "The organisation profile: the forum's own details, and the "
                   "home of the social links.",
                   "ملف الجهة: تفاصيل الملتقى نفسه، وموطن روابط التواصل."),
            figure("cp-admin-configuration-default",
                   "The Configuration table. It ships with the six app-update "
                   "policy keys already created and empty, ready for their "
                   "values; the software adds rows of its own here too — the "
                   "walk-in overrides, and the end-of-programme rating marker — "
                   "and a key an operator invents is stored but read by nothing.",
                   "جدول التهيئة. يُشحن ومفاتيح سياسة تحديث التطبيق الستة منشأة "
                   "فيه وفارغة، جاهزة لقيمها؛ والبرنامج يضيف هنا سجلات من عنده "
                   "أيضًا — تجاوزات الحضور المباشر، وعلامة تقييم نهاية البرنامج "
                   "— والمفتاح الذي يخترعه المشغّل يُحفَظ ولا يقرؤه شيء."),
        ],
    }
