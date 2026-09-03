# Acceptance review register

SIMF-BRD-001 v1.3, SIMF-HLD-004 v1.4, SIMF-LLD-003 v1.5. 2026-09-03.

## What this is

The three delivered documents read the way the customer reads them: by a
systems engineer who knows nothing of the project, holds only these three
files, and has to decide whether to sign a certificate of completion. No
source tree, no other document, no access to the contractor.

Six review passes raised **203 findings**. Each was then given to a reader
acting for the contractor whose job was to refute it from the same three
files. **125 were refuted** and are not recorded here. **78 survived.**

| Severity | Count | Meaning |
|---|---|---|
| BLOCKER | 3 | The certificate is not signed until it is fixed |
| MAJOR | 32 | Signed around, with a written reservation |
| MINOR | 43 | Recorded, does not affect signature |

## Verified independently before recording

Findings are not relayed on a reviewer's word. These were re-checked
against the documents directly, and the check changed the result twice:

- **Requirement counts.** The review reported 107 functional requirements.
  The BRD defines **90 FR, 12 NFR, 14 BR and 16 OP**, 132 in total, counted
  from the .docx and again from an independent text extraction. The larger
  number is a reviewer arithmetic error and is not used here.
- **Traceability.** Confirmed with a deliberately loose pattern that would
  match `FR-101`, `FR 101` and `FR101`: **not one** of the 132 identifiers
  appears in either design document.
- **Facial processing.** A first count suggested the BRD and HLD did mention
  it, which would have refuted four findings. Reading the matches showed
  they were `interface`, `surface` and `Face ID`. Searching for
  `facial|liveness|face verification` returns **zero** in the HLD. The
  findings stand.
- **Badge desk.** The HLD does describe SIMF.BadgeDesk as a fourth client.
  **Zero rows** of the section 2.8 communication matrix mention it.

## T1. The website surface is asserted and denied

*2 BLOCKER, 3 MAJOR*

The LLD denies a website authentication surface three times and its own route table lists one. This decides the penetration-test boundary, the personal-data inventory and the NFR-02 anonymous-surface statement, so nothing downstream can be scoped until it is answered.

### SEC-04 (BLOCKER, ACROSS)

The public internet-facing Website is described four times as having no sign-in and no account page and storing no personal data, while the LLD's own route table lists nine sign-in and account routes on it.

> LLD 7.1.2: "Website: Blazor SSR for static public content; no sign-in, registration or account page and no personal data stored, so no authentication surface and no per-page permissions." LLD 4.2, immediately above its own table: "It carries no sign-in, registration or account page and stores no personal data" and "The tables below reproduce the Real rows of the page index. No routes are invented here; every route is copied from that index." The table then lists: "| `/login` | Login | Anyone | Sign-in (auth-only) |", "| `/login/verify` | OTP verify | Mid-sign-in | E-mail OTP second factor (auth-only) |", "| `/forgot-password` | Forgot password | Anyone | Request password reset (auth-only) |", "| `/reset-password` | Reset password | After forgot | Set new password (auth-only) |", "| `/account/profile` | Account profile | Any signed-in | View/edit profile (interactive) |", "| `/account/notifications` | Notifications | Any signed-in | Notification list |", "| `/account` | Home (account) | Any signed-in | Signed-in home |", plus `/account/pending` and `/account/rejected`. LLD 2.1.2 adds a third contradiction: "the Website uses only `SimfPublicClient` ... The Website has no such flow: it signs nobody in and holds no bearer token."

**Why it affects the signature.** This is the difference between two entirely different security assessments. If the route table is right, there is an undeclared authentication surface and a personal-data editing surface on an internet-facing host in the SSA zone, with credential handling, password reset and profile edit, and the LLD states in terms that it has been designed with "no authentication surface and no per-page permissions". If the prose is right, the LLD's own rule that no route is invented and every route is copied from the page index has been broken, and I cannot trust any route table in the document. Either way the data-residency and no-personal-data-on-the-website claims that run through all three documents rest on an unresolved contradiction. I cannot scope a security review against a system whose attack surface the designer describes two ways.

**Required.** Reconcile section 4.2 against section 7.1.2 and section 2.1.2 in one corrected issue. If the routes exist, declare the Website authentication surface, state the permission and session model for `/account/*`, state what personal data those pages hold and transmit, and withdraw the no-personal-data claim from the BRD, the HLD and the LLD. If they do not exist, remove them from the route table and state what else in the page index the tables have reproduced incorrectly.

### SC-01 (BLOCKER, ACROSS)

All three documents state the public website has no sign-in, registration or account page, and the LLD then designs a full website authentication surface in four separate places, including account pages that hold personal data.

> LLD 4.2 prose: "It carries no sign-in, registration or account page and stores no personal data: registration and all visitor data live in the mobile application and the Control Panel." LLD 4.2 route table, same section: "| `/login` | Login | Anyone | Sign-in (auth-only) |", "| `/login/verify` | OTP verify | Mid-sign-in | E-mail OTP second factor (auth-only) |", "| `/account/profile` | Account profile | Any signed-in | View/edit profile (interactive) |", "| `/account` | Home (account) | Any signed-in | Signed-in home |". LLD 3.1.2 UC-04: "Audience gate: user's surface must match (cp to Admin; web/app to Visitor)." LLD 5.1: "`PendingApproval` allowed with limited access on Web/App but refused on CP" and error code "`AUTH_WRONG_SURFACE_WEB`". BRD 2.3.2: "The public website carries informational content only: it has no user registration and stores no personal data."

**Why it affects the signature.** The website is either an anonymous read-only site or an authenticated site that edits visitor profiles. These are different systems with different PDPL data footprints, different penetration-test boundaries, different permission surfaces and different acceptance tests. I cannot certify a component whose security surface the delivery documents both assert and deny, and the denial is the basis on which the website was excluded from the personal-data controls.

**Required.** A single definitive statement of the delivered website surface, and a corrected LLD section 4.2 whose route table matches it. If `/login`, `/login/verify`, `/forgot-password`, `/reset-password`, `/account`, `/account/profile`, `/account/notifications`, `/account/pending` and `/account/rejected` are delivered, then the website must be added to the anonymous-surface statement (NFR-02), to the personal-data inventory, and to the penetration-test scope, and the BRD's website scope paragraph reissued. If they are not delivered, remove them from the table and reconcile UC-04, section 5.1 and `AUTH_WRONG_SURFACE_WEB`.

### TR-03 (MAJOR, LLD)

LLD section 4.2 contradicts itself two paragraphs apart on whether the public website has an authentication and account surface, so the delivered scope of the website is indeterminate.

> LLD 4.2 opening: "The public Website is Blazor SSR... It carries no sign-in, registration or account page and stores no personal data: registration and all visitor data live in the mobile application and the Control Panel." LLD 4.2 route table, same section: "| `/login` | Login | Anyone | Sign-in (auth-only) |", "| `/login/verify` | OTP verify | Mid-sign-in | E-mail OTP second factor (auth-only) |", "| `/account` | Home (account) | Any signed-in | Signed-in home |", "| `/account/profile` | Account profile | Any signed-in | View/edit profile (interactive) |", "| `/account/notifications` | Notifications | Any signed-in | Notification list |". The same denial is repeated in HLD 1.3 ("the website has no sign-in, no registration and no account page, and stores no personal data") and in BRD 2.3.2 ("The public website carries informational content only: it has no user registration and stores no personal data.").

**Why it affects the signature.** This is not a wording slip: it determines whether the website is an anonymous read-only surface or a credentialed personal-data surface. That decision drives NFR-02 (the anonymous surface), NFR-11 (personal data encryption), NFR-12 (data residency) and the PDPL position the HLD relies on. I cannot sign a delivery whose own detailed design gives two opposite answers about where visitor personal data lives.

**Required.** A written reconciliation stating which is correct. If the website does host sign-in, OTP, profile edit and notifications, then the BRD scope statement, the HLD system context and the HLD data-classification analysis must all be reissued, and the website's per-page permission and personal-data treatment supplied. If it does not, delete the seven contradicting routes from LLD 4.2.

### C-02 (MAJOR, LLD)

The LLD asserts three times that the public website has no sign-in, no registration, no account page and stores no personal data, and then its own route table for that website lists a login page, an e-mail OTP second-factor page, forgot-password and reset-password pages, a signed-in home, a notifications list and a profile page described as "View/edit profile (interactive)".

> LLD 4.2: "It carries no sign-in, registration or account page and stores no personal data: registration and all visitor data live in the mobile application and the Control Panel." AND LLD 2.1.2: "The Website has no such flow: it signs nobody in and holds no bearer token." VS LLD 4.2 route table, same section: "| `/login` | Login | Anyone | Sign-in (auth-only) |", "| `/login/verify` | OTP verify | Mid-sign-in | E-mail OTP second factor (auth-only) |", "| `/account/profile` | Account profile | Any signed-in | View/edit profile (interactive) |", "| `/account` | Home (account) | Any signed-in | Signed-in home |"

