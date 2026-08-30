"""Building the programme: sessions, halls, seating, bookings and meetings.

Every dependency here was read from the entity and the validator that enforces
it, not from the menu order and not from the requirements document - which is
worth saying because on two points they disagree, and the chapter says so rather
than quietly siding with one.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_programme():
    return {
        "id": "programme",
        "title": t("Building the programme", "بناء البرنامج"),
        "blocks": [
            p("The Programme menu lists twenty pages in a sensible reading order, "
              "and it is easy to take that order for a wizard. It is not. The "
              "actual dependencies are far sparser than the menu suggests, and "
              "knowing which is which saves an afternoon of filling in screens "
              "nothing was waiting for.",
              "تسرد قائمة البرنامج عشرين صفحة بترتيب قراءة منطقي، ومن السهل أن "
              "يُظنّ هذا الترتيب معالجًا متسلسلًا. وليس كذلك. فالاعتماديات الفعلية "
              "أقل بكثير مما توحي به القائمة، ومعرفة الفرق يوفّر عصرًا كاملًا في "
              "ملء شاشات لم يكن أحد ينتظرها."),

            h2("What a session actually needs", "ما تحتاجه الجلسة فعلًا"),
            table(
                ["To create a session you need", "Required?"],
                ["لإنشاء جلسة تحتاج", "مطلوب؟"],
                [
                    ["A hall, and it must be active",
                     "Yes — the only hard link a session has"],
                    ["A type: Workshop, Session or Event", "Yes"],
                    ["At least one speaker",
                     "Yes, unless the type is Event"],
                    ["A unique code", "Yes — and it stays taken by a deleted session"],
                    ["One or more themes", "No"],
                    ["A session category", "No"],
                    ["A programme day covering its date",
                     "No — sessions are matched to a day by DATE, not by a link"],
                ],
                [
                    ["قاعة، ويجب أن تكون نشطة",
                     "نعم — وهي الارتباط الإلزامي الوحيد للجلسة"],
                    ["نوع: ورشة أو جلسة أو فعالية", "نعم"],
                    ["متحدث واحد على الأقل",
                     "نعم، إلا إذا كان النوع فعالية"],
                    ["رمز فريد", "نعم — ويظل محجوزًا لجلسة محذوفة"],
                    ["محور واحد أو أكثر", "لا"],
                    ["تصنيف للجلسة", "لا"],
                    ["يوم برنامج يغطي تاريخها",
                     "لا — تُربط الجلسات باليوم بالتاريخ، لا برابط"],
                ]),
            note("Two rules catch people out. A session code stays reserved after "
                 "the session is deleted, because deletion deactivates rather "
                 "than removes — so reusing a code from a cancelled session is "
                 "refused. And two active sessions cannot overlap in the same "
                 "hall; the second one is rejected outright rather than flagged.",
                 "قاعدتان توقعان الناس. يظل رمز الجلسة محجوزًا بعد حذفها، لأن الحذف "
                 "تعطيل لا إزالة — فيُرفض إعادة استخدام رمز جلسة ملغاة. ولا يمكن أن "
                 "تتداخل جلستان نشطتان في القاعة نفسها؛ وتُرفض الثانية رفضًا صريحًا "
                 "لا تُعلَّم فحسب."),
            note("The requirements document says a session's theme, hall and "
                 "category are all required. The code requires only the hall. "
                 "Where the two disagree, the system does what the code says.",
                 "تقول وثيقة المتطلبات إن محور الجلسة وقاعتها وتصنيفها كلها مطلوبة. "
                 "والشيفرة لا تشترط إلا القاعة. وحين يختلفان، يفعل النظام ما تقوله "
                 "الشيفرة."),

            h3("Themes and categories are not the same thing",
               "المحاور والتصنيفات ليست شيئًا واحدًا"),
            p("A theme is one of the forum's pillars: a code, a bilingual name, an "
              "order and an accent colour, and a session may carry several. A "
              "category is a single, separate label, and its list ships EMPTY on "
              "purpose — the values are the client's to decide, which is why it "
              "is a table you fill rather than a fixed set in the code. Neither "
              "is required, so a session with no theme and no category is legal "
              "and will simply appear ungrouped wherever the programme is shown.",
              "المحور أحد ركائز الملتقى: رمز واسم بلغتين وترتيب ولون مميّز، وقد تحمل "
              "الجلسة أكثر من محور. أما التصنيف فتسمية واحدة منفصلة، وقائمته تُشحن "
              "فارغة عن قصد — فالقيم يحدّدها العميل، ولهذا هي جدول تملؤه لا مجموعة "
              "ثابتة في الشيفرة. وليس أيٌّ منهما مطلوبًا، فالجلسة بلا محور وبلا "
              "تصنيف جلسة سليمة، وستظهر فحسب غير مجمّعة حيثما عُرض البرنامج."),
            figure("cp-admin-sessions-default",
                   "The sessions list, with the real programme in it.",
                   "قائمة الجلسات، وفيها البرنامج الحقيقي."),

            h3("Programme days matter — but not where you would expect",
               "أيام البرنامج مهمة — لكن ليس حيث تتوقع"),
            p("Because a session finds its day by date, a missing programme day "
              "does not stop anybody scheduling anything. What it does instead is "
              "quiet and worse. The forum's own date boundary is calculated as "
              "the earliest and latest ACTIVE programme day. Business meetings, "
              "hall allocations, and speaker and delegation availability windows "
              "must all fall inside it. Two things are not checked against it: a "
              "HALL availability window, and the slot a meeting is finally bound "
              "to — that slot comes out of a hall window, so it carries whatever "
              "dates the hall window carries. When there are no active days the "
              "boundary is nothing at all, and even the checks that do apply stop "
              "applying — so an administrator can set a speaker's availability in "
              "the wrong month and nothing objects.",
              "لأن الجلسة تجد يومها بالتاريخ، فإن غياب يوم برنامج لا يمنع أحدًا من "
              "جدولة أي شيء. لكن ما يفعله بدلًا من ذلك أهدأ وأسوأ. إذ يُحسب حدّ "
              "تواريخ الملتقى بأبكر وأحدث يوم برنامج نشط. ويجب أن تقع داخله "
              "اجتماعات الأعمال وتخصيصات القاعات ونوافذ إتاحة المتحدثين والوفود. "
              "وأمران لا يُفحصان مقابله: نافذة إتاحة القاعة، والموعد الذي يُربط به "
              "الاجتماع في النهاية — فهذا الموعد مأخوذ من نافذة قاعة، فيحمل ما "
              "تحمله تلك النافذة من تواريخ. وحين لا توجد أيام نشطة ينعدم ذلك الحد "
              "ويتوقف حتى ما يسري من عمليات التحقق — فيستطيع المسؤول ضبط إتاحة "
              "متحدث في شهر خاطئ دون أن يعترض شيء."),
            note("The boundary is read from the programme days deliberately, and "
                 "not from the event start and end dates on the organisation "
                 "profile — those hold a placeholder range that has been stale "
                 "for some time. If meetings are landing outside the forum, check "
                 "the programme days are present AND active first, then look for "
                 "a hall availability window authored outside them.",
                 "يُقرأ الحد من أيام البرنامج عن قصد، لا من تاريخَي بداية الفعالية "
                 "ونهايتها في ملف الجهة — فهذان يحملان نطاقًا مؤقتًا قديمًا منذ مدة. "
                 "فإذا وقعت اجتماعات خارج أيام الملتقى، فتحقق أولًا من وجود أيام "
                 "البرنامج ومن كونها نشطة، ثم ابحث عن نافذة إتاحة قاعة أُنشئت "
                 "خارجها."),
            note("Run of Show is a read-only view. It groups the same sessions by "
                 "day on one screen and authors nothing — there is no edit "
                 "permission for it because there is nothing to edit.",
                 "جدول الفعاليات صفحة عرض فقط. فهي تجمّع الجلسات نفسها حسب اليوم في "
                 "شاشة واحدة ولا تنشئ شيئًا — ولا توجد لها صلاحية تحرير لأنه لا شيء "
                 "فيها يُحرَّر."),

            h3("Publishing a session needs a recording",
               "نشر الجلسة يتطلب تسجيلًا"),
            p("A session moves Scheduled → Held → Recorded → Published, one step "
              "at a time; it cannot jump. It cannot be marked Held before its "
              "start time has passed, and it cannot reach Recorded or Published "
              "at all until a recording file has been uploaded. Un-publishing "
              "means stepping back to Recorded — that is the only retraction.",
              "تنتقل الجلسة من مجدولة إلى منعقدة إلى مُسجَّلة إلى منشورة، خطوة بخطوة "
              "ولا تقفز. ولا يمكن وسمها منعقدة قبل مرور وقت بدايتها، ولا يمكنها بلوغ "
              "«مسجّلة» أو «منشورة» إطلاقًا قبل رفع ملف تسجيل. والتراجع عن النشر هو "
              "العودة خطوةً إلى «مسجّلة» — وهو سبيل التراجع الوحيد."),

            {"t": "pagebreak"},

            h2("Seating is two levels, and the top one is the hall",
               "المقاعد مستويان، وأعلاهما القاعة"),
            p("The hall owns the seat map; the session only paints reservations on "
              "it. There is no per-session layout: the session seat plan draws "
              "the hall's rows and shows what is held on them for that session.",
              "القاعة هي التي تملك خريطة المقاعد؛ والجلسة لا تفعل سوى رسم الحجوزات "
              "عليها. فلا يوجد مخطط خاص بكل جلسة: إذ يرسم مخطط مقاعد الجلسة صفوف "
              "القاعة ويعرض ما هو محجوز عليها لتلك الجلسة."),
            bullets([
                "Define the hall's rows once, on Hall seat layouts: row labels, "
                "seats per row, and a tier per row. The seats across all rows may "
                "not exceed the hall's capacity.",
                "Then, per session, use Session seat plans to block a whole row, "
                "hold a single seat for a VIP, or release something held.",
                "A hall with NO layout has no seat picker at all. The session "
                "silently becomes open seating and allocates against the hall's "
                "capacity number — it does not fail, it just stops being a seat "
                "map.",
            ], [
                "عرّف صفوف القاعة مرة واحدة في صفحة مخططات مقاعد القاعات: تسميات "
                "الصفوف، وعدد المقاعد في كل صف، وفئة لكل صف. ولا يجوز أن يتجاوز "
                "مجموع المقاعد سعة القاعة.",
                "ثم استخدم مخططات مقاعد الجلسات، لكل جلسة، لحجب صف كامل أو حجز مقعد "
                "واحد لشخصية بارزة أو تحرير محجوز.",
                "والقاعة التي لا مخطط لها لا يوجد بها اختيار مقاعد أصلًا. فتتحول "
                "الجلسة بهدوء إلى جلوس مفتوح وتُوزّع مقابل رقم سعة القاعة — ولا "
                "تُخفق، بل تكفّ عن كونها خريطة مقاعد.",
            ]),

            note("A newly added row starts as VVIP, deliberately: nobody may "
                 "book it until an administrator downgrades it to VIP or Normal. "
                 "That default has a history — a layout re-saved without its "
                 "tiers once shifted every row's tier by one, and a row that had "
                 "been bookable the day before began refusing every visitor with "
                 "no explanation that the tiers had moved. When you edit a "
                 "layout, check the tiers afterwards.",
                 "يبدأ الصف المضاف حديثًا بفئة كبار كبار الشخصيات عن قصد: فلا يجوز "
                 "لأحد حجزه حتى يخفضه مسؤول إلى فئة كبار الشخصيات أو الفئة العادية. "
                 "ولهذا الإعداد الافتراضي تاريخ — إذ أدّى حفظ مخطط دون فئاته مرة إلى "
                 "إزاحة فئة كل صف بمقدار واحد، فصار صف كان قابلًا للحجز بالأمس يرفض "
                 "كل زائر دون أي إشارة إلى أن الفئات قد انزاحت. فبعد تعديل أي مخطط، "
                 "راجع الفئات."),

            h3("Three ways a seat can be unavailable",
               "ثلاث طرق يصبح بها المقعد غير متاح"),
            table(
                ["What it is", "What it means"],
                ["ما هو", "ماذا يعني"],
                [
                    ["An admin-reserved row",
                     "A whole row painted off-limits for one session. It is "
                     "written as one record per seat, and it holds nobody — it "
                     "just blocks"],
                    ["A VIP tier row",
                     "A property of the HALL layout, not of a session. Only a "
                     "visitor whose profile type is a VIP tier may pick it"],
                    ["A VVIP seat with a guest note",
                     "Nobody may self-pick it. There is no registration behind "
                     "it, so the note you type IS the occupant record — "
                     "\"Reserved for the Minister\" is what the app and the "
                     "seating desk show"],
                ],
                [
                    ["صف محجوز إداريًا",
                     "صف كامل يُحجب لجلسة واحدة. ويُكتب سجلًا لكل مقعد، ولا يحجز "
                     "لأحد — بل يمنع فحسب"],
                    ["صف بفئة كبار الشخصيات",
                     "خاصية في مخطط القاعة لا في الجلسة. ولا يختاره إلا زائر نوع "
                     "ملفه من فئات كبار الشخصيات"],
                    ["مقعد كبار كبار الشخصيات مع ملاحظة ضيف",
                     "لا يجوز لأحد اختياره بنفسه. ولا تسجيل خلفه، فالملاحظة التي "
                     "تكتبها هي سجل شاغله — و«محجوز لمعالي الوزير» هو ما يعرضه "
                     "التطبيق ومكتب الإجلاس"],
                ]),

            h3("A booking IS a seat", "الحجز هو المقعد نفسه"),
            p("There is no separate booking record. When a visitor books a "
              "session on a hall that has a layout, the booking is the seat; on "
              "an open-seating session it is the same record with no seat on it. "
              "A visitor cannot hold two bookings for sessions that overlap in "
              "time — the second is refused.",
              "لا يوجد سجل حجز منفصل. فحين يحجز زائر جلسة في قاعة لها مخطط، يكون "
              "الحجز هو المقعد؛ وفي جلسة الجلوس المفتوح يكون السجل نفسه بلا مقعد. "
              "ولا يمكن للزائر أن يحمل حجزين لجلستين متداخلتين في الوقت — ويُرفض "
              "الثاني."),
            note("The Bookings page is a monitor, not a desk. Its approve and "
                 "reject actions were removed when the approval queue was retired, "
                 "so a booking is confirmed the moment it is made. Releasing a "
                 "held seat is done on Session seat plans — the server's own "
                 "error message says so, because people looked for it here first.",
                 "صفحة الحجوزات شاشة مراقبة لا مكتب عمل. فقد أُزيل منها إجراءا "
                 "القبول والرفض عند إلغاء قائمة الاعتماد، فالحجز مؤكد لحظة إنشائه. "
                 "أما تحرير مقعد محجوز فيتم في مخططات مقاعد الجلسات — ورسالة الخطأ "
                 "من الخادم تقول ذلك صراحة، لأن الناس بحثوا عنه هنا أولًا."),
            p("A held seat whose holder has not arrived is released automatically "
              "three minutes before the session starts. Blocked rows are exempt — "
              "they are not waiting for anybody.",
              "يُحرَّر تلقائيًا المقعد المحجوز الذي لم يحضر صاحبه قبل ثلاث دقائق من "
              "بدء الجلسة. والصفوف المحجوبة مستثناة — فهي لا تنتظر أحدًا."),

            {"t": "pagebreak"},

            h2("Four kinds of meeting, and only two have a queue",
               "أربعة أنواع من الاجتماعات، ولاثنين منها فقط قائمة انتظار"),
            table(
                ["Kind", "Who asks", "Who decides"],
                ["النوع", "من يطلب", "من يقرّر"],
                [
                    ["Speaker meeting request",
                     "An attendee, from the app",
                     "An administrator accepts — then the SPEAKER confirms by "
                     "email before it is really booked"],
                    ["Delegation meeting request",
                     "A delegate, for their country",
                     "The team accepts, then the target delegation confirms. Any "
                     "eligible member's click confirms for all of them"],
                    ["Business meeting",
                     "Nobody — there is no request",
                     "An administrator creates it, already confirmed"],
                    ["Meeting table",
                     "Not a request at all — it is a resource",
                     "An administrator adds tables to a meeting hall"],
                ],
                [
                    ["طلب مقابلة متحدث",
                     "أحد الحضور، من التطبيق",
                     "يقبله مسؤول — ثم يؤكده المتحدث نفسه بالبريد قبل أن يُحجز فعلًا"],
                    ["طلب اجتماع وفد",
                     "مندوب، نيابة عن بلده",
                     "يقبله الفريق، ثم يؤكده الوفد المستهدف. ونقرة أي عضو مؤهل "
                     "تؤكد عنهم جميعًا"],
                    ["اجتماع أعمال",
                     "لا أحد — لا يوجد طلب",
                     "ينشئه مسؤول، مؤكدًا منذ لحظته"],
                    ["طاولة اجتماعات",
                     "ليست طلبًا أصلًا — بل مورد",
                     "يضيف المسؤول طاولات إلى قاعة اجتماعات"],
                ]),
            note("The state that causes the most confusion is the one between "
                 "accept and confirm. Approve accepts a speaker request and binds "
                 "it to a slot, but it does not book it: it waits for the "
                 "speaker's own click on an emailed link, and while it waits the "
                 "ATTENDEE'S APP STILL SHOWS IT AS PENDING. Telling them it is "
                 "confirmed at that point is telling them something the app "
                 "contradicts. Confirm is the other path, for when you already "
                 "have the speaker's word: it books the slot at once, sends no "
                 "link at all, and the app shows the meeting as accepted straight "
                 "away. A third button, Accept without a hall, accepts the request "
                 "but books no room. The slot is held on either binding path, so "
                 "offering it to somebody else will fail.",
                 "أكثر الحالات إثارةً للّبس هي التي بين القبول والتأكيد. فزر "
                 "«موافقة» يقبل طلب مقابلة المتحدث ويربطه بموعد، لكنه لا يحجزه: "
                 "بل ينتظر نقرة المتحدث نفسه على رابط في بريده، وأثناء الانتظار "
                 "يظل تطبيق صاحب الطلب يعرضه «قيد الانتظار». وإخباره بأنه مؤكد في "
                 "تلك اللحظة إخبارٌ بما يناقضه التطبيق. أما زر «تأكيد» فهو المسار "
                 "الآخر، ولا يُستعمل إلا حين تكون قد أخذت موافقة المتحدث شفويًا: "
                 "فهو يحجز الموعد فورًا ولا يرسل أي رابط، ويعرض التطبيق الاجتماع "
                 "مقبولًا من فوره. وثمّة زر ثالث، «قبول بدون قاعة»، يقبل الطلب دون "
                 "حجز قاعة. والموعد محجوز في كلا مسارَي الربط، فعرضه على شخص آخر "
                 "سيُخفق."),
            p("The three availability pages — speaker, hall and delegation — exist "
              "to produce the bookable slots those requests choose from. Each "
              "window is a start and an end divided into fixed slots, half an "
              "hour by default. A speaker or delegation window must fall inside "
              "the forum days; a hall window is never checked against them, so it "
              "can be authored outside the event entirely — and because both "
              "meeting desks take their slots from the hall, such a window quietly "
              "produces bookable meetings outside the forum. Deleting a window "
              "does not cancel a meeting already booked in it; the meeting keeps "
              "its time.",
              "صفحات الإتاحة الثلاث — للمتحدثين والقاعات والوفود — موجودة لإنتاج "
              "المواعيد القابلة للحجز التي تختار منها تلك الطلبات. وكل نافذة بداية "
              "ونهاية مقسّمة إلى مواعيد ثابتة، نصف ساعة افتراضيًا. ويجب أن تقع "
              "نافذة المتحدث أو الوفد داخل أيام الملتقى؛ أما نافذة القاعة فلا "
              "تُفحص مقابلها إطلاقًا، فيمكن إنشاؤها خارج أيام الفعالية كلها — ولأن "
              "مكتبَي الاجتماعات يأخذان مواعيدهما من القاعة، فإن نافذة كهذه تُنتج "
              "بهدوء اجتماعات قابلة للحجز خارج أيام الملتقى. وحذف النافذة لا يلغي "
              "اجتماعًا حُجز فيها؛ إذ يحتفظ الاجتماع بوقته."),
            figure("cp-admin-business-meetings-default",
                   "Business meetings — created here, never requested.",
                   "اجتماعات الأعمال — تُنشأ هنا ولا تُطلب."),
        ],
    }
