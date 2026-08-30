"""The reports chapter: eight reports, one toolbar, and what an export is not.

The eight reports share so much furniture - the same date range, the same totals
strip, the same export button - that they read as one feature with eight
filters. They are not: each answers a different question, three of them
deliberately withhold a column somebody will ask for, and one of them ignores
the date range entirely.

The chapter closes on the export, because that is where the trust is misplaced:
it is capped, it is not localised, and neither fact is visible on screen.
Every rule below was read out of the reporting service and the report pages.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_reports():
    return {
        "id": "reports",
        "title": t("Reports", "التقارير"),
        "blocks": [
            p("The reports hub is a grid of cards, and you see only the cards you "
              "hold the permission for — the hub does not advertise a report it "
              "would then refuse to open. Each report is also gated at the "
              "server, on its own permission, so somebody can be given the gate "
              "log without being given the attendee roster.",
              "مركز التقارير شبكة بطاقات، ولا ترى منها إلا ما تملك صلاحيته — فهو "
              "لا يعرض تقريرًا سيرفض فتحه بعد ذلك. وكل تقرير مُحصَّن كذلك في "
              "الخادم بصلاحيته الخاصة، فيمكن منح أحدهم سجل البوابات دون منحه "
              "كشف الحضور."),
            figure("cp-admin-reports-default",
                   "The reports hub. A card you cannot see is a report you do "
                   "not have permission for.",
                   "مركز التقارير. البطاقة التي لا تراها تقريرٌ لا تملك صلاحيته."),

            note("Every figure in every report is calculated when you open it, "
                 "from the live records. Nothing is stored, summarised nightly or "
                 "cached, so two people running the same report a minute apart "
                 "can legitimately see different numbers. FDS-011 describes "
                 "stored statistics snapshots; they were never built, and the "
                 "document records that correction itself.",
                 "كل رقم في كل تقرير يُحسب حين تفتحه، من السجلات الحية. ولا شيء "
                 "مخزَّن ولا ملخَّص ليلًا ولا مخبَّأ مؤقتًا، فقد يرى شخصان يشغّلان "
                 "التقرير نفسه بفارق دقيقة أرقامًا مختلفة عن حق. ويصف المستند "
                 "FDS-011 لقطات إحصائية مخزَّنة؛ ولم تُبنَ قط، والمستند نفسه يسجّل "
                 "ذلك التصحيح."),

            h2("What each report answers", "عمّا يجيب كل تقرير"),
            table(
                ["Report", "The question it answers", "Worth knowing"],
                ["التقرير", "السؤال الذي يجيب عنه", "جدير بالمعرفة"],
                [
                    ["Attendance", "Was this session attended? One row per "
                     "session, with how many people arrived and how many are "
                     "still inside",
                     "The attendee number counts people, not scans: somebody who "
                     "left and came back counts once"],
                    ["Registrations", "Who registered, and in what state? One row "
                     "per attendee account",
                     "The date range filters when the account was created"],
                    ["Gate activity", "What happened at the doors? One row per "
                     "scan, admitted or refused, with the reason",
                     "The visitor's name and type are the ones recorded on the "
                     "scan itself, so a historic row still reads correctly after "
                     "somebody is renamed"],
                    ["Sessions", "How did this session do? Programme, speakers, "
                     "attendance, questions and average score on one row",
                     "An unrated session shows an empty score, not zero — a zero "
                     "would read as unanimously terrible"],
                    ["Ratings", "What was scored, and where? One row per rating "
                     "submitted",
                     "The respondent is deliberately not shown: a rating carries "
                     "a free-text comment, and naming the author would turn an "
                     "anonymous channel into an attributed one"],
                    ["Partners", "Who is participating? Exhibitors, sponsors and "
                     "booths flattened into one contact directory",
                     "This one ignores the date range, and the control is hidden "
                     "on it — a directory is who is here, not events in a "
                     "period"],
                    ["Meetings", "What was requested, of whom, and was it "
                     "answered? Speaker and delegation requests together",
                     "The date range filters when the request was made, not when "
                     "the meeting was scheduled"],
                    ["Engagement", "What did the audience ask, and what was "
                     "suppressed? Session questions with their moderation state",
                     "Hidden questions are included — that is the point of a "
                     "moderation report. The asker is not shown"],
                ],
                [
                    ["الحضور", "هل حُضرت هذه الجلسة؟ سجل لكل جلسة، ومعه كم شخصًا "
                     "وصل وكم ما زال بالداخل",
                     "عدد الحاضرين يعدّ الأشخاص لا عمليات المسح: فمن خرج ثم عاد "
                     "يُعدّ مرة واحدة"],
                    ["التسجيلات", "من سجّل، وبأي حالة؟ سجل لكل حساب حاضر",
                     "يرشّح المدى الزمني وقت إنشاء الحساب"],
                    ["نشاط البوابات", "ماذا جرى عند الأبواب؟ سجل لكل مسح، سماحًا "
                     "أو رفضًا، مع السبب",
                     "اسم الزائر ونوعه هما المسجَّلان في عملية المسح نفسها، فيظل "
                     "السجل التاريخي صحيحًا بعد إعادة تسمية الشخص"],
                    ["الجلسات", "كيف كان أداء هذه الجلسة؟ البرنامج والمتحدثون "
                     "والحضور والأسئلة ومتوسط التقييم في سجل واحد",
                     "الجلسة غير المقيَّمة تُعرض بتقييم فارغ لا بصفر — فالصفر "
                     "يُقرأ على أنه سوءٌ بالإجماع"],
                    ["التقييمات", "ماذا قُيِّم، وأين؟ سجل لكل تقييم مُرسَل",
                     "لا يُعرض المقيِّم عمدًا: فالتقييم يحمل تعليقًا نصيًّا حرًّا، "
                     "وذكر كاتبه يحوّل قناة مجهولة إلى قناة منسوبة"],
                    ["الشركاء", "من المشارك؟ العارضون والرعاة والأجنحة في دليل "
                     "تواصل واحد",
                     "هذا التقرير يتجاهل المدى الزمني، وأداة المدى مخفية فيه — "
                     "فالدليل هو من هنا، لا أحداث في فترة"],
                    ["اللقاءات", "ماذا طُلب، وممن، وهل أُجيب؟ طلبات المتحدثين "
                     "والوفود معًا",
                     "يرشّح المدى الزمني وقت تقديم الطلب لا وقت انعقاد اللقاء"],
                    ["التفاعل", "ماذا سأل الجمهور، وما الذي حُجب؟ أسئلة الجلسات "
                     "وحالة الإشراف عليها",
                     "الأسئلة المحجوبة مُدرجة — وهذا هو المقصود من تقرير إشراف. "
                     "ولا يُعرض السائل"],
                ]),
            figure("cp-admin-reports-gates-default",
                   "The gate activity report. Refusals are rows here too, with "
                   "the reason each one was refused.",
                   "تقرير نشاط البوابات. والرفض سجلات هنا أيضًا، ومع كل رفض "
                   "سببه."),

            h2("The toolbar every report shares",
               "الشريط الذي تشترك فيه كل التقارير"),
            bullets(
                ["**The date range is in Saudi calendar dates and includes both "
                 "ends.** A range of the 1st to the 3rd includes everything on "
                 "the 3rd, up to midnight.",
                 "**The totals describe the whole filtered set, not the page you "
                 "are looking at.** They will not add up to the rows on screen, "
                 "and that is correct.",
                 "**Changing the range takes you back to the first page.** "
                 "Staying on page seven of a different period would show an "
                 "empty grid for no visible reason.",
                 "**There is no filter for the event year.** A report has no "
                 "edition dimension; use the date range."],
                ["**المدى الزمني بالتواريخ الميلادية بتوقيت السعودية ويشمل "
                 "الطرفين.** فالمدى من الأول إلى الثالث يشمل كل ما وقع في "
                 "الثالث حتى منتصف الليل.",
                 "**والإجماليات تصف المجموعة المرشَّحة كاملةً لا الصفحة التي "
                 "تنظر إليها.** فلن تساوي مجموع السجلات المعروضة، وذلك صحيح.",
                 "**وتغيير المدى يعيدك إلى الصفحة الأولى.** فالبقاء في الصفحة "
                 "السابعة من فترة أخرى يعرض شبكة فارغة بلا سبب ظاهر.",
                 "**ولا يوجد ترشيح بسنة الفعالية.** فليس للتقرير بُعد دورةٍ؛ "
                 "استخدم المدى الزمني."]),
            figure("cp-admin-reports-ratings-default",
                   "A report page: the range and the totals strip above the "
                   "grid, and the export beside them.",
                   "صفحة تقرير: المدى وشريط الإجماليات فوق الشبكة، والتصدير إلى "
                   "جانبهما."),

            h2("Exporting", "التصدير"),
            p("Export produces an Excel workbook, downloaded as an attachment and "
              "named for the report and the moment you asked for it in Saudi "
              "local time. It carries the whole filtered report, not just the "
              "page on screen. It is gated by its own permission, separate from "
              "the permission to read the report: taking the data off the "
              "premises as a file is a bigger act than reading a page of it.",
              "ينتج التصدير مصنّف إكسل، يُنزَّل كمرفق ويُسمّى باسم التقرير ولحظة "
              "طلبك إياه بتوقيت السعودية المحلي. ويحمل التقرير المرشَّح كاملًا لا "
              "الصفحة المعروضة فقط. وهو محصَّن بصلاحيته الخاصة، منفصلةً عن صلاحية "
              "قراءة التقرير: فإخراج البيانات من المنشأة في ملف فعلٌ أكبر من "
              "قراءة صفحة منها."),
            note("**An export stops at twenty thousand rows, and says nothing.** "
                 "Beyond that it returns the first twenty thousand in the "
                 "report's own order rather than failing, and nothing on screen "
                 "tells you it happened. On an event-scale gate log that is "
                 "easy to reach. Narrow the date range and export in pieces when "
                 "the report is large.",
                 "**يتوقف التصدير عند عشرين ألف سجل، ولا يقول شيئًا.** فإذا "
                 "تجاوزها أعاد أول عشرين ألفًا بترتيب التقرير نفسه بدل أن يفشل، "
                 "ولا شيء على الشاشة يخبرك بذلك. وبلوغ هذا العدد يسير في سجل "
                 "بوابات بحجم فعالية. فضيّق المدى الزمني وصدّر على أجزاء حين "
                 "يكون التقرير كبيرًا."),
            p("The workbook also does not read like the screen. Where the page "
              "shows a translated label, the file carries the underlying code — "
              "so a gate export says HolderNotApproved and CheckIn where the "
              "Arabic page said them in Arabic. That is deliberate: an export is "
              "a file people sort, filter and pivot on, and a stable identifier "
              "is worth more there than a sentence.",
              "والمصنّف كذلك لا يُقرأ كما تُقرأ الشاشة. فحيث تعرض الصفحة تسمية "
              "مترجَمة، يحمل الملف الرمز الأصلي — فيقول تصدير البوابات "
              "HolderNotApproved وCheckIn حيث قالتهما الصفحة العربية بالعربية. "
              "وذلك مقصود: فالتصدير ملف يفرزه الناس ويرشّحونه ويحلّلونه، والمعرّف "
              "الثابت فيه أنفع من الجملة."),
        ],
    }