**Why it affects the signature.** The website is the one SIMF component sitting in the internet-facing SSA zone, and the entire data-protection argument put to me in the BRD ("The public website carries informational content only: it has no user registration and stores no personal data") and the HLD ("the website has no sign-in, no registration and no account page, and stores no personal data. The two deliberate exceptions are ...") rests on that claim. The LLD's own route table refutes it. I cannot accept a data-residency and attack-surface statement that the design document contradicts one paragraph later.

**Required.** State which is true of the delivered build. If those routes exist, withdraw the "no sign-in / no personal data" claim from BRD 2.3.2, HLD 1.3, HLD 2.2 and LLD 2.1.2/4.2, and supply for the website the authentication surface, session handling, permission model and personal-data inventory that every other authenticated surface in this package carries. If they do not exist, delete the eight rows from the LLD 4.2 route table.

### GAP-07 (MAJOR, ACROSS)

The website is stated three times to have no sign-in, no account page and no personal data, and the same LLD section then lists eight authentication and account routes on it.

> It carries no sign-in, registration or account page and stores no personal data: registration and all visitor data live in the mobile application and the Control Panel.

**Why it affects the signature.** The route table immediately below that sentence lists '/login | Login | Anyone | Sign-in (auth-only)', '/login/verify | OTP verify', '/forgot-password', '/reset-password', '/account/profile | View/edit profile (interactive)', '/account/notifications', '/account/pending' and '/account/rejected'. The BRD says 'The public website carries informational content only: it has no user registration and stores no personal data' and the HLD says 'the website has no sign-in, no registration and no account page, and stores no personal data'. The website's authentication surface, its data-protection scope and its permission model all depend on which of these is true, and the package asserts both.

**Required.** State whether the delivered website authenticates users and holds personal data. Correct whichever of the two statements is wrong across all three documents, and if it does authenticate, supply its permission model and its PDPL scope.

## T2. The acceptance criteria are not in the package

*1 BLOCKER, 5 MAJOR*

The three documents place the acceptance criteria, the page inventory, the module rules and the schema in artefacts the customer has not been given. No requirement identifier from the BRD appears in either design document, so no requirement can be followed into the design.

### SC-03 (BLOCKER, ACROSS)

The three documents are not self-sufficient: the acceptance criteria, the complete page inventory, the per-module functional rules and the physical schema are all placed in documents and code that are not part of this delivery.

> HLD Solution Architecture Section: "A per-page end-to-end test-case catalogue (Gherkin, one authored file per page) provides the executable acceptance criteria." LLD 4.1: "The following table lists Control Panel pages. It is not the complete set: the stub route `/m/{module}` (ModulePlaceholder), the nine reporting pages under `/admin/reports`, and `/admin/walk-in-mode`, `/admin/editions`, `/admin/visitors/badge-batches`, `/admin/sessions/live-hall`, `/admin/announcements`, `/admin/site-settings`, `/admin/ops/services` and `/admin/delegation-availability` are not listed. The page index carries every route." LLD 6.3.2: "Exact column types, lengths and indexes are reviewed as code in those migrations and are not reproduced column-by-column here." LLD 5: "the authoritative, exhaustive rule set for each remains its own feature design specification".

**Why it affects the signature.** I am asked to sign for a system whose acceptance criteria, whose full administrative page list, whose per-module business rules and whose ~100-table schema are each declared to live somewhere else. The contractor has named the documents that define "done"; it has not delivered them. On the delivered evidence I cannot enumerate what I am accepting, let alone test it.

**Required.** Deliver, as controlled annexes to this submission: the complete page index (every Control Panel, Website and mobile route, including the ten Control Panel routes named as unlisted); the per-page end-to-end Gherkin catalogue named as the executable acceptance criteria; the fourteen Functional Design Specifications; and a rendered data dictionary for both databases. Until they are in my hands the design documents cannot be assessed for completeness.

### SC-02 (MAJOR, ACROSS)

Neither design document cites a single BRD requirement identifier, so there is no way to verify that the delivered design covers the 107 functional, 14 business, 12 non-functional and 16 operational requirements I am being asked to accept.

> BRD 1.1: "so the analysis, design, development, testing and quality-assurance teams work from a single approved reference and each requirement can be traced into design, code and tests through a stable identifier". ABSENT: searched all three for requirement citations. `FR-[0-9]` occurs 107 times in BRD.txt, 0 times in HLD.txt, 0 times in LLD.txt. `BR-0` 9 / 0 / 0. `NFR-` 13 / 0 / 0. `OP-0` 9 / 0 / 0.

**Why it affects the signature.** The BRD states traceability as the purpose of its identifiers, and the design documents do not use them. I have no mechanical means of confirming that any given requirement was designed, and no means of detecting a requirement that was silently dropped. Every coverage question in this review had to be answered by reading and grepping, which is not an acceptance method.

**Required.** A requirements traceability matrix delivered as a controlled annex, mapping every FR / BR / NFR / OP identifier in BRD v1.3 to the HLD section, the LLD section and the named test that covers it, with an explicit "not covered" row for any requirement that is deferred, withdrawn or unbuilt.

### TR-01 (MAJOR, ACROSS)

No requirement identifier defined in the BRD appears anywhere in the HLD or the LLD, and no traceability matrix is delivered, so no requirement can be mechanically followed into design.

> BRD 1.1: "It defines the objectives and scope of the solution and records the requirements agreed with the owner, so the analysis, design, development, testing and quality-assurance teams work from a single approved reference and each requirement can be traced into design, code and tests through a stable identifier." ABSENT: searched all three files for the prefixes FR-, BR-, NFR- and OP-. The BRD defines 90 FR rows (FR-101..FR-1208), BR-01..BR-14, NFR-01..NFR-12 and OP-01..OP-16. HLD returns zero occurrences of every one of those four prefixes; LLD returns zero occurrences of every one. No document contains a requirement-to-design trace table.

**Why it affects the signature.** A certificate of completion certifies that the agreed requirements were delivered. With zero identifier linkage I cannot demonstrate, for any single FR row, which design element satisfies it, and I cannot show an auditor that the ~130 agreed requirements were all addressed rather than a convenient subset. Every coverage judgement in this review had to be made by keyword search, which is not an acceptance method.

**Required.** Deliver a bidirectional Requirements Traceability Matrix as a controlled document: one row per BRD identifier (FR, BR, NFR, OP), naming the HLD section and the LLD section/module/entity that realises it, and flagging every identifier with no design coverage. Additionally, cite the governing FR/BR/NFR/OP identifiers inline in each LLD module heading in section 5 and each HLD section, so the trace survives future revisions.

### TR-02 (MAJOR, LLD)

The LLD names itself as a secondary document and delegates its authoritative low-level content to at least eight artefacts and to the source tree, none of which form part of this delivery, so the trace terminates outside the documents I have been given.

> LLD 1.4 reference 11: "Low-Level Design (SIMF-LLD-001): the internal detailed component and data design. It is a separate document from this one (SIMF-LLD-003) and is the primary source for this document's low-level content." LLD 6.3.2: "Authoritative full schema. The complete per-column schema for all tables (approximately 100 across both databases) is defined by the single `InitialCreate` migration per context under `src/Backend/SIMF.Infrastructure/Persistence/Migrations/{Identity,App}/` and the EF Core entity configurations... Exact column types, lengths and indexes are reviewed as code in those migrations and are not reproduced column-by-column here." LLD 6: "grounded in the authoritative Data Model and Database Design (v1.2, Approved), its conventions, bounded-context model, core ERD, indexing, and Amendments A/B". LLD 5: "the authoritative, exhaustive rule set for each remains its own feature design specification."

**Why it affects the signature.** A Low-Level Design that declares another document its primary source is not a low-level design I can accept against. Only ten tables of approximately one hundred carry any column definition; for the other ninety the stated authority is source code I am contractually not reviewing at this gate. I cannot verify the delivered data model, and the customer would be accepting a schema defined nowhere in the accepted document set.

**Required.** Either (a) deliver SIMF-LLD-001, the Feature Design Specification set 001-014, Data Model and Database Design v1.2 including Amendments A and B, and the API Specification as controlled documents under this acceptance, or (b) incorporate their content into SIMF-LLD-003 so it is self-contained. In either case the full data dictionary for all ~100 tables must be in a document, not in a migration file.

### SC-19 (MAJOR, HLD)

The delivery pipeline does not run the test suites, so the delivery produces no independent evidence that the system meets its requirements, and no test results are included in this submission.

> HLD 3 Schedule and Change-Freeze Risk: "The unit, integration and end-to-end suites sit in the pipeline behind a switch that is off by owner decision, so they are run on the developer's machine before a push and a failing test does not stop the build. A per-page end-to-end test-case catalogue is maintained as the regression record." HLD 2.7: "the unit, integration and end-to-end suites are run on the developer's machine before a push".

**Why it affects the signature.** I understand the pipeline switch is the owner's own decision and I am not asking for it to be reversed. The consequence for acceptance stands regardless: nothing in the delivery process produces test evidence I can inspect, and none was submitted. "Run on the developer's machine" is an assertion, not evidence, and the catalogue named as the regression record was not delivered (see SC-03).

