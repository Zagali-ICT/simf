# Play + App Store listing copy — DRAFT, needs owner approval

**Status:** drafted 2026-08-28. The audit found no listing text anywhere in the
repo, and `Mobile-Store-Release.md` records that Play will not create the store
listing over the API — it is console work. Console day should not stall on
copywriting for a Ministry-of-Defence-facing product, so this is the draft to
approve, edit, or replace.

**The Arabic needs the brand owner's sign-off before it goes up**, for the same
reason D-358 flagged: this repo already carries three different spellings of the
forum's Arabic name. Settle that first (see the bottom of this file).

Character limits are Google Play's. Apple's are tighter on the subtitle (30) and
promotional text (170); those are noted where they differ.

---

## Developer name (public on the listing)

**Apexium** — owner decision, 2026-08-28. The Play account belongs to
Zagali for Multi Active (UK, Organization, D-U-N-S held); Apexium is its KSA
branch and is the name shown to users.

## App name — the LAUNCHER and the STORE TITLE are different, deliberately

This section first said "use what the launcher already shows, or the two will
disagree". That was wrong, and it is the kind of wrong that costs installs. The
launcher name and the store title serve different jobs and normally differ: the
launcher is what fits under an icon, the store title is **what people search
for**. `SIMF` alone is four letters nobody types into Play.

| | Launcher (code, unchanged) | Store listing title | Length |
|---|---|---|---|
| English | `SIMF` | `SIMF – Saudi Maritime Forum` | 27 |
| Arabic | `الملتقى البحري` | `الملتقى البحري السعودي الدولي` | 29 |

Both titles fit Play's 30-character cap. **No code change** — `values/strings.xml`
and `values-ar/strings.xml` keep the short forms, which is what the launcher and
the in-app header want.

Do not add promotional words ("best", "official #1", "free") to the title. Play
rejects keyword-stuffed titles, and the descriptive form above is already the
searchable one.

---

## Short description — max 80 characters

**English (66):**

```
Your official companion for the Saudi International Maritime Forum.
```

**Arabic (44):**

```
رفيقك الرسمي في الملتقى البحري السعودي الدولي
```

---

## Full description — max 4000 characters

### English

```
The official app of the Saudi International Maritime Forum.

Plan your visit, move through the venue, and keep the whole programme in your
pocket for the duration of the forum.

WHAT YOU CAN DO

• Browse the full programme — sessions by day, with speakers, times and halls
• Reserve your seat in a session, and see your bookings in one place
• Follow live sessions, including the sign-language feed
• Put a question to the panel during a session
• Carry your admission badge as a QR code, ready at the gate
• Find your way with the venue map, the exhibition booths and the hall plan
• Read about the speakers, the exhibitors, the sponsors and the media partners
• Exchange contact details with other attendees by scanning a code
• Request a business meeting and track its status
• Catch up on forum news, the photo gallery and past editions
• Get notified when something you booked is about to start

REGISTRATION

The app is open to browse without an account. Registering as a visitor unlocks
your badge, seat booking, questions and contacts. Registration asks for an
identity document so your badge can be issued and verified at the entrance — how
we handle that is set out in full in our privacy policy.

ARABIC AND ENGLISH

Every screen works in both languages, right-to-left and left-to-right, and you
can switch at any time.

Saudi International Maritime Forum — Royal Saudi Naval Forces.
```

### Arabic

```
التطبيق الرسمي للملتقى البحري السعودي الدولي.

خطّط لزيارتك، وتنقّل داخل المعرض، واحتفظ بالبرنامج كاملاً في جيبك طوال أيام
الملتقى.

ماذا يمكنك أن تفعل

• تصفّح البرنامج كاملاً — الجلسات حسب اليوم، مع المتحدثين والأوقات والقاعات
• احجز مقعدك في الجلسة، وتابع حجوزاتك في مكان واحد
• تابع الجلسات المباشرة، بما في ذلك بث لغة الإشارة
• اطرح سؤالك على المتحدثين أثناء الجلسة
• احمل شارة الدخول الخاصة بك كرمز QR جاهزة عند البوابة
• استدل على طريقك عبر خريطة المكان وأجنحة المعرض ومخطط القاعات
• اطّلع على المتحدثين والعارضين والرعاة والشركاء الإعلاميين
• تبادل بيانات التواصل مع الحضور بمسح الرمز
• اطلب اجتماع أعمال وتابع حالته
• اطّلع على أخبار الملتقى ومعرض الصور والنسخ السابقة
• استلم تنبيهاً قبل بدء ما حجزته

التسجيل

يمكنك تصفّح التطبيق دون حساب. والتسجيل كزائر يفتح لك شارة الدخول وحجز المقاعد
والأسئلة وجهات الاتصال. يطلب التسجيل وثيقة هوية حتى تُصدر شارتك ويتم التحقق منها
عند المدخل، وسياسة الخصوصية تشرح تفصيلاً كيف نتعامل مع ذلك.

بالعربية والإنجليزية

كل الشاشات تعمل باللغتين، من اليمين إلى اليسار ومن اليسار إلى اليمين، ويمكنك
التبديل في أي وقت.

الملتقى البحري السعودي الدولي — القوات البحرية الملكية السعودية.
```

---

## What the copy deliberately does NOT say

- **No claim of being a government app** beyond the factual affiliation line at
  the end. The Government apps declaration on App content is where that is
  declared, and Google may ask RSNF/MoD for an authorization letter — see
  `Mobile-Store-Release.md`. Overclaiming in the listing risks suspension under
  Misleading Claims; underclaiming while carrying RSNF branding risks the same.
- **No feature that is not in the shipped build.** Every bullet above maps to a
  screen that exists in versionCode 20. A listing that promises more than the
  binary delivers is a rejection reason in its own right.
- **No dates or edition number.** They go stale and force a listing edit.

## The Arabic name question — settle before publishing

Three spellings are live in this repo:

| Where | Value |
|-------|-------|
| `values-ar/strings.xml`, `app_l10n.dart` `appName` | `الملتقى البحري` |
| `app_l10n.dart:530` `signInForumTitle`, **and the feature graphic** | `الملتقى الدولى البحرى` |
| `app_l10n.dart` (full brand form), website page titles | `الملتقى البحري السعودي الدولي` |

The second differs from the others in word order *and* writes the final letter as
alef maksura `ى` rather than yeh `ي`. D-358 flagged it when the sign-in screen
took the design's text verbatim. It is now on the **largest asset on the store
listing**, sitting above a title that spells the name differently.

Recommendation: `الملتقى البحري السعودي الدولي` as the full name (it matches the
website), `الملتقى البحري` as the short launcher form, and re-render the feature
graphic and fix `signInForumTitle` in the same changeset.
