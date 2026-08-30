"""Running the event day: gates, halls, questions, summaries, ratings, numbers.

The chapter an operator reads on the morning of the forum. Every rule was read
from the engine that enforces it - particularly the gate refusal reasons, which
are the difference between "the badge is wrong" and "the gate is wrong", and
which nothing else in the documentation lists in one place.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_event_day():
    return {
        "id": "event-day",
        "title": t("Running the event day", "تشغيل يوم الفعالية"),
        "blocks": [
            p("On the day, four groups of pages matter: the gates that let people "
              "in, the halls that record where they went, the question and "
              "summary desks the scientific committee works, and the numbers "
              "everybody asks for. This chapter is about those, in that order.",
              "في يوم الفعالية تهمّ أربع مجموعات من الصفحات: البوابات التي تُدخل "
              "الناس، والقاعات التي تسجّل أين ذهبوا، ومكتبا الأسئلة والملخصات اللذان "
              "تعمل عليهما اللجنة العلمية، والأرقام التي يسأل عنها الجميع. وهذا "
              "الفصل عنها، بهذا الترتيب."),

            h2("Gates", "البوابات"),
            p("A gate is a physical way in. Each one carries a direction — in, "
              "out, or both — a list of profile types it admits, and the "
              "operators assigned to work it. A gate attached to a hall is a hall "
              "door, and an allowed scan there opens or closes the attendee's "
              "attendance for that hall; a gate with no hall is a perimeter gate.",
              "البوابة مدخل مادي. ولكل واحدة اتجاه — دخول أو خروج أو كلاهما — وقائمة "
              "بأنواع الملفات التي تسمح بها، والمشغّلون المكلّفون بها. والبوابة "
              "المرتبطة بقاعة هي باب قاعة، والمسح المسموح عندها يفتح حضور صاحبه في "
              "تلك القاعة أو يغلقه؛ أما البوابة بلا قاعة فهي بوابة محيط."),

            note("The allow-list is the single most dangerous control on this "
                 "page, because it is not read the way it looks. An EMPTY list "
                 "admits everybody. A list with entries admits only those. And a "
                 "list whose every entry has been DEACTIVATED admits nobody at "
                 "all — it does not fall back to allow-all. That is deliberate: "
                 "denying visibly was judged safer than silently turning a VIP "
                 "gate into a public one. The consequence on the day is that the "
                 "gate refuses everyone, one badge at a time, and it goes on "
                 "doing so. Do not wait for it to stop itself: the gate's "
                 "failure circuit counts backend faults, not refusals, so no "
                 "number of refused badges will ever trip it. It has to be put "
                 "right on the gate's own page.",
                 "قائمة السماح أخطر أداة في هذه الصفحة، لأنها لا تُقرأ كما تبدو. "
                 "فالقائمة الفارغة تسمح للجميع. والقائمة التي فيها مدخلات لا تسمح "
                 "إلا لها. أما القائمة التي عُطّلت كل مدخلاتها فلا تسمح لأحد إطلاقًا "
                 "— ولا ترجع إلى السماح للجميع. وهذا مقصود: فقد رُئي أن المنع "
                 "الظاهر أسلم من تحويل بوابة كبار الشخصيات بصمت إلى بوابة عامة. "
                 "وأثر ذلك في اليوم أن البوابة ترفض الجميع، شارةً بعد شارة، وتمضي "
                 "في ذلك. ولا تنتظر أن تتوقف من تلقاء نفسها: فقاطع الأعطال في "
                 "البوابة يعدّ أعطال الخادم لا الرفضات، فلن يُفعّله أي عدد من "
                 "الشارات المرفوضة. ولا بد من تصحيح ذلك في صفحة البوابة نفسها."),
            figure("cp-admin-gates-default",
                   "The gates list. Each row is an entrance with its own direction "
                   "and allow-list.",
                   "قائمة البوابات. كل صف مدخل له اتجاهه وقائمة السماح الخاصة به."),

            h3("A refused scan is not an error", "المسح المرفوض ليس خطأً"),
            p("This is the distinction to hold on to. If the request itself "
              "failed — the operator is not assigned to this gate, or the gate's "
              "circuit has tripped — the console shows an error. If the badge was "
              "simply not admitted, the request SUCCEEDED and the refusal comes "
              "back as a reason on a normal response. So \"it worked\" is never "
              "evidence that anybody got in, and the operator should be reading "
              "the reason, not the status.",
              "هذا هو الفارق الذي ينبغي تذكّره. فإن أخفق الطلب نفسه — لأن المشغّل غير "
              "مكلّف بهذه البوابة، أو لأن قاطع البوابة قد فُعّل — أظهرت الشاشة خطأً. "
              "أما إذا لم تُقبل الشارة فحسب، فإن الطلب ينجح ويعود الرفض سببًا ضمن "
              "استجابة عادية. فـ«نجح» ليست دليلًا قط على أن أحدًا قد دخل، وعلى "
              "المشغّل أن يقرأ السبب لا الحالة."),
            table(
                ["The scan was refused because", "What to do"],
                ["رُفض المسح لأن", "ما العمل"],
                [
                    ["The code matched no attendee",
                     "Wrong or damaged badge — re-issue from Print badge"],
                    ["The gate was deactivated between the scan and the check",
                     "Somebody edited the gate mid-shift; re-activate it"],
                    ["The holder is not approved",
                     "Approve them from the pending queue"],
                    ["The account is disabled, or locked out",
                     "An administrator disabled it, or it hit the sign-in lockout"],
                    ["The holder's profile type is inactive",
                     "Re-activate the type in Reference data"],
                    ["The badge belongs to a closed edition",
                     "A badge from last year. Deliberately not distinguished to "
                     "the holder"],
                    ["The profile type is not on this gate's list",
                     "Either genuinely the wrong entrance, or the allow-list "
                     "problem above"],
                    ["A hall door, and they are not registered for anything it "
                     "admits for",
                     "They need a booking, or walk-in mode opened for the day"],
                ],
                [
                    ["الرمز لا يطابق أي حاضر",
                     "شارة خاطئة أو تالفة — أعد إصدارها من صفحة طباعة الشارة"],
                    ["عُطّلت البوابة بين المسح والتحقق",
                     "عدّل أحدهم البوابة أثناء الوردية؛ أعد تفعيلها"],
                    ["حامل الشارة غير معتمد",
                     "اعتمده من قائمة الانتظار"],
                    ["الحساب معطّل أو مقفل",
                     "عطّله مسؤول، أو بلغ قفل تسجيل الدخول"],
                    ["نوع ملف الحامل غير نشط",
                     "أعد تفعيل النوع في البيانات المرجعية"],
                    ["الشارة من نسخة مغلقة للملتقى",
                     "شارة من العام الماضي. ولا يُميَّز ذلك للحامل عن قصد"],
                    ["نوع الملف ليس في قائمة هذه البوابة",
                     "إمّا أنه المدخل الخاطئ فعلًا، وإمّا مشكلة قائمة السماح أعلاه"],
                    ["باب قاعة، وهو غير مسجّل في أي جلسة تسمح بها",
                     "يحتاج إلى حجز، أو إلى فتح وضع الحضور لذلك اليوم"],
                ]),
            note("Two behaviours that look like faults and are not. The same "
                 "badge scanned twice within five seconds replays the first "
                 "result instead of recording a second movement — unless the "
                 "operator deliberately flipped the direction on a both-way "
                 "gate, which is a real movement and is recorded. And the badge "
                 "is never told WHICH check it failed; a refusal is deliberately "
                 "vague to the person holding it, and specific only on the "
                 "operator's screen.",
                 "سلوكان يبدوان عطلًا وليسا كذلك. فالشارة نفسها إذا مُسحت مرتين خلال "
                 "خمس ثوانٍ يُعاد عرض نتيجة الأولى بدل تسجيل حركة ثانية — إلا إذا "
                 "قلب المشغّل الاتجاه عمدًا في بوابة ثنائية، فتلك حركة حقيقية "
                 "وتُسجَّل. كما لا يُخبَر حامل الشارة قط بأي فحص أخفق؛ فالرفض مبهم "
                 "له عن قصد، ولا يكون مفصّلًا إلا على شاشة المشغّل."),

            h2("Halls and who is inside them", "القاعات ومن بداخلها"),
            p("Hall attendance is a different record from a gate scan. A scan is "
              "the audit of an entrance; attendance is one row per attendee per "
              "session, opened when they arrive and closed when they leave. A row "
              "with no leaving time is somebody still in the room. Attendance is "
              "keyed to the PROFILE, not to an account, because a walk-in visitor "
              "may have no account at all.",
              "حضور القاعة سجل مختلف عن مسح البوابة. فالمسح تدقيق لمدخل، أما الحضور "
              "فسجل واحد لكل حاضر في كل جلسة، يُفتح عند وصوله ويُغلق عند مغادرته. "
              "والسجل بلا وقت مغادرة يعني شخصًا ما زال في القاعة. والحضور مرتبط "
              "بالملف لا بالحساب، لأن الزائر الحاضر قد لا يملك حسابًا أصلًا."),
            bullets([
                "Hall arrivals is the door console for a hall.",
                "Live hall is the read-only monitor: the seat map and everyone "
                "currently in the room, refreshing itself every fifteen seconds. "
                "It writes nothing.",
                "The gates dashboard shows who is currently inside the venue, "
                "worked out from each visitor's most recent allowed scan.",
            ], [
                "صفحة الوصول إلى القاعات هي وحدة الباب لقاعة بعينها.",
                "والقاعة المباشرة شاشة مراقبة للقراءة فقط: خريطة المقاعد وكل من في "
                "القاعة الآن، وتحدّث نفسها كل خمس عشرة ثانية. ولا تكتب شيئًا.",
                "ولوحة البوابات تعرض من هم داخل الموقع حاليًا، مستنتجًا ذلك من آخر "
                "مسح مسموح لكل زائر.",
            ]),
            note("\"Currently inside\" is derived, not stored, and it is bounded "
                 "by a rolling window on purpose. An entrance that only ever "
                 "records arrivals never records a departure, so without that "
                 "bound a visitor who came in once would be counted as present "
                 "forever.",
                 "«الموجودون بالداخل الآن» قيمة مستنتجة لا مخزَّنة، وهي مقيّدة بنافذة "
                 "متحركة عن قصد. فالمدخل الذي لا يسجّل إلا الوصول لا يسجّل مغادرة "
                 "قط، فلولا ذلك القيد لظل الزائر الذي دخل مرة محسوبًا حاضرًا إلى "
                 "الأبد."),
            figure("cp-admin-hall-arrivals-default",
                   "The hall arrivals console.",
                   "وحدة الوصول إلى القاعات."),

            {"t": "pagebreak"},

            h2("Questions, and who may touch them",
               "الأسئلة، ومن يجوز له التعامل معها"),
            p("Attendees submit questions from the app. Where a question goes "
              "depends on when it was asked. One sent before the session starts "
              "travels through three hands: an AI screen, the scientific "
              "committee, and then the session's own moderator. One sent once "
              "the session is live skips the first two by design; it lands "
              "approved on the session moderator's desk, and the moderator is "
              "the only gate on it. Each of those is a different page and a "
              "different permission.",
              "يرسل الحضور أسئلتهم من التطبيق. ووجهة السؤال تتوقف على وقت "
              "إرساله. فالمرسَل قبل بداية الجلسة يمرّ عبر ثلاث جهات: فرز بالذكاء "
              "الاصطناعي، ثم اللجنة العلمية، ثم مشرف الجلسة نفسه. أما المرسَل بعد "
              "أن تصير الجلسة مباشرة فيتخطى الجهتين الأوليين عن قصد؛ إذ يصل "
              "معتمدًا إلى مكتب مشرف الجلسة، ويكون المشرف هو الجهة الوحيدة عليه. "
              "وكل من هذه صفحة مختلفة وصلاحية مختلفة."),
            bullets([
                "The AI verdict is ADVISORY. It blocks nothing and hides nothing, "
                "and in the shipped configuration the filter is a stub until it "
                "is switched on.",
                "The committee works the central question queue: approve, hide, "
                "or escalate to a ROLE — never to a named person.",
                "The moderator works only their own session's approved set: "
                "reorder, hide, push to stage, mark answered. Questions still "
                "waiting for the committee are not readable from that desk, on "
                "purpose.",
                "Un-hiding restores a question to where it was, so a rejected "
                "question returns to pending rather than jumping to the stage.",
            ], [
                "حكم الذكاء الاصطناعي استرشادي. فهو لا يمنع شيئًا ولا يخفي شيئًا، "
                "وفي التهيئة المشحونة يكون المرشِّح معطّلًا حتى يُفعَّل.",
                "وتعمل اللجنة على قائمة الأسئلة المركزية: قبول أو إخفاء أو تصعيد "
                "إلى دور — لا إلى شخص بعينه أبدًا.",
                "ويعمل المشرف على المجموعة المعتمدة لجلسته وحدها: إعادة ترتيب "
                "وإخفاء ودفع إلى المنصة ووسم بالإجابة. أما الأسئلة التي ما زالت "
                "تنتظر اللجنة فلا تُقرأ من ذلك المكتب، عن قصد.",
                "وإلغاء الإخفاء يعيد السؤال إلى حيث كان، فالسؤال المرفوض يعود إلى "
                "الانتظار لا يقفز إلى المنصة.",
            ]),
            note("Moderator authority is granted PER SESSION, on the Session "
                 "moderators page. The mobile application's own Moderator role "
                 "grants none of it. Somebody given the app role and no grant "
                 "opens an empty desk and reports the system as broken; somebody "
                 "given the grant moderates exactly the session it names.",
                 "تُمنح صلاحية الإشراف لكل جلسة على حدة، من صفحة مشرفي الجلسات. أما "
                 "دور «مشرف» في تطبيق الجوال فلا يمنح منها شيئًا. فمن يُمنح دور "
                 "التطبيق بلا تفويض يفتح مكتبًا فارغًا ويبلّغ عن عطل في النظام؛ ومن "
                 "يُمنح التفويض يشرف على الجلسة المذكورة فيه تحديدًا."),
            p("An active session takes questions at any time up to the moment it "
              "ends — days ahead of the start is fine. Once the end time passes "
              "it takes none, with no grace at all; a session that has been "
              "deactivated takes none either way. The venue check applies only "
              "once the session is live: where the hall has a geofence the "
              "attendee must have a hall arrival on record for that session — "
              "they arrived at some point, whether or not they are still in the "
              "room — so somebody watching a live session remotely is refused, "
              "which is the intended behaviour, not a fault. Before the start, "
              "and in a hall with no geofence, there is no venue check at all.",
              "تستقبل الجلسة النشطة الأسئلة في أي وقت حتى لحظة انتهائها — ولا بأس "
              "بإرسالها قبل بدايتها بأيام. فإذا مضى وقت النهاية لم تعد تستقبل "
              "شيئًا، بلا أي مهلة؛ والجلسة المعطّلة لا تستقبل في الحالين. ولا "
              "يسري فحص الحضور إلا بعد أن تصير الجلسة مباشرة: فحيث تكون للقاعة "
              "حدود جغرافية يجب أن يكون للحاضر تسجيل وصول إلى القاعة في تلك "
              "الجلسة — أي أنه وصل في وقت ما، سواء أبقي في القاعة أم لا — فيُرفض "
              "من يتابع جلسة مباشرة عن بُعد، وهذا هو السلوك المقصود لا عطل. أما "
              "قبل البداية، وفي قاعة بلا حدود جغرافية، فلا فحص حضور إطلاقًا."),

            h2("Session summaries", "ملخصات الجلسات"),
            p("The committee's minutes for a session are drafted by the AI "
              "service and then approved by a person. The draft the model "
              "produced is kept separately and never overwritten by an edit, so "
              "there is always a record of where the text started.",
              "تُصاغ محاضر اللجنة لكل جلسة بخدمة الذكاء الاصطناعي ثم يعتمدها إنسان. "
              "وتُحفظ المسودة التي أنتجها النموذج على حدة ولا يُعاد كتابتها بأي "
              "تعديل، فيبقى دائمًا سجل لنقطة انطلاق النص."),
            note("In the shipped configuration the AI provider is a stub that "
                 "echoes the prompt back. It produces something that looks like "
                 "minutes and is not, and the system refuses to publish it — "
                 "which reads as a bug unless you know a real provider has not "
                 "been configured yet. Configuring one is a change to the "
                 "prompt's provider in the Control Panel, not a code change.",
                 "في التهيئة المشحونة يكون مزوّد الذكاء الاصطناعي بديلًا يردّ المحفّز "
                 "نفسه. فينتج شيئًا يشبه المحضر وليس محضرًا، ويرفض النظام نشره — "
                 "وهو ما يبدو خللًا ما لم تعلم أنه لم يُهيَّأ مزوّد حقيقي بعد. "
                 "وتهيئته تغيير لمزوّد المحفّز داخل لوحة التحكم، لا تغيير في "
                 "الشيفرة."),
            note("Approval is not permanent. Any edit to the content clears it, "
                 "and the summary must be approved again — so a small correction "
                 "after sign-off quietly returns it to the queue.",
                 "الاعتماد ليس نهائيًا. فأي تعديل في المحتوى يلغيه، ويجب اعتماد "
                 "الملخص من جديد — فالتصحيح الصغير بعد التوقيع يعيده بهدوء إلى "
                 "قائمة الانتظار."),

            h2("Ratings", "التقييمات"),
            p("Five rating forms ship with the system: one for the application, "
              "one for the event, one for the exhibition, one per day, and one "
              "per session. Their scope decides how often somebody may answer — "
              "once overall, once a day, or once per session — and the rule "
              "throughout is that you may only rate what you attended.",
              "تُشحن مع النظام خمس استمارات تقييم: واحدة للتطبيق، وواحدة للفعالية، "
              "وواحدة للمعرض، وواحدة لكل يوم، وواحدة لكل جلسة. ويحدّد نطاقها كم مرة "
              "يجوز للشخص الإجابة — مرة إجمالًا، أو مرة كل يوم، أو مرة لكل جلسة — "
              "والقاعدة في جميعها أنك لا تقيّم إلا ما حضرته."),
            p("A rating is asked for automatically: at the end of a day to "
              "everybody who checked in that day, at the end of the programme, "
              "when a session's clock runs out, when somebody leaves a hall, and "
              "when they close the live stream. Leaving the venue through a gate "
              "is NOT one of the triggers, because a gate belongs to the venue "
              "and knows nothing about which session anybody was in.",
              "يُطلب التقييم تلقائيًا: في نهاية اليوم من كل من سجّل حضوره ذلك اليوم، "
              "وفي نهاية البرنامج، وعند انتهاء وقت الجلسة، وعند مغادرة أحدهم القاعة، "
              "وعند إغلاقه البث المباشر. أما مغادرة الموقع من البوابة فليست من "
              "المحفّزات، لأن البوابة تخص الموقع ولا تعرف شيئًا عن الجلسة التي كان "
              "فيها أحد."),
            note("The five built-in forms can be renamed and their questions "
                 "changed, but they cannot be deleted and they cannot be "
                 "deactivated — the switch is ignored on a built-in type. Only "
                 "forms an administrator added can be turned off.",
                 "يمكن إعادة تسمية الاستمارات الخمس المدمجة وتغيير أسئلتها، لكن لا "
                 "يمكن حذفها ولا تعطيلها — إذ يُتجاهل المفتاح في النوع المدمج. ولا "
                 "يمكن إيقاف إلا الاستمارات التي أضافها مسؤول."),

            h2("The numbers", "الأرقام"),
            p("The dashboard and the statistics page compute every figure the "
              "moment you open them. There is no snapshot, no overnight rollup "
              "and no \"as at\" time — two people opening the page seconds apart "
              "can legitimately see different numbers, and there is nothing to "
              "re-run when a figure looks stale.",
              "تحسب لوحة المعلومات وصفحة الإحصائيات كل رقم لحظة فتحها. فلا توجد "
              "لقطة محفوظة ولا تجميع ليلي ولا وقت «حتى تاريخه» — فقد يرى شخصان "
              "يفتحان الصفحة بفارق ثوانٍ أرقامًا مختلفة عن حق، ولا شيء يُعاد تشغيله "
              "حين يبدو رقم قديمًا."),
            bullets([
                "Attendees are counted from PROFILES, not from accounts, because "
                "a walk-in or a pre-printed badge may have no account behind it — "
                "and at a real event that is a large share of the room.",
                "\"Total arrivals\" counts distinct attendee-and-session pairs, "
                "not scans: somebody who steps out of a hall and comes back "
                "counts once. It will therefore never equal the gate scan count, "
                "and the two should not be reconciled.",
                "Exhibitors and sponsors are counted as organisations the team "
                "manages, not as accounts.",
            ], [
                "يُحتسب الحضور من الملفات لا من الحسابات، لأن الحاضر المباشر أو "
                "الشارة المطبوعة مسبقًا قد لا يقف خلفها حساب — وفي فعالية حقيقية "
                "يمثّل هؤلاء نسبة كبيرة من القاعة.",
                "و«إجمالي الوصول» يعدّ أزواج الحاضر والجلسة المتمايزة لا عمليات "
                "المسح: فمن يخرج من قاعة ويعود يُحتسب مرة واحدة. ولذلك لن يساوي "
                "أبدًا عدد عمليات مسح البوابات، ولا ينبغي مطابقة الرقمين.",
                "ويُحتسب العارضون والرعاة جهاتٍ يديرها الفريق، لا حسابات.",
            ]),
            figure("cp-admin-statistics-default",
                   "The statistics page. Every figure on it was calculated when "
                   "the page opened.",
                   "صفحة الإحصائيات. كل رقم فيها حُسب لحظة فتح الصفحة."),
        ],
    }