**Required.** Executed test evidence delivered as an acceptance artefact: the unit, integration and end-to-end run output with pass and fail counts, the date and the build identifier it was run against, plus the per-page Gherkin catalogue and a per-page pass record. A statement that a suite passes on a developer machine will not be accepted in place of the output.

### SEC-03 (MAJOR, ACROSS)

Every named compliance standard is asserted and none is substantiated; the standards made binding by the BRD appear nowhere in either design document, and the gap analysis the HLD relies on is not delivered.

> BRD NFR-01: "Security. The system shall meet the NCA Secure Application Development Standard and the controls it references (ECC-1:2018, CSCC-1:2019, the OWASP Top 10 and OWASP ASVS), and shall apply defence-in-depth across every layer." HLD 2.9: "Compliance: aligned to the NCA Secure Application Development Standard, with ECC and OWASP baselines, and to the Saudi Personal Data Protection Law (PDPL). A documented gap analysis drives the remediation programme." ABSENT: I searched all three files for ECC-1, CSCC, ASVS. "ECC-1" and "CSCC" occur twice each and only in the BRD (the glossary row and NFR-01). "ASVS" occurs twice and only in the BRD (the same two places). Neither identifier appears once in the HLD or the LLD. "OWASP" appears in the HLD only as the name of a load-balancer rule set.

**Why it affects the signature.** The word "aligned" is not a compliance position. My security authority certifies against clauses, and there is not one clause reference in 250 KB of design documentation. There is no control-to-clause mapping, no statement of which ASVS level is targeted, no OWASP Top 10 coverage statement, and the gap analysis the HLD names as the thing that drives remediation is not attached, not listed in LLD section 1.4 References, and not described. I would be presenting an unsupported assertion as if it were an assessment, and signing my name to it.

**Required.** Deliver the documented gap analysis section 2.9 cites, plus a control-to-clause traceability matrix mapping each delivered control to the specific ECC-1:2018 and CSCC-1:2019 controls and to the OWASP ASVS requirements at a named verification level, with each row marked met, partially met or not met and the residual risk stated. Cite the ASVS version and level in the same issue of the HLD.

## T3. Biometric processing nobody asked for and nobody declared

*5 MAJOR*

The delivered system performs facial and liveness processing on registrants. No BRD requirement authorises it and the HLD, which carries the data-classification and PDPL statements, never mentions it.

### SC-04 (MAJOR, LLD)

The delivered system performs facial-image processing and liveness capture on registrants, and this appears in the LLD only; the BRD does not require it and the HLD's security, data-classification and PDPL sections do not mention it.

> LLD 3.1.2 Module 2 Main Flow: "6. Identity photo verification (women-exception alternative path)." LLD 5.2: "identity photo verification (with the documented women's-alternative path)". LLD 4.3: "| `identityVerification` | Visitor (approved) | Avatar liveness capture |". LLD 7.2.6: "| FaceAiSharp.Bundle | 0.6.35 | Face processing (identity/liveness) |", "| Microsoft.ML.OnnxRuntime | 1.26.0 | ONNX model inference |", "| google_mlkit_face_detection | ^0.13.1 | Face detection (liveness) |". ABSENT: searched all three for facial / liveness processing. "liveness" occurs 0 times in BRD.txt, 0 in HLD.txt, 3 in LLD.txt; "FaceAiSharp", "Face processing" and "Face detection" occur only in LLD.txt. The BRD's only biometric reference is FR-104: "the mobile app shall additionally offer device-biometric sign-in (fingerprint or Face ID)", which is device unlock, not identity verification of the registrant.

**Why it affects the signature.** Facial data is a special category of personal data under the PDPL. The HLD's data-classification statement, which is the basis on which the ministry's technology reviewers assessed the privacy posture, says the platform holds "personal data of visitors, speakers and exhibitors" and never says biometric. The BRD's approved scope contains no requirement for it. I would be signing acceptance of biometric processing that was never requested and never disclosed to the reviewers who cleared the data classification.

**Required.** A written statement of exactly what facial processing the delivered system performs, on whom, at which step, what is stored and for how long, whether a match/comparison or only a liveness check is performed, and the lawful basis and consent text used. The "documented women's-alternative path" must be produced, since it is cited as documented and appears nowhere in this delivery. The BRD must then be amended to carry the requirement, or the capability removed.

### TR-04 (MAJOR, ACROSS)

The LLD delivers server-side facial processing and liveness detection of visitors, which no BRD requirement authorises and which the HLD never mentions at all.

> LLD 4.3: "| `identityVerification` | Visitor (approved) | Avatar liveness capture |". LLD 5.2 main flow step 6: "6. Identity photo verification (women-exception alternative path)." LLD 7.2.6: "| FaceAiSharp.Bundle | 0.6.35 | Face processing (identity/liveness) |", "| Microsoft.ML.OnnxRuntime | 1.26.0 | ONNX model inference |", "| google_mlkit_face_detection | ^0.13.1 | Face detection (liveness) |". ABSENT: searched all three files for "liveness", "face" and "biometric". The HLD returns zero hits for liveness and face processing. The BRD's only biometric requirement is FR-104: "the mobile app shall additionally offer device-biometric sign-in (fingerprint or Face ID) once the account is set up on the device" - a handset unlock, not server-side face matching. FR-207 makes the personal photo optional: "The system shall accept optional fields: job title and a personal photo."

**Why it affects the signature.** Biometric data is a special category under the PDPL, which the BRD names as a binding constraint. The HLD's data-classification analysis, on which the whole PDPL and NCA position rests, enumerates the personal data groups and never includes biometric templates or face imagery. I would be certifying that a biometric processing capability was correctly specified, assessed and lawfully based, when no requirement asks for it and the architecture document does not know it exists. The cited "women-exception alternative path" is described as documented but that rule appears in none of the three files.

**Required.** Either supply the approved business requirement authorising facial/liveness verification, together with a PDPL lawful-basis and data-protection assessment, an updated HLD data-classification section covering biometric data, retention and deletion rules, and the written women's-alternative rule; or remove the capability and its packages from the delivery. Also state whether FR-207's optional personal photo has become mandatory, since LLD 5.2 places photo verification in the mandatory main flow.

### C-04 (MAJOR, ACROSS)

The LLD makes facial photo verification a numbered step of the mandatory registration flow and ships three face-processing libraries, while the BRD lists a personal photo as an optional field and neither the BRD nor the HLD mentions facial or biometric processing anywhere.

> BRD FR-207: "The system shall accept optional fields: job title and a personal photo." AND HLD Security Requirements, PII inventory: "PII columns (national ID / Iqama / passport, mobile numbers) are encrypted at rest" VS LLD 3.1.2 Module 2 Main Flow: "6. Identity photo verification (women-exception alternative path)." AND LLD 5.2 Key Functionalities: "identity photo verification (with the documented women's-alternative path)" AND LLD 7.2.6: "| FaceAiSharp.Bundle | 0.6.35 | Face processing (identity/liveness) |", "| google_mlkit_face_detection | ^0.13.1 | Face detection (liveness) |" AND LLD 4.3: "| `identityVerification` | Visitor (approved) | Avatar liveness capture |"

**Why it affects the signature.** Biometric processing is a special category of personal data under the PDPL and it appears in exactly one of the three documents I am signing against. The BRD I approve the business scope from never asks for it, the HLD I have my technology directorate review the data classification from never lists it, and the LLD makes it a required step with a documented gender-based exception path. I cannot certify a system whose most sensitive processing activity is absent from two of its three controlling documents.

**Required.** Add facial and liveness processing to the BRD as a numbered requirement with its lawful basis, consent point and the rule behind the women's-alternative path; add it to the HLD data classification, the PII encryption inventory and the section 2.8 file-store category list; and state its retention period. Or remove the step and the three libraries from the delivered build.

### GAP-06 (MAJOR, ACROSS)

The system performs biometric facial processing on visitors. There is no business requirement for it, the HLD does not mention it at all, and the exception path for women is said to be documented somewhere I have not been given.

> 6. Identity photo verification (women-exception alternative path).

**Why it affects the signature.** The LLD packages inventory lists 'FaceAiSharp.Bundle | 0.6.35 | Face processing (identity/liveness)', 'Microsoft.ML.OnnxRuntime | 1.26.0' and 'google_mlkit_face_detection | ^0.13.1 | Face detection (liveness)', and the mobile screen table lists 'identityVerification | Visitor (approved) | Avatar liveness capture'. ABSENT: searched all three for biometric/liveness/face processing as a requirement, and the BRD contains none, its only biometric reference being FR-104 device unlock. ABSENT: searched HLD for liveness/face processing, zero hits, so it appears in neither the security view, the data classification, the encrypted file categories nor the PDPL discussion. The LLD also contradicts itself on when the step runs, placing it at registration step 6 (before approval) but attributing the screen to 'Visitor (approved)'. Biometric data is a special category under the PDPL and I cannot sign for processing that no requirement authorises and no security document describes.

**Required.** Declare the biometric capability in the BRD and the HLD: its legal basis, what is captured, where it is stored, whether it is encrypted, its retention period, whether a template leaves the device, and the full women's-alternative path. Supply the document that path is said to be recorded in.

### SC-11 (MAJOR, LLD)

Exhibitors can scan attendee badges to capture visitor records into a private list, and this personal-data flow appears in the LLD only; the BRD and HLD do not mention it.

> LLD 3.1.2: "| | Capture exhibitor lead | Exhibitor (approved) | Scan visitor badge to \"my visitors\". |" LLD 4.3: "| `myVisitors` (زواري) | Exhibitor (approved) | Captured visitors list |" and "| `scanVisitor` | Exhibitor (approved) | Exhibitor lead-capture scan |". LLD 6.3.2 as-built additions include "`ExhibitorVisitorScan`". ABSENT: searched all three for lead capture. "lead" occurs 0 times in BRD.txt, 0 times in HLD.txt, 5 times in LLD.txt; "my visitors" 0 / 0 / 1. The BRD's exhibition requirements FR-601 to FR-605 contain no visitor-scanning capability, and FR-304 grants badge scanning only to "one attendee" saving "that person as a contact".

**Why it affects the signature.** This routes attendee personal data to third-party exhibiting companies. The HLD's PDPL and data-classification analysis, on which the technology review was cleared, does not identify exhibitors as recipients of visitor data. Accepting this without a requirement means accepting an undisclosed disclosure of personal data to commercial third parties.

**Required.** State what fields an exhibitor receives when scanning a visitor badge, whether the visitor consents at the point of scan, how long the exhibitor retains the record, whether it can be exported, and the lawful basis. Add the requirement to the BRD and the disclosure to the HLD's data-classification section, or remove the capability.

## T4. Security claims the rest of the package contradicts

*6 MAJOR*

Four statements in the HLD security view describe mechanisms that every other passage, in both documents, describes differently.

### SEC-01 (MAJOR, HLD)

The HLD describes key management in two mutually exclusive ways within the same document, and the central key management service and hardware security module it relies on exist nowhere else in the architecture.

> Section 2.9: "Both keys live in the central key management service, backed by a hardware security module; no key material sits in application configuration." Solution Architecture Section, Security Requirements: "PII columns (national ID / Iqama / passport, mobile numbers) are encrypted at rest with application-level AES-256-GCM under a 32-byte operator-supplied key, Storage:UserIdDocumentEncryptionKey. Identity-document images and the other confidential stored files are encrypted at rest with AES-GCM envelope encryption under a second operator-supplied key, FileStorage:EncryptionKey". Section 2.7: "Production configuration and every secret are applied as machine-scope, SIMF-prefixed environment variables by per-site scripts ... an operator fills the secret values on the server." Section 3: "Secrets supplied only through environment variables with production boot guards."

**Why it affects the signature.** Key management is the first thing my security authority will examine, because every encryption-at-rest claim in the package depends on it. The document tells me both that no key material sits in application configuration and that two named configuration keys hold the key material. A KMS and an HSM appear in no other place in the deliverable: not in the section 2.1 per-tier sizing table, not in the section 2.7 zone allocation, not in the Communication Requirements Matrix in section 2.8, and not in the Figure 1 description. I cannot certify a control whose custodian may or may not exist, and I cannot assess key generation, storage, access control, rotation or compromise recovery from what is written.

**Required.** State which one is true and correct the other three passages in the same issue. If it is a KMS/HSM, add the KMS to the section 2.1 sizing table, the section 2.7 zone allocation and the section 2.8 Communication Requirements Matrix with its protocol, port and zone, and name the product and its FIPS validation status. If it is operator-supplied environment variables, delete the KMS and HSM sentence from section 2.9 and supply a key-management procedure covering generation, custody, access control, rotation cadence and compromise response for Storage:UserIdDocumentEncryptionKey and FileStorage:EncryptionKey.

### C-01 (MAJOR, HLD)

The HLD states in one section that encryption keys are held in a central HSM-backed key management service with no key material in application configuration, and states in three other places that every secret, including those keys, is an operator-supplied value in a machine-scope environment variable; the LLD sides with the environment variables.

> HLD 2.9 Solution Security View: "Both keys live in the central key management service, backed by a hardware security module; no key material sits in application configuration." VS HLD Solution Architecture Section, Security Requirements: "PII columns (national ID / Iqama / passport, mobile numbers) are encrypted at rest with application-level AES-256-GCM under a 32-byte operator-supplied key, Storage:UserIdDocumentEncryptionKey." AND HLD 2.7: "Production configuration and every secret are applied as machine-scope, SIMF-prefixed environment variables by per-site scripts (deploy/set-env-api.ps1, set-env-cp.ps1, set-env-web.ps1 and set-env-edge.ps1); the committed scripts carry the non-secret values, every secret entry is empty, and an operator fills the secret values on the server." AND LLD 7.2.5: "production overrides/secrets via per-host prefixed environment variables (`SIMF_API_` for the API, `SIMF_CP_` for the Control Panel, `SIMF_WEB_` for the Website and `SIMF_EDGE_` for the mobile edge ...)"

**Why it affects the signature.** An HSM-backed key management service and a plaintext environment variable on a Windows host are not variants of one control; they are different systems with different NCA and PDPL consequences, and the second is what the deployment sections and the LLD actually describe. The two statements describe different systems and I cannot certify which one was delivered. The HLD's own pre-launch list compounds it by saying production values are "issued from the key management service", so the document promises the customer a service no other section designs, budgets or names.

**Required.** Delete one of the two. Either state that PII and file-encryption keys are held in named environment variables on each host, and remove every reference to a key management service and hardware security module from 2.9 and from the pre-launch activity list; or specify the key management service by product, host, zone, protocol and port, add it to the section 2.1 sizing table and the section 2.8 Communication Requirements Matrix, and correct the Security Requirements row and section 2.7 accordingly.

### C-08 (MAJOR, HLD)

The HLD claims once that the presentation and application tiers authenticate each other with mutual TLS requiring a client certificate, while its own production prerequisites, its own communication matrix and the LLD's deployment description all describe plain HTTPS 443 with no client certificate anywhere.

> HLD 2.9: "The presentation tier and the application tier authenticate each other with mutual TLS, so reaching the API needs a client certificate and not merely a network position." VS HLD 2.7 Production prerequisites: "Issue a CA-signed TLS certificate for the published SIMF host names. The mobile client needs no change: it carries no trust-all setting and performs ordinary certificate validation." AND HLD 2.8 matrix: "| SIMF.MobileEdge / SIMF.Web / SIMF.ControlPanel | API load balancer | HTTPS | 443 | SSA to HSA |" AND LLD 7.1: "All three call the API over HTTPS 443 across the internal firewall, which admits nothing else."

**Why it affects the signature.** Mutual TLS is claimed as the reason a compromised presentation host cannot reach the API, which is a load-bearing defence-in-depth argument for putting the internet-facing tier in SSA. Nothing else in either document provisions, issues, distributes or rotates the client certificates it would require, and the LLD, which is the design document, does not mention it. I would be signing for a control that appears in one sentence and in no design.

**Required.** Either add mutual TLS to the HLD 2.7 production prerequisites (certificate authority, issuance, distribution, rotation and revocation for the presentation-tier client certificates), mark the matrix rows as mTLS, and add it to the LLD's deployment and configuration sections; or delete the sentence from HLD 2.9.

### C-09 (MAJOR, HLD)

The HLD states in section 2.5 that the audit trails are append-only at the application level, and in section 2.9 that they are append-only at the database as well with the runtime account denied update and delete; the LLD sides with application-level only and says explicitly that nothing in the database enforces it.

> HLD 2.9: "Both are append-only at the database as well as in the application: the runtime account may insert into them but may not update or delete, and every entry is also shipped to the ministry log collector, so an actor with database access cannot erase their own trail." VS HLD 2.5: "Both audit trails are append-only at the application level, so the record cannot be rewritten by the application." AND LLD 7.2.5: "both audit tables are append-only at the application level." AND LLD 6.1: "`GateScan` is an append-only audit log (bigint identity PK; append-only by application convention, nothing in the application updates or deletes a row, with no database trigger enforcing it)."

**Why it affects the signature.** The 2.9 claim is the one that answers the audit-integrity question my compliance reviewer will ask: can an actor with database access erase their own trail. Section 2.5 and the LLD say the only protection is that the application does not issue the statement, which does not constrain anyone holding a database session. Two different assurance levels are being asserted about the same tables and I cannot certify the stronger one on this evidence.

**Required.** State whether the runtime database account is denied UPDATE and DELETE on OperationLog, RowAudit and GateScan in the delivered deployment. If it is, record the grant script as a deployment step and correct HLD 2.5 and LLD 7.2.5. If it is not, delete the database-level claim from HLD 2.9 and from the section 3 Data Integrity mitigation.

### C-18 (MAJOR, HLD)

The HLD states that no client reaches the object store directly and that every read is policy-enforced by the API, then states that session recordings are served through a caching layer in front of MinIO rather than through the API nodes; the LLD gives a third answer, that the API streams the bytes itself.

> HLD 2.8, response to technical review point 6: "Access is only ever through the API, which enforces the per-category policy on every read and write. No client reaches MinIO directly." VS HLD 2.9 Sizing and Performance View: "Session recordings and other large media are served through a caching layer in front of MinIO rather than streamed through the API nodes, so a burst of viewers consumes cache bandwidth and not API capacity." AND LLD 2.2: "An administrator uploads the recording file. The API stores it in the file store and streams its bytes to the app against a stream token."

**Why it affects the signature.** This matters more than a routing preference because the same HLD section says session recordings are the category deliberately left unencrypted: "Session recordings are deliberately not encrypted at rest, because HTTP range requests (206 Partial Content) require seekable content and the authenticated-encryption mode used is not seekable; they are protected by access control instead." A caching layer in front of MinIO that bypasses the API is precisely a path around the access control that is the sole protection on that unencrypted content. Three descriptions of one path, one of which voids the compensating control.

**Required.** State the single delivered path for session-recording playback, end to end, with the component that enforces authorisation on each byte range. If a cache sits in front of MinIO, specify how it authorises a request and add it to Figure 1, the sizing table and the section 2.8 matrix; if it does not exist, delete that sentence from HLD 2.9.

### GAP-05 (MAJOR, ACROSS)

The documents give two irreconcilable accounts of where the encryption keys and secrets live, and the key management service they name appears in no sizing table, zone allocation or communication matrix.

> Both keys live in the central key management service, backed by a hardware security module; no key material sits in application configuration.

**Why it affects the signature.** The same HLD says in its Requirements table 'secrets from environment variables with production boot guards' and 'a 32-byte operator-supplied key, Storage:UserIdDocumentEncryptionKey', its Risks section says 'Secrets supplied only through environment variables with production boot guards', and the LLD says 'production overrides/secrets via per-host prefixed environment variables'. No KMS or HSM appears in the section 2.1 sizing table, the section 2.7 zone allocation, Figure 1's box list or the section 2.8 Communication Requirements Matrix. This is the control that protects national ID, Iqama and passport numbers under NFR-11 and the PDPL, and I cannot tell which of the two it is.

**Required.** State definitively whether an HSM-backed key management service exists; if it does, add it to the sizing table, the zone allocation and the communication matrix with its protocol and port; if it does not, correct section 2.9 and re-assess the NCA and PDPL position.

## T5. Personal data with no retention, deletion or lawful basis

*2 MAJOR*

PDPL is binding in all three documents and no PDPL obligation is designed: no retention schedule, no erasure path, no data-subject rights.

### SEC-05 (MAJOR, ACROSS)

PDPL is named as a binding constraint in all three documents, but no PDPL obligation is designed: there is no retention schedule, no deletion or erasure path, no data-subject rights handling, no lawful basis and no breach procedure.

> BRD 5.5: "The system must satisfy the National Cybersecurity Authority standards and the Saudi Personal Data Protection Law (PDPL)." HLD 2.9: "personal data of visitors, speakers and exhibitors, which is processed under the PDPL and the National Cybersecurity Authority controls." ABSENT: I searched all three files for "data subject" (0 hits), "erasure" (0), "right to" (0), "DPIA" (0), "anonymi" (0), "pseudonym" (0), "purge" (0), "account deletion" (0), "privacy" (0 in BRD and HLD). "breach" occurs once, in HLD section 3, only as a risk impact: "a data breach through an unprotected endpoint". "retention" occurs four times: log files ("rolling files with 31-day retention"), a worker name ("the retention and hall-attendance closeout sweeps"), and twice to say a retention period is unresolved ("its retention period is an open item").

**Why it affects the signature.** The system collects national identity numbers, Iqama and passport numbers, identity-document images, dates and places of birth, nationality, mobile numbers, facial images for liveness capture and location telemetry, from members of the general public including foreign nationals. A data protection review asks four questions first: on what basis, for how long, how is it deleted, and what happens on a breach. This package answers none of them. The BRD's only related content is FR-1207, which says the legal text is supplied by the owner, so the obligation has been passed back to us without the design that would let us honour it. There is a retention sweep worker named in LLD 2.1.3 and no retention policy anywhere for it to enforce.

**Required.** Supply a data protection annex: a per-category personal-data inventory with lawful basis, a retention schedule with a defined period for every personal-data category including identity documents, facial images, gate scans, location pings and audit trails, the design of the deletion and erasure path and what it does to soft-deleted rows and to the append-only audit trails, the data-subject access and correction path, and the breach detection and notification procedure. State what the named retention sweep worker actually enforces.

### OPS-M7 (MAJOR, ACROSS)

There is no personal-data retention or disposal schedule, and the one retention period the documents do discuss is recorded as an open item.

> GPS-presence is sensitive personal data collected only with permission and encrypted at rest, and its retention period is an open item.

**Why it affects the signature.** The only retention figure anywhere is for log files: 'structured logging via Serilog to console and rolling files with 31-day retention'. Nothing states how long the identity documents, identity numbers, mobile numbers and photographs of roughly 50,000 registrants are kept after the event, how they are destroyed, or how long the OperationLog and RowAudit trails are retained; 'disposal' returns zero matches across all three files. BRD NFR-11 and the constraints section both invoke the PDPL, and on handover I become the party answerable under it with no written rule to follow.

**Required.** A data retention and disposal schedule per data category: identity documents and images, identity and contact numbers, gate and attendance records, the two audit trails, and the application logs; including the post-event disposal step and its owner, and the GPS-presence retention decision closed.

## T6. The operations pack does not exist

*3 MAJOR*

Backup and restore is one sentence. There is no deployment runbook. The object store holding the encrypted identity documents has no redundancy scheme and no backup method of its own.

### OPS-B1 (MAJOR, HLD)

The entire backup and restore design is a single sentence with no schedule, retention, location, owner or restore procedure, and no evidence that a restore has ever been performed.

> Backups: scheduled backups of both databases and the file store support recovery. Migration order is enforced (App database before Identity) so deployments stay forward-compatible.

**Why it affects the signature.** BRD OP-04 states 'The system shall provide periodic backup and reliable restore of data.' The word 'restore' applied to data appears exactly once across all three documents, and it is that requirement itself, not an answer to it (the HLD's only 'restores' is of binaries during rollback; the LLD's is a Flutter folder name). I cannot certify OP-04 met, and on the day the contractor leaves I have no documented way to recover from data loss on either database or the object store.

**Required.** A backup and restore specification covering what is backed up (SIMF_Identity, SIMF_App, MinIO, the shared data-protection key ring and the encryption keys), full/differential/log schedules, retention periods, off-host storage location, and a restore runbook; plus evidence of at least one full rehearsal restore into staging with the measured elapsed time.

### OPS-B4 (MAJOR, LLD)

The operational content of the delivery is deferred to a 'Deployment and Operations' document that is named as a governing source but is not part of this delivery, and what remains in the three files is not a procedure my team could execute.

> 9. Deployment and Operations: environments, CI/CD, health, rollback, and observability.

**Why it affects the signature.** What is in scope is: 'The deployment pipeline stops each site, copies the new files over it and starts it again, one site at a time, in that order.' The pipeline product is never named; the procedure covers four IIS sites while the production estate is ten application hosts (four API, two web, two Control Panel, two mobile edge) with no per-node sequencing, no load-balancer drain or re-introduction step and no verification between nodes. Rollback 're-runs the smoke test', and 'smoke test' occurs once in all three documents and is never defined. The log shipper is 'performed by a host log shipper configured at deployment' with no configuration given. My team cannot deploy or roll back this system from what is written.

**Required.** Deliver the Deployment and Operations document as part of this acceptance, or fold into the HLD: the named pipeline and artefact store, an ordered per-host deployment runbook across all ten application hosts including drain and re-introduction, the contents of the smoke test, the log-shipper configuration, and the rollback procedure.

### OPS-M3 (MAJOR, HLD)

The object store holding the encrypted identity documents has no stated redundancy scheme, no node-failure behaviour, no backup method of its own and an unconfirmed sizing.

> | MinIO object store | HSA | 2 | 8 vCPU / 16 GB / 4 TB, Windows Server 2022 (proposed minimum) | Stored files over the S3 API, including encrypted identity documents |

**Why it affects the signature.** The database tier is given a named HA design (AlwaysOn AG, synchronous commit, automatic failover), but the store holding legally sensitive personal documents for roughly 50,000 registrants gets two nodes and no deployment mode; 'erasure' appears nowhere in the three documents, and the only backup reference is the shared clause 'both databases and the file store'. Its specification is marked 'proposed minimum', and the 4 TB figure has no derivation from the 19 byte-holding file categories the HLD enumerates.

**Required.** The MinIO deployment mode and redundancy scheme, the documented behaviour when one node is lost, a backup method and schedule specific to the object store, and a capacity derivation supporting 4 TB.

## T7. Requirements that cannot be delivered as written

*4 MAJOR*

Three requirements oblige behaviour the design does not provide, and one obliges a test whose pass criteria nobody has written.

### SC-15 (MAJOR, ACROSS)

FR-803 requires a push notification on a high match score, and no push transport exists anywhere in the design: the notification channels are in-app, e-mail, SMS and WhatsApp, the in-app channel is read by client polling, and no push service or library appears in any inventory.

> BRD FR-803: "When a match score reaches 80% or more, the system shall send the user a push notification recommending that person." HLD 2.9: "Live notifications and Q&A use client polling on a bounded interval of 30 seconds; no server-push transport ships in this build." LLD 5.9: "one operation (\"notify this recipient about this event type, with this content\") over four channels (in-app, e-mail, SMS, WhatsApp)" and "In-app push is read by the client from the in-app inbox over REST." ABSENT: searched all three for push infrastructure. "firebase" occurs 0 times in all three; "APNs" 0 times in all three; "push notification" occurs once, in BRD FR-803 only.

**Why it affects the signature.** FR-803 as written cannot be satisfied by a polled inbox: an attendee whose app is closed receives nothing until they next open it, which is not a push notification. Either the requirement is not met or its meaning has been silently redefined. The same doubt attaches to FR-710 session notifications and FR-902 reminders, which the event depends on reaching people in time.

**Required.** State whether the delivered mobile application receives operating-system push notifications (APNs / FCM) at all. If not, reword FR-803, FR-710 and FR-902 to say in-app inbox and e-mail, and confirm in writing that no notification reaches a closed app, so the operational teams plan around it.

### SC-17 (MAJOR, ACROSS)

FR-1208, the multi-edition configuration that is the platform's stated central objective, has no design coverage: its Control Panel page is one of the routes the LLD declines to list, and the running edition is not in the data model.

> BRD FR-1208: "The system shall let each edition's identity and settings (the event name, the colours and visual identity, the logos, and the edition's start and end dates) be configured from the Control Panel, so a new edition is set up without a code change." BRD 2.3.1: "Deliver one dynamic, configurable platform that serves the current edition and every future edition of the forum without a rebuild". LLD 4.1: "`/admin/editions` ... are not listed. The page index carries every route." LLD 3.1.2 gate engine: "(9.5 edition check: a badge issued for a closed edition is denied with `OutsideTimeWindow`; a profile with no recorded edition is left alone.)" LLD 5.12 Control Panel Configuration lists "`SystemSetting` (key/value), `OrganizationProfile`, `ContentBlock`/`Banner`, and the `RegistrationGate` singleton" and does not mention editions. LLD 6.1's `Edition` entity sits under Content & Media as the past-edition archive.

**Why it affects the signature.** The single business objective that justified the whole programme, one reusable platform for every future edition, is the one capability the design documents do not describe. Its Control Panel page is explicitly omitted, its entity is absent from the data model, and the only trace of it is an undocumented check inside the gate engine. I cannot verify the objective on which the investment rests.

**Required.** A design section covering the edition lifecycle: the entity, the `/admin/editions` page, what an edition owns (name, colours, logos, start and end dates, archiving), how opening and closing an edition affects badges, profiles, statistics and the archive, and a documented walkthrough of standing up edition five without a code change.

### SC-08 (MAJOR, ACROSS)

NFR-04 obliges the system to pass a load test before launch, and no document states the pass criteria; both design documents defer the thresholds to the contractor, after acceptance.

> BRD NFR-04: "Performance. The system shall sustain the event-day peak load, about 30,000 concurrent attendees at peak, roughly 50,000 registered at a 0.6 peak factor. The system shall pass a realistic load test against that peak before launch." HLD 2.9 Solution Sizing and Performance View: "Load-test pass/fail thresholds (p95 response time, error rate, concurrent sessions and throughput) are set during staging load testing." LLD 1.2.1 Out of scope: "Load-test thresholds and the monitoring/alerting toolchain, unset in the source."

**Why it affects the signature.** A requirement whose pass criterion is set by the supplier after the customer has signed is not a requirement I can accept. The HLD elsewhere quotes targets (400 ms sign-in, 250 ms gate scan, 300 ms read, under 75% CPU) but does not bind the load test to them, and the LLD places the thresholds out of scope entirely. On this wording any load-test result can be declared a pass.

**Required.** Numeric, binding load-test pass/fail thresholds agreed before the test is run: p95 and p99 response time per journey (sign-in, gate scan, read, seat reservation, question submission), maximum error rate, sustained concurrent sessions and throughput, and the CPU ceiling; plus the test plan and the date the staging rehearsal runs. Acceptance of NFR-04 is withheld until the executed report against those numbers is delivered.

### TR-05 (MAJOR, ACROSS)

The LLD module that owns Control Panel configuration explicitly refuses the requirement that defines the programme's headline objective: per-edition configuration of colours, visual identity and logos without a code change.

> BRD FR-1208: "The system shall let each edition's identity and settings (the event name, the colours and visual identity, the logos, and the edition's start and end dates) be configured from the Control Panel, so a new edition is set up without a code change." BRD 2.3.1: "Deliver one dynamic, configurable platform that serves the current edition and every future edition of the forum without a rebuild, with everything that changes between editions (name, colours, logos, content, categories, start and end dates, archiving) set from the Control Panel." LLD 5.12 Business Rules: "This module manages content and per-category colours only: it does not change the brand palette or typeface fixed by the visual identity and the theme tokens." HLD 2.6 confirms the same: "colours and typography come from `theme.tokens.css`."

**Why it affects the signature.** BRD section 2.1 gives the entire business case for this procurement as ending the per-edition rebuild. FR-1208 and NFR-10 are how that case is made testable. The design states that the brand palette and typeface are fixed in a stylesheet, so re-skinning the next edition requires a release - the exact cost the platform was bought to remove, and one the BRD's post-publish change freeze makes worse. This is a refusal of the requirement, not a design preference I am entitled to overrule.

**Required.** State in writing which parts of FR-1208 are delivered and which are not. If the brand palette, typeface and logos are not Control-Panel configurable, FR-1208 must be formally re-scoped and re-approved by the owner before I can sign, and the LLD must specify the exact release procedure and effort to change edition branding. If they are configurable, correct LLD 5.12 and name the settings and the CP page that carry them.

## T8. The delivery state of three things is unstated

*3 MAJOR*

A module described as partly built, a fourth client with no network path, and an anonymous endpoint serving a withdrawn feature.

### SC-07 (MAJOR, LLD)

The Business Meetings module is delivered in an unstated condition: the LLD calls part of it "partly built" and part "build-ready", while listing its endpoints, Control Panel pages and mobile screens as though delivered.

> LLD 5.13: "The v1.1 draft adds an attendee-initiated speaker/VIP request path with a hall-availability gate and a speaker double-opt-in e-mail, partly built, the rest owner-resolved and build-ready." LLD 3.1.2 module index: "attendee meeting-in-hall request then admin review vs hall availability then speaker double-opt-in (Visitor, build-ready)". The same section lists as exposed: "admin `/admin/meeting-tables`, `/admin/business-meetings`, `/admin/speaker-availability`, `/admin/speaker-meeting-requests/{id}/respond`, and app `POST /app/speakers/{id}/meeting-requests`, `GET /app/speakers/{id}/available-slots`, `POST /app/delegation-meeting-requests`", and LLD 4.1 lists `/admin/speaker-meeting-requests`, `/admin/speaker-availability`, `/admin/hall-availability` and `/admin/delegation-meetings` as Control Panel pages.

**Why it affects the signature.** "Partly built" and "build-ready" are not delivery states I can sign against. The module simultaneously has live Control Panel pages, live app screens, live endpoints and a declaration that part of it is not built. If I sign, I have accepted whatever exists, including half-wired screens that an administrator can open and an attendee can reach.

**Required.** A built / not-built table for every function in section 5.13 and its v1.1 path, naming per function the endpoint, the Control Panel page, the mobile screen and the delivery state. Anything not built must be disabled and removed from the page and screen catalogues before handover, not left reachable.

### C-05 (MAJOR, HLD)

The HLD introduces a fourth client, a WinForms badge desk that posts to the admin API, but the internal firewall it specifies admits the presentation zone alone, the badge desk appears in no zone and in no row of a matrix that claims to carry every flow, and it authenticates with a raw bearer token that the same document says the Control Panel never exposes.

> HLD 2.1: "The internal firewall is the only route into it and permits TCP 443 from the presentation zone alone, so it is also the only barrier in front of the databases and the file store." AND HLD 2.8: "Communication Requirements Matrix. Every flow on Figure 1, with its protocol and port." (the matrix carries no badge-desk row) AND HLD 2.4: "Browser sessions hold encrypted authentication cookies and never raw bearer tokens." VS HLD 2.2: "Badge desk (SIMF.BadgeDesk, WinForms), a Windows desk application that staff run at the venue to print and issue badges. It works offline and holds no database connection. It posts each shift to the API over HTTPS, to POST /api/v1/admin/offline/batch, authenticated with a bearer token that the operator pastes from a Control Panel session."

**Why it affects the signature.** A delivered client that writes to the admin surface has no permitted network path in the architecture that is being certified, no row in the matrix my network team will build firewall rules from, and no zone in Figure 1. Worse, its authentication method requires the Control Panel to hand a human a raw bearer token, which contradicts the token-handling control stated two sections earlier and defeats the BFF pattern the LLD relies on. The LLD compounds it: LLD 2.1.1 says "Four clients call it" while LLD section 4 opens "SIMF presents three distinct clients over the one backend API" and catalogues no badge-desk screen at all.

**Required.** Place SIMF.BadgeDesk in a named zone on Figure 1, add its flow to the section 2.8 Communication Requirements Matrix with source, destination, protocol, port and direction, and state the firewall rule that lets it cross into HSA. Replace the pasted-bearer-token mechanism with a credential that does not require exposing a raw token to an operator, or state explicitly that the "never raw bearer tokens" control does not hold for the Control Panel. Add the badge desk to LLD section 4 or remove it from the delivery.

### C-07 (MAJOR, ACROSS)

The BRD limits the anonymous surface to public content and pre-token authentication endpoints, but the LLD lists an unauthenticated endpoint reaching the on-site language model that the HLD says belongs to a withdrawn feature and that no client calls.

> BRD NFR-02: "Authorisation. Every administrative endpoint and screen shall require a permission. The anonymous surface is limited to public content on the website and in the app, and to the authentication endpoints that run before a token exists." VS LLD 2.1.5: "`AllowAnonymous()` for the public read endpoints, for the authentication endpoints that run before the caller holds a bearer token, for the single-use speaker action-token endpoint, for the public contact form `POST /app/contact-inquiry`, and for the two AI endpoints `POST /app/ai/faq` and `POST /app/ai/translate`" AND HLD 2.4: "The API also retains two endpoints from the withdrawn live-translation and sign-language feature; no client calls them, and anything they carry reaches the same on-site endpoint and goes no further."

**Why it affects the signature.** `POST /app/ai/translate` is, on the package's own evidence, unauthenticated, reachable from the internet through the mobile edge, wired to the GPT OSS 120B inference server inside HSA, and serves no delivered feature. That is an anonymous path into the most expensive compute in the estate, retained for a feature that was withdrawn, and it is outside the anonymous surface NFR-02 defines. The BRD's limit and the LLD's list are not the same set, so the NCA anonymous-surface statement I would be certifying is untrue as written.

**Required.** Remove the endpoints of the withdrawn live-translation and sign-language feature from the delivered API. For each remaining anonymous entry point beyond public content and pre-token authentication (the speaker action-token endpoint, `POST /app/contact-inquiry`, `POST /app/ai/faq`), record a per-entry justification against NFR-02 and either amend NFR-02 to cover them or gate them.

## T9. Conditions precedent to final acceptance

*1 MAJOR*

Named pre-launch activities and four BRD open items are unclosed at the point the certificate is requested.

### OPS-M10 (MAJOR, ACROSS)

Named pre-launch activities and four BRD open items remain unclosed at the point I am asked to sign, and two of them are functional requirements.

> Pre-launch deployment activities. Four activities are scheduled between acceptance of this design and go-live. Each can only be carried out on the production estate, which is why none is closed at design time.

**Why it affects the signature.** I credit the disclosure: each activity carries an owner and a closing condition, including 'The owner commissions an independent penetration test, closed on acceptance of its report', and that is proper practice rather than a gap. The acceptance problem is timing. Signing a certificate of completion now accepts a system whose independent penetration test has not been performed, whose secrets present in development configuration history have not been rotated, and whose identity-lifecycle controls are still disabled; and BRD 5.7 still carries OI-1 against FR-1006 and FR-1104, meaning two functional requirements are open at BRD v1.3.

**Required.** Acceptance recorded as conditional, with the certificate naming these items and the conditions precedent to final acceptance: the penetration-test report accepted, the four pre-launch activities closed against their stated conditions, and OI-1 closed so FR-1006 and FR-1104 have a fixed field set and statistics list.

## Every other surviving finding

The findings above are the ones that carry the verdict. The remainder are
recorded so none is lost.

| Id | Severity | Doc | Finding | Required |
|---|---|---|---|---|
| C-12 | MINOR | HLD | The hardware specification reproduced from the customer server requirements workbook budgets two load balancers in one active/passive pair, while the section 2.1 sizing table in the same document specifies t... | Correct the Hardware Specification paragraph to four load balancer units in two active/passive pairs, one in the SSA perimeter zone and one in HSA, and state whether the second ... |
| C-13 | MINOR | ACROSS | The BRD says a registering user chooses Visitor or Other and that an administrator records the exhibiting organisation in the Control Panel, while the LLD documents Exhibitor as a third self-service registra... | State whether the delivered app offers Exhibitor as a self-service registration type. If it does, amend FR-201 and the BRD business process, and specify the vetting applied to a... |
| C-14 | MINOR | LLD | The Notifications module description makes per-channel delivery records the mechanism for multi-channel send, failure capture and retry, and the data dictionary then says the per-channel delivery model exist... | State whether a NotificationDelivery table exists in the delivered SIMF_Identity or SIMF_App schema. If it does not, rewrite LLD 5.9 to describe the delivery, failure-capture an... |
| C-16 | MINOR | HLD | The HLD requires a Staging environment that mirrors the production topology and is where the mandatory pre-launch load test runs, while its hardware specification budgets only Production and a single-node De... | Add the Staging environment to the Hardware Specification with its node counts and per-node specification, or state plainly that Staging shares the Production estate or is scale... |
| C-22 | MINOR | ACROSS | BRD FR-807 is headed "Withdrawn" yet still promises that the app offers a sign-language feed beside the main feed, while the LLD says the feature is withdrawn from the delivered scope with only the column an... | Delete the final sentence of FR-807, or deliver it. If the Control Panel field that writes `LiveSignLanguageFileId` is retained while no client reads it, say so in FR-807 so tha... |
| C-25 | MINOR | LLD | The key-table data dictionary contradicts itself on nullability and column types, and mints a state field, AdmissionState, that is used once and defined nowhere in the package. | Correct the Null column for `UserProfile.UserId` and `QrId`, give `DenialReasonCode` one type, and either define `AdmissionState` with its values and its relationship to `Accoun... |
| C-29 | MINOR | ACROSS | The BRD requires registration to close automatically at the end of the last forum day, while the LLD closes it at an arbitrary administrator-set date and time. | State whether the auto-close time defaults to the configured edition end date. If it does not, either default it or amend BR-14 and FR-216 to say the close time is set manually ... |
| GAP-43 | MINOR | LLD | The per-channel notification delivery model is described in the present tense as behaviour, and the data-model note says it exists only in the logical model. | State whether NotificationDelivery exists in the delivered schema. If it does not, correct section 5.9 and state how a failed channel send is recorded and retried. |
| GAP-46 | MINOR | LLD | The data dictionary contradicts itself on the nullability of the two most load-bearing columns on the visitor profile. | Correct the nullability column for QrId and UserId, and reconcile the data dictionary with the ERD statement about profiles without user rows. |
| GAP-61 | MINOR | BRD | The BRD gives two different dates for its own first issue. | Correct the version history so the two tables agree on the v1.0 issue date. |
| OPS-M4 | MINOR | HLD | Several load-bearing infrastructure decisions are written in a form that reads as settled while describing work that has not been done, with no owner or date. | The quorum witness named with its placement agreed and recorded; the mobile-edge and MinIO specifications confirmed in writing; and any of the three that remain open moved onto ... |
| SC-09 | MINOR | ACROSS | An entire delegations capability is designed and delivered with no business requirement: delegates, invited countries, delegation meetings, delegation availability, a public delegations screen and a per-user... | Either a BRD amendment adding the delegations requirement set (who is a delegate, who marks a country invited, what the delegation meeting workflow is, who approves it, what is ... |
| SC-10 | MINOR | ACROSS | The Business Meetings module (B2B/B2C meetings, meeting tables, hall allocation, speaker meeting requests) has no requirement in the BRD, whose only meeting requirement is the one-to-one request routed to PR. | A BRD amendment carrying the business-meetings requirement set and reconciling it with FR-804 and BR-09, or a statement that the module is withdrawn. State explicitly whether th... |
| SC-12 | MINOR | LLD | Six further Control Panel surfaces and the nine reporting pages are delivered with no business requirement in the BRD. | Add each of these surfaces to the BRD requirement set and to the LLD page table with its purpose, its permission code, its workflow and its acceptance criteria, or confirm in wr... |
| SC-13 | MINOR | ACROSS | The sign-language feature is declared withdrawn in all three documents while the same documents describe it as delivered: the BRD's own withdrawal row states the app offers the feed, and the schema column, t... | State whether the delivered mobile app renders the sign-language feed beside the main feed. Rewrite FR-807 so the withdrawal text does not describe delivered behaviour. Remove t... |
| SC-14 | MINOR | ACROSS | The delivered anonymous API surface includes an AI translation endpoint that the HLD says no client calls, for a feature the BRD withdrew. | State whether `POST /app/ai/translate` is one of the two retained withdrawn-feature endpoints, name the other, and either remove both from the delivered build or justify each ag... |
| SC-16 | MINOR | ACROSS | FR-802's partner directory and its Control Panel management of discoverable profile types are not designed anywhere, and the nearest capability in the LLD is described as planned. | Either the design of the partner directory, its opt-in mechanism, its Control Panel management page for discoverable profile types and the enforcement of the Visitor-exclusion r... |
| SC-18 | MINOR | ACROSS | An external system named Mawj receives VIP personal data through an export, and it appears once in the LLD; the HLD's external-entity inventory states that the only external destinations are YouTube. | Identify the Mawj system, its owner and its purpose; state exactly which fields leave SIMF in the VIP export, in which formats and by what route; add it to the HLD's external-en... |
| SC-21 | MINOR | ACROSS | The HLD claims mutual TLS with client certificates between the presentation and application tiers; the LLD describes the same calls with no client certificate, and the production prerequisites provide for no... | Confirm in writing whether mutual TLS with client-certificate authentication is delivered and enabled between SIMF.Web / SIMF.ControlPanel / SIMF.MobileEdge and the API. If yes,... |
| SC-26 | MINOR | LLD | The gate engine's own module section describes two constraint checks as active and then lists their denial codes as reserved, so I cannot tell whether a badge from a closed edition or an attendee without a b... | Confirm which of steps 9.5 and 11.5 execute in the delivered build and whether `OUTSIDE_TIME_WINDOW` and `BOOKING_REQUIRED_MISSING` are emitted, and correct section 5.3. Supply ... |
| SC-27 | MINOR | ACROSS | The contractor excludes remediation of secrets it committed to its own configuration history from the deliverable and transfers the work to the customer. | An inventory of every secret present in the delivered configuration history, stating what it protects and whether it was ever used in a production or pre-production environment,... |
| SC-30 | MINOR | LLD | The data dictionary contradicts itself on the nullability of two load-bearing columns. | Correct the Null column on both rows and re-check the whole data dictionary for the same defect, or deliver the rendered schema so the dictionary is no longer the only source. |
| SC-31 | MINOR | HLD | The hardware specification derived from the customer workbook lists two load balancers, and the sizing table in the same section lists two pairs, four instances. | Reconcile the hardware specification paragraph with the sizing table and state the total load-balancer count and placement the site must provision, flagging the API load-balance... |
| SEC-09 | MINOR | LLD | GateScan, a third audit trail carrying the physical access-control record, is append-only by convention only, with nothing in the database preventing alteration. | Enforce immutability on GateScan at the database, by revoking UPDATE and DELETE from the runtime principal or by a trigger, and supply the statement. If GateScan is to remain co... |
| SEC-10 | MINOR | ACROSS | Mutual TLS between the presentation tier and the application tier is claimed once and is unsupported by the transport description, absent from every row of the Communication Requirements Matrix, and absent f... | Confirm whether mutual TLS is deployed. If it is, add the client-certificate requirement to the relevant rows of the Communication Requirements Matrix in section 2.8, name the i... |
| SEC-11 | MINOR | ACROSS | "Upload scanning" is listed twice as a delivered hardening control, but no malware scanning of any kind is described anywhere; the actual upload design is file-type sniffing and a size cap. | Either name the anti-malware engine, where it runs in the upload path, what it does on a positive detection and how its signatures are updated inside HSA with no internet path, ... |
| SEC-12 | MINOR | ACROSS | Encryption at rest is materially narrower than the business rule states, database-native encryption is explicitly disclaimed, and nothing is said about protecting the backups. | Correct BR-13 to match the design, or extend the encryption to cover it. Supply the definitive list of which personal-data columns are encrypted and which are not, with the just... |
| SEC-14 | MINOR | ACROSS | Location telemetry is collected and stored as sensitive personal data although the BRD places the capability out of scope, its retention period is an open item, and its claimed encryption at rest is supporte... | State whether location telemetry is collected in the delivered system. If it is, remove the contradiction with BRD 2.3.3, fix a retention period for both the raw pings and the r... |
| SEC-15 | MINOR | ACROSS | Two AI endpoints are reachable without authentication on the internet-facing app surface, and one of them belongs to a feature that has been formally withdrawn from scope yet is retained live. | Remove the endpoints of the withdrawn FR-807 feature from the deployed API, or gate them behind authentication. For the AI endpoints that remain anonymous by design, state the j... |
| SEC-16 | MINOR | HLD | The data-protection key ring that protects Control Panel administrator session cookies is a folder shared read-write across four hosts, with no protection of its own stated. | Specify how the key ring is protected: encryption at rest and by what mechanism, the filesystem access control and which service accounts hold it, the key lifetime and rotation,... |
| SEC-17 | MINOR | HLD | The badge desk is a fourth client that posts to the administrative API with a bearer token pasted by an operator, and it appears in no zone of the network model and in no row of the Communication Requirement... | Place the badge desk in a zone, add its flow to the Communication Requirements Matrix with protocol, port and direction, and replace the pasted-token pattern with a device crede... |
| SEC-18 | MINOR | ACROSS | The RS256 token-signing private key has no stated lifecycle, and the distribution of the public half to the verifiers the design relies on is never described. | Supply the signing-key lifecycle: generation method and entropy source, storage and access control on the four API hosts, rotation cadence and the procedure for rotating without... |
| SEC-24 | MINOR | LLD | A second authentication scheme, StreamToken, is registered on the API and is specified nowhere. | Specify the StreamToken scheme: issuance, binding, lifetime, scope, revocation, and which resources it grants access to, together with the justification for a second authenticat... |
| TR-09 | MINOR | ACROSS | FR-802's partner directory and its Control-Panel-managed discoverability model are designed nowhere; the LLD's only related sentence describes such a directory as "planned". | Deliver the design for the partner directory, the per-attendee opt-in flag on the profile, the Control Panel page that manages the discoverable profile types, and the enforcemen... |
| TR-13 | MINOR | ACROSS | Three substantial delivered modules - Business Meetings, the Track 1 shared Contact directory, and delegations - have no requirement of any kind in the BRD. | For each of Business Meetings, the Track 1 Contact directory, delegations and speaker meeting availability, supply either the approved change request that added it to scope or t... |
| TR-16 | MINOR | ACROSS | Two of the four open-item identifiers are never carried into the design documents, one identifier in the sequence is undefined, and the G-OI-2 identifier collides with OI-2 in any text search. | Renumber the open items into one unambiguous sequence with no gap and no colliding prefixes, account for OI-3 explicitly, and cite the OI identifier at every point in the HLD an... |
| TR-17 | MINOR | HLD | The High Level Design carries no document identifier and no version number of its own, and the LLD that sits under it cannot cite it either, so there is no unambiguous designation for the HLD on a certificat... | Reissue the HLD with a document identifier, a version number, a revision history and an approvals block matching the BRD and LLD conventions; correct the project name field from... |
| TR-18 | MINOR | LLD | The LLD delivers GPS presence collection as a built capability, contradicting both the BRD's out-of-scope declaration and the LLD's own out-of-scope section, and its retention period is undecided. | State unambiguously whether the DevicePositionPings table exists in the delivered schema and whether any client writes to it. If it does not, remove it from LLD 5.11 and 6.3.2. ... |
| TR-19 | MINOR | LLD | The data dictionary in LLD 6.3.1 contradicts itself on four columns, so the delivered schema cannot be established even for the ten tables the document does describe. | Correct all four entries so the nullability flag, the type, the constraint and the prose agree, and re-verify the remaining six tables in LLD 6.3.1 against the same check before... |
| TR-20 | MINOR | ACROSS | Figure numbering collides between the HLD and the LLD - the same three diagrams are numbered in reverse - and the LLD refers the reader to sequence diagrams it does not contain. | Adopt document-prefixed figure numbering (HLD-F1, LLD-F1) or align the numbering, and correct LLD 2.2 to cite the HLD figures explicitly rather than "elsewhere in this document". |
| TR-21 | MINOR | ACROSS | Three testable non-functional acceptance criteria - the date display format, browser keyboard operability, and the supported mobile operating-system versions - appear in no design document. | State the minimum supported Android and iOS versions and the supported browser list in the HLD, and record the dd-MM-yyyy Latin-digit date rule and the keyboard-operability rule... |
| TR-22 | MINOR | ACROSS | FR-1207's Policies document has no presentation surface anywhere in the design; only the Terms and Conditions are addressed, and the HLD does not mention either. | Name the route, screen and content-block key on which the Policies are presented in the app, the website and the Control Panel, or confirm that FR-1207 is only half delivered so... |
| TR-24 | MINOR | LLD | The LLD contains identifiers and rule sets that are referenced but defined nowhere in the three documents. | Define Page_014, state the location-privacy rules in the LLD, and reproduce the women's-alternative identity-verification path in the design, or remove each reference. |

## Order of work

1. **T1**, the website surface. A day's work, and every other item is
   scoped against the answer. Produce the annexes against the wrong website
   scope and they are produced twice.
2. **T4**, the contradicted security claims. These were introduced into the
   HLD without being carried into the LLD, so they are a documentation
   defect with a known cause and a bounded fix.
3. **T3**, the undeclared biometric processing. It is the item most likely
   to stop a data-protection review, and it needs a business decision, not
   an edit.
4. **T2**, the annexes. Weeks of work; start in parallel once T1 is answered.
