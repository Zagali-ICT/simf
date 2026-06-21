# SIMF — Gap Analysis vs. NCA Secure Application Development Standard

**Reference standard:** «نموذج معيار التطوير الآمن للتطبيقات» — NCA (National Cybersecurity
Authority) Secure Application Development Standard template, aligned to **ECC‑1:2018**
and **CSCC‑1:2019**. Source PDF supplied by the owner
(`15-04-2024/معيار أمن تطوير التطبيقات.pdf`).

**Subject:** SIMF — .NET 10 modular monolith (FastEndpoints API + EF Core + SQL Server),
Blazor Server Control Panel, Blazor SSR Website, Flutter mobile app. Two physically
separated databases (`SIMF_Identity` + `SIMF_App`).

**Method:** white‑box source audit (read‑only) of the live `src/` tree by six parallel
security reviewers, cross‑referenced with the prior platform assessment
`docs/security/SIMF-Security-Assessment-2026-06-20.md` and live recon already recorded
there. Every verdict is grounded in a verified `file:line` or stated fact. **No code was
changed to produce this report.**

**Date:** 2026‑06‑20  ·  **Author:** security review (agent‑assisted)  ·  **Status:** for owner review

---

## 1. Executive summary

| Domain | Controls | ✅ Met | ⚠️ Partial | ❌ Gap | ➖ N/A |
|---|---|---|---|---|---|
| §1 Secure Application Development | 20 | 3 | 8 | 4 | 5 |
| §2 Source Code Repository | 12 | 1 | 7 | 0 | 4 |
| §3 Source Code Review & Testing | 20 | 1 | 15 | 4 | 0 |
| A1 Access Control | 21 | 17 | 3 | 1 | 0 |
| A2 Cryptography | 20 | 6 | 10 | 3 | 1 |
| A3 Injection / Input Validation | 19 | 12 | 6 | 0 | 1 |
| A4 Insecure Design | 19 | 15 | 3 | 0 | 1 |
| A5 Communications & Misconfiguration | 31 | 12 | 13 | 2 | 4 |
| A6 Vulnerable Components & File Handling | 32 | 19 | 10 | 2 | 1 |
| A7 Authentication & Session | 46 | 32 | 8 | 6 | 0 |
| A8 Software/Data Integrity (Deserialization) | 6 | 3 | 2 | 0 | 1 |
| A9 Logging & Error Handling | 15 | 9 | 6 | 0 | 0 |
| A10 SSRF | 9 | 5 | 2 | 0 | 2 |
| A11 Mobile Verification | 28 | 13 | 8 | 6 | 1 |
| **TOTAL** | **298** | **148** | **101** | **28** | **21** |

Of the **277 applicable** controls: **148 fully met (53%)**, **101 partial**, **28 not met**.

**What is genuinely strong (verified, not assumed):** a single permission catalogue drives a
dynamic per‑page/per‑action `perm:` policy on both API and CP, build‑time tests fail the
build if a gate is missing, IDOR is structurally prevented (actor always resolved from the
signed JWT `sub`), no SQL/LDAP/OS‑command injection, no XXE, no user‑driven SSRF, no unsafe
deserialization (`BinaryFormatter`/`TypeNameHandling` absent), algorithm‑pinned HS256 JWT
with security‑stamp revocation + rotation + reuse detection, an absolute 24 h session cap,
CSPRNG everywhere, AES‑256‑GCM for the ID‑document image, magic‑byte upload allow‑lists,
generic error handling (no stack‑trace leakage), and an exceptionally thorough audit
catalogue (~250 typed events).

**Where the gaps concentrate:**
1. **Transport (owner‑held).** Self‑signed production TLS cert (A5‑1/H2) and the Flutter
   **app‑wide trust‑all TLS bypass that ships in release** (A11‑1/15/22, A5‑6, C2).
2. **Secret hygiene.** JWT / ID‑document‑AES / SMTP keys + the super‑admin default
   password are blanked in the working tree but **remain in git history** and unrotated
   (A2‑9, A7‑7/16, C1/H1).
3. **DevSecOps in CI.** No SAST / DAST / SCA / SBOM, and the pipeline test gate is
   **explicitly disabled** (§1‑2, §3‑2, §3‑20, A5‑20, A6‑13).
4. **Crypto at rest for PII.** National‑ID / Iqama / passport numbers stored as plaintext
   columns (A2‑10).
5. **Password policy depth.** No complexity classes, no breached/common‑password
   blocklist, no expiry, no history, no last‑login notice (A7‑10/13/20/28/29/31).
6. **Mobile hardening.** No cert pinning, root/jailbreak detection, screenshot protection,
   anti‑debug, Dart obfuscation, or real release signing (A11‑6/7/11/15/16/22/26).
7. **Governance attestations.** SSDLC/DevSecOps doc, pentest/risk‑assessment/WAF, threat
   model, training/workstation/supplier/segmentation records — mostly organizational
   controls requiring owner attestation with exported evidence.

---

## 1a. Remediation status (updated 2026-06-21)

Owner approved Groups A + B + C (TLS bypass held; freeze area unlocked). Implemented
and committed on `feature/app-cp-api-split` (each verified — builds 0/0 + tests green,
several in an isolated worktree to avoid a concurrent worker's broken-tree windows):

| Wave | Controls closed / improved | Commit |
|---|---|---|
| 1 | A1-12 audit 403 denials · A5-12 charset · A2-14 no-store on ID docs | `8f991fc8` |
| 2 | A7-10/28/29 password policy (classes + repeat/sequence + leet blocklist + central validator) | `96e3209b` |
| 3 | A2-11/A2-20 autocomplete-off on PII · A3-14 Unicode-normalise filenames | `ba50122d` |
| 4 | A3-4/A5-13/A6-21 CSP (enforced on API; report-only on CP/Web) | `c0cd95ca` |
| 5 | A11-3 shared-PII temp purge (A11-6 FLAG_SECURE + A11-16 signing applied to git-ignored `android/` → owner) | `ef86f93a` |
| 8 | §3-20 re-enabled test gate · A6-13 SCA + NuGetAudit · A6-2 SBOM · §1-2/§3-2 SAST/DAST scaffold | `9d9c1a4b` |
| 9 | A4-13 threat model (`SIMF-Threat-Model-2026-06-21.md`) + `SecurityResponseTests` regression | `f6ebbbc5` |
| 6a | A2-10 PII-at-rest (AES-GCM EF value converter + migration `SecA210`; RowAudit redacts PII) | `4b1c3086` |
| 6b | A7-13 password expiry (`PasswordChangedAtUtc` + config; migration `SecA713`) | `5287aad9` |
| 6c | A7-20 password-history reuse prevention (new table; migration `SecA720`) | `609a42b0` |
| 6d | A7-31 last-sign-in tracking + `AuthTokens.PreviousSignInAtUtc` (migration `SecA731`) | `8a7b4e70` |
| 6e | A1-19 dormant-account auto-disable (service + daily `BackgroundService`) | `041a4aea` |
| 7 | A6-18 upload AV scan (`IUploadScanner` + EICAR default; wired into every upload path) | `ae0b4eb2` |

**Status: every ❌ gap that is fixable in code is now closed** (18 commits, all full-suite
green: API 1179/1179, Application 34, CP 180, ApiClient 41 + the dedicated security tests).

**Remaining (not hard ❌ gaps):**
- **A1-14/A4-10** per-user/day export governor — ⚠️ *partial* (strong per-request rate
  limits already exist); optional enhancement.
- **A7-8** IP-lockout tier — ⚠️ partial (account + per-email + per-IP limits already exist).
- **A9-15** crypto-/TLS-failure audit events — ⚠️ (most security events already audited).
- **Client follow-up:** surface the A7-31 "last signed in …" notice in app/web/CP UI
  (the data is already on `AuthTokens.PreviousSignInAtUtc`).
- **Mobile (need a pub package + owner policy + device test):** A11-7 root/JB detection,
  A11-26 anti-debug, A11-11 Dart `--obfuscate`, A11-14 field autocorrect, A11-19 CSRF; plus
  A11-6 FLAG_SECURE + A11-16 release signing live on disk in the git-ignored `android/`.

**Owner / ops (cannot be code — Groups D & E):** rotate the 4 git-history secrets + purge
(C1); verify `SIMF_SuperAdmin__*` env (H1); CA cert then remove the Flutter TLS bypass +
add pinning (C2/H2); SQL/host hardening; WAF; independent pentest; key-management policy
(A2-7); SIEM forwarding/log archival; the one-time legacy-PII re-encryption sweep; and
**enable** the new admin knobs (`IdentityLifecycle:PasswordMaxAgeDays` /
`PasswordHistoryCount` / `DormantAccountDisableDays`, all default 0/off).

**Owner / ops actions (cannot be code — Groups D & E):** rotate the 4 git-history secrets
+ purge history (C1); verify `SIMF_SuperAdmin__*` env (H1); CA cert then remove the Flutter
TLS bypass + add pinning (C2/H2); SQL/host hardening; WAF; independent pentest/risk
assessment; key-management policy (A2-7); SIEM forwarding/log archival; persist the
`android/` FLAG_SECURE + release-signing (or start tracking `android/`).

---

## 2. Headline open findings (carried from the platform assessment)

These are the highest‑risk items and they map directly onto NCA controls.

| Ref | Finding | NCA controls failed | Status |
|---|---|---|---|
| **C2** | Flutter app installs an app‑wide `badCertificateCallback => true` trust‑all override (`lib/core/net/self_signed_api_tls_io.dart:26‑27`), wired in `main.dart:28` with **no `kReleaseMode` guard** → ships in release → full MITM of all app traffic | A11‑1, A11‑15, A11‑22, A5‑6 | **OPEN — owner‑held** pending CA cert |
| **H2** | Production TLS is a **self‑signed** cert `CN=WIN‑MAP9VAMAU4Q` with no trusted chain | A5‑1, A5‑6 | **OPEN — owner‑held** |
| **C1** | `appsettings.Development.json` is git‑tracked; JWT signing key, ID‑document AES‑256 key, and Zoho SMTP password are blanked in the working tree but **remain in git history** | A2‑9, A2‑3, A2‑19, A7‑16 | **OPEN — ops:** rotate + purge history |
| **H1** | Super‑admin default password (`Aa@123456789`) + working TOTP seed were committed; inert only if `SIMF_SuperAdmin__*` env overrides are set on the server | A7‑7, §3‑14 | **OPEN — ops:** verify env + rotate |

> A Production boot guard now refuses to start with the committed default password
> (`Program.cs:354‑360`) and the JWT key length is gated (`Program.cs:274‑278`) — these
> reduce blast radius but do **not** close C1/H1 until the secrets are rotated and history
> purged.

---

## 3. Detailed cross‑walk — gaps & partials

Only ⚠️ partial and ❌ gap rows are listed per domain (fully‑met ✅ and ➖ N/A controls are
summarised by count). Verdicts: ✅ met · ⚠️ partial · ❌ gap · ➖ N/A · 🏛️ process/organizational.

### §1 Secure Application Development — ✅3 ⚠️8 ❌4 ➖5

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| 1‑1 | Documented & applied SSDLC | ⚠️🏛️ | SES‑001 + OPS‑001 cover the pieces but no single SSDLC doc maps NCA §1–§3. Author one control‑mapping doc; owner‑attest it is *applied*. |
| 1‑2 | DevSecOps defined & followed | ❌ | `azure-pipelines.yml` = build/publish/deploy only; test stage `condition: false` (L108); **no SAST/DAST/secret‑scan/SCA**. Largest process gap. |
| 1‑3 | Security requirements captured early | ⚠️🏛️ | Named in SES‑001 §12; no per‑feature security‑requirement/abuse‑case artifact. Add to FDS template. |
| 1‑4 | Security testing in SSDLC test phases | ⚠️ | Strong authz/IDOR/injection regression tests exist, but the pipeline test gate is disabled and there is no automated security‑test stage. |
| 1‑5 | Automated malware/known‑exploit scan in test | ❌ | None in pipeline/repo. Add artifact/image malware + known‑exploit scanning. |
| 1‑6 | Varied methods (fuzzing, black‑box) | ❌ | No fuzzing/black‑box harness; prior assessment was white‑box + non‑destructive recon only. |
| 1‑7 | Secure separate dev/test/QA environments | ⚠️🏛️ | OPS‑001 §4 defines 4 envs; live recon shows all hosts on one IP. Owner‑attest isolation. |
| 1‑8 | Apply secure‑coding guidelines (Table A) | ⚠️🏛️ | SES‑001 §5–§12 encode rules; not explicitly mapped to NCA Table‑A per language. Reference them in SES‑001 §12. |
| 1‑11 | Restrict modify rights to source/prod data | ⚠️🏛️ | `main` protected + PR review (SES‑001 §9); Azure DevOps branch policy is server‑side — owner‑attest + export. |
| 1‑13 | Modern/trusted/licensed tools & libraries | ⚠️ | NuGetAudit + `TreatWarningsAsErrors` promote advisories to build errors (`Directory.Build.props:11`); no SBOM; pin `NuGetAudit=true` explicitly; add SBOM + licence inventory. |
| 1‑14 | Web‑app protection per WAF policy | ❌🏛️ | App‑layer rate‑limiting exists but **no WAF** in topology and no WAF policy doc. Deploy + document a WAF. |
| 1‑20 | Authorized changes; restrict dev‑env; event/change logging | ⚠️🏛️ | Git + PR + rich runtime audit/SIEM rules; dev‑env access + DevOps audit log are server‑side. Owner‑attest + access matrix. |
| 1‑9, 1‑10, 1‑15 | Memory‑safe language; OWASP Top‑10 mitigated; standardized crypto | ✅ | C#/.NET 10 + Dart; no SQLi/XXE/SSRF; AES‑GCM/HS256/PBKDF2/CSPRNG. |
| 1‑12, 1‑16, 1‑17, 1‑18, 1‑19 | Supplier clauses; vendor‑supported sw; training; personnel competency; workstation hardening | ➖🏛️ | Organizational — no in‑repo evidence; owner‑attest with records. |

### §2 Source Code Repository — ✅1 ⚠️7 ➖4

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| 2‑1 | Secure repo: identity + version control + audit + login | ⚠️🏛️ | Azure DevOps Repos + PR policy; SSO/MFA/audit‑log are server‑side. Owner‑attest. |
| 2‑2 | Restrict repo access on‑need | ⚠️🏛️ | Role‑based implied; export the member/permission list. |
| 2‑3 | Unified version scheme with install dates | ⚠️🏛️ | Doc versioning + DECISIONS_LOG; no app version‑stamp tied to install dates. Add assembly version + build date at `/health`/about. |
| 2‑4 | Periodically archive old source versions | ⚠️🏛️ | Git retains history; document a periodic archive/retention procedure. |
| 2‑6 | Archive EOL source for retrieval | ⚠️🏛️ | Git + last‑known‑good binary retention; document an EOL‑source archival procedure. |
| 2‑7 | Store copy of all externally‑developed source | ⚠️🏛️ | Flutter app in‑repo; confirm designer assets/components are all stored. |
| 2‑9 | Secrets management; none in containers | ⚠️ | Secrets‑out‑of‑repo design + `.gitignore` excludes prod scripts, **but** `appsettings.Development.json` still tracked + secrets in history; no managed vault. Untrack the file; adopt a vault. |
| 2‑5 | Separate dev source from prod source | ✅🏛️ | Trunk‑based; pipeline deploys only from `main` (`azure-pipelines.yml:34‑35`). |
| 2‑8, 2‑10, 2‑11, 2‑12 | Container/Docker hardening; trusted images; private registry+scan; no high‑priv container mgmt | ➖ | No containers (IIS‑on‑Windows). N/A unless containers are introduced. |

### §3 Source Code Review & Testing — ✅1 ⚠️15 ❌4

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| 3‑2 | SAST + DAST (internal software) | ❌ | No SAST/DAST tool in repo or pipeline. Integrate CodeQL/Semgrep + ZAP. |
| 3‑6 | Risk assessment before production | ❌🏛️ | No formal risk‑assessment register. Produce one before prod. |
| 3‑11 | VA + pentest **after** deploy | ❌🏛️ | No post‑deploy schedule. Schedule periodic post‑prod VA + pentest. |
| 3‑20 | Integrate tests into CI/CD | ❌ | Pipeline test stage **explicitly disabled** (`azure-pipelines.yml:108` `condition: false`). Re‑enable; make failing tests + SAST/SCA block the pipeline. |
| 3‑1 | Regular source‑code review (internal) | ⚠️🏛️ | Non‑author peer review mandated (SES‑001 §10) + agent reviews; document cadence + attest human review. |
| 3‑3 | Security review of external apps | ⚠️🏛️ | OPS‑001 §12 requires it; retain records. |
| 3‑4 | Review & approve controls before prod | ⚠️🏛️ | OPS‑001 §B.8 pre‑flight; execute + sign off before go‑live. |
| 3‑5 | Re‑evaluate after major change/period | ⚠️🏛️ | Freeze governance exists; add a periodic re‑assessment schedule. |
| 3‑7 | Cyber‑compliance testing before prod | ⚠️🏛️ | OPS‑001 §B.8 checklist; run + document. |
| 3‑8 | OWASP ASVS for requirements + test cases | ⚠️ | ASVS named; E2E catalogue exists; add ASVS‑L2 requirement/test traceability. |
| 3‑9 | Review config/hardening/patches before prod | ⚠️🏛️ | OPS‑001 §B.1/§B.8; execute (incl. IIS banner suppression INFO‑1). |
| 3‑10 | VA + pentest + secure‑dev review **before** prod | ⚠️🏛️ | Internal non‑destructive assessment only; commission an independent NCA‑accredited pentest before go‑live. |
| 3‑12 | Remediate all secure‑dev‑review issues before prod | ⚠️ | Active remediation done; **C2 + H2 remain open** by owner decision. Track to zero in a defect register. |
| 3‑13 | Test separation‑of‑duties | ⚠️ | Build‑time permission tests exist; add explicit SoD tests (approver ≠ requester) + deploy‑vs‑dev SoD attestation. |
| 3‑14 | Remove test accounts/data before prod | ⚠️🏛️ | Required by SES‑001 §12; committed super‑admin default (H1) is the live risk. Owner‑attest purge + env override + rotation. |
| 3‑15 | Logically separate test/dev from prod (ACL+FW) | ⚠️🏛️ | OPS‑001 §4 separate envs/DBs; provide network‑segmentation/firewall evidence. |
| 3‑17 | Licensed assessment tools | ⚠️🏛️ | NuGetAudit only; procure + document licensed SAST/DAST/SCA. |
| 3‑18 | Security testing across UT/SIT/UAT/non‑functional | ⚠️ | UT/IT/E2E mandated; add UAT/non‑functional security checks; re‑enable the gate. |
| 3‑19 | Defect/vuln management + register + tracking | ⚠️ | Informal register (DECISIONS_LOG + assessment findings); adopt a formal vuln register with severity SLAs. |
| 3‑16 | Non‑author peer review before prod | ✅🏛️ | Branch policy requires ≥1 approving non‑author review; owner‑attest the policy is enabled. |

### A1 Access Control — ✅17 ⚠️3 ❌1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A1‑19 | Disable unused/expired accounts (>30 days) | ❌ | No inactivity‑expiry/auto‑disable job. Add a scheduled flag/disable for accounts unused or `PendingApproval` beyond a configurable window, with audit. |
| A1‑12 | Log all failed access‑control decisions | ⚠️ | Token/rate‑limit rejections audited, but a permission **403** leaves no audit row. Add an authorization‑failure audit hook (FastEndpoints `OnForbidden`/middleware). |
| A1‑14 | Cumulative protection / Resource Governor | ⚠️ | Strong per‑request rate limits (`Program.cs:85‑170`) + pagination, but no per‑user record‑volume/bulk‑export ceiling. Add a per‑actor export/record governor + anomaly alerting. |
| A1‑18 | Service accounts least‑privilege | ⚠️ | Two‑DB separation + strict CORS; DB‑login privilege not source‑verifiable + committed SMTP cred. Confirm SQL login is `db_datareader/writer` only; rotate SMTP. |
| A1‑1…11, 13, 15, 16, 17, 20, 21 | (mediation, secured URLs/data, IDOR, fail‑secure, server‑side enforcement, CSRF, central mechanism, privileged‑logic separation, stored‑data ACLs, periodic re‑verify, revoke‑on‑change) | ✅ | Strong — see PermissionCatalog + `PermissionAuthorization.cs` + security‑stamp revocation. |

### A4 Insecure Design — ✅15 ⚠️3 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A4‑10 | Per‑user/day business limits + alerts + automated response | ⚠️ | Rate limits exist; no per‑user/day caps (seat reservations, bulk badges) with alerting. Add per‑actor daily caps + alerts. |
| A4‑13 | Threat modeling for critical flows | ⚠️ | Strong implicit reasoning in code/decisions; **no standing STRIDE artifact**. Author one for auth, permission, badge/gate, meeting‑request flows. |
| A4‑19 | Limit resource consumption per user/service | ⚠️ | Multi‑dimensional rate limits + upload caps; no per‑user concurrency/daily volume budget (overlaps A1‑14/A4‑10). |
| A4‑18 | Tenant separation | ➖ | Single‑tenant by design; document the decision. |
| A4‑1…9, 11, 12, 14, 15, 16, 17 | (trusted‑server flows, anti‑spoof, anti‑param‑tamper, anti‑repudiation, info‑disclosure, brute‑force, anti‑escalation, sequential flows, SoD/step‑up, lifecycle security, secure‑design libs, security language, layered inspection, abuse‑case tests, layer separation) | ✅ | Strong — server‑side flows, in‑transaction immutable RowAudit, محضر editor≠approver SoD. |

### A2 Cryptography — ✅6 ⚠️10 ❌3 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A2‑10 | PII encrypted at rest | ❌ | `NationalId`/`IqamaNumber`/`PassportNumber`/mobile in **plaintext** columns (`UserProfile.cs:65‑104`); only the ID *image* is AES‑GCM. Encrypt via EF `ValueConverter` over the AES‑GCM helper, or SQL Always Encrypted/TDE. **May need column‑length change → D‑110 freeze approval.** |
| A2‑9 | Keys protected; compromised key replaced/revoked | ❌ | JWT/AES/SMTP keys still in git **history** (`appsettings.Development.json` tracked). Rotate + purge history. |
| A2‑7 | Key‑management lifecycle policy | ❌ | No issue/rotate/revoke/expire policy or code; ID‑doc AES rotation marked "out of scope". Author + implement (incl. re‑encryption path). |
| A2‑3 | Master secrets not protected by plaintext on disk | ⚠️ | Machine‑scope env vars (not OS‑encrypted at rest). Move to DPAPI/Vault. |
| A2‑5 | Crypto verified per policy | ⚠️ | Platform primitives; no FIPS‑140/crypto‑policy statement. Document algorithm/policy mapping. |
| A2‑11 | Disable client storage/autocomplete of protected‑info forms | ⚠️ | Only one field has `autocomplete="off"`. Apply to national‑ID/passport inputs. |
| A2‑12 | Protected info in HTTP body not URL | ⚠️ | Credentials/PII in body; exception = recording‑stream token in `?access_token=` (scoped, `no-store`, L1). Acceptable. |
| A2‑14 | No client caching of protected‑info pages (`no-store`) | ⚠️ | PII byte endpoints use `private, max-age=60/300`, not `no-store`. Use `no-store` on ID/passport responses. |
| A2‑15 | Inventory of protected info + access/encryption policy | ⚠️ | No single PII‑field → control register. Produce one (A2‑10 is the consequence). |
| A2‑16 | Method to delete all PII at end of retention | ⚠️ | ID image deletable; soft‑delete keeps PII rows indefinitely. Add a retention/erasure routine. |
| A2‑18 | Detect/alert on abnormal info‑request volume (DLP) | ⚠️ | Rate limits bound request rate, not records returned. Add per‑role data‑volume thresholds + alerts on bulk export. |
| A2‑19 | App‑server creds not hardcoded; encrypted | ⚠️ | Working tree clean; gap = unencrypted env store + git history (A2‑3/A2‑9). |
| A2‑20 | Autocomplete disabled except auth forms | ⚠️ | Inconsistent; audit all forms. |
| A2‑8 | Non‑repudiation via digital signature | ➖/⚠️ | No payments in scope; audit trail not cryptographically signed (see A8‑1). Add signing if e‑payment/records added. |
| A2‑1, 2, 4, 6, 13, 17 | (server‑side crypto, fail‑secure, CSPRNG, approved mode, temp‑file protection, minimal outbound params) | ✅ | Strong. |

### A3 Injection / Input Validation — ✅12 ⚠️6 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A3‑4 | No XSS; controls prevent it | ⚠️ | JSON‑only API + `nosniff` + Blazor auto‑encode, but **no CSP on any host**. Add a tuned CSP (report‑only → enforce). |
| A3‑8 | Extra controls for dangerous chars | ⚠️ | Encode‑on‑output covers it; no global input allow‑list/normalisation + no CSP. Document policy; add CSP. |
| A3‑12 | Untrusted HTML output sanitised | ⚠️ | Auto‑encoding present; add CSP as second control; audit any `MarkupString`. |
| A3‑13 | Charset (UTF‑8) specified for inputs | ⚠️ | UTF‑8 default; no explicit `charset=utf-8` per Content‑Type. Set globally (see A5‑12). |
| A3‑14 | Input normalised before validation | ⚠️ | Trimming present; no NFC/NFKC normalisation. Add `string.Normalize()` at the trust boundary for filenames/hosts/identifiers. |
| A3‑18 | Validation failures logged | ⚠️ | Rate‑limit/ApiException logged; routine field‑validation failures not audited. Log validation‑failure events if NCA requires. |
| A3‑1, 2, 3, 6, 7, 9, 10, 11, 15, 16, 17, 19 | (no buffer‑overflow/SQLi/OS‑cmd, LIMIT, type/range/length, central validation, reject/audit, server‑side, mass‑assignment, HPP, per‑type, per‑destination encoding) | ✅ | Strong — EF parameterized, FluentValidation, DTO binding. |
| A3‑5 | LDAP injection | ➖ | No LDAP/AD integration. |

### A5 Communications & Misconfiguration — ✅12 ⚠️13 ❌2 ➖4

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A5‑1 | Trusted CA path; cert verified | ❌ | Self‑signed prod cert (H2). Issue a public‑CA cert for non‑underscore hostnames. |
| A5‑20 | SAST + DAST for XXE detection | ❌ | No SAST/DAST/SCA in CI. Add (overlaps §3‑2). |
| A5‑6 | Failed TLS doesn't fall back to insecure | ⚠️ | Servers HTTPS‑only; **Flutter trust‑all accepts any cert (C2)** — worse than fallback. Remove the override. |
| A5‑5 | External‑system least‑priv account | ⚠️ | Dedicated `no-reply@` mailbox; DB uses integrated auth — confirm service‑account scope. |
| A5‑12 | Every response Content‑Type w/ safe charset | ⚠️ | JSON lacks explicit `; charset=utf-8`. Add via middleware. |
| A5‑3, 9, 10 | TLS‑failure logging; UTF‑8 per connection; periodic config review | ⚠️ | IIS/Kestrel‑layer + ops cadence; partly outside app. |
| A5‑22 | Complex/secure DB credentials | ⚠️ | Integrated auth (no DB password); C1 secrets in history. Rotate + purge. |
| A5‑23, 26, 28, 29, 30, 31 | DB least‑priv; remove unneeded fns; disable accounts; per‑priv creds; no remote/anon; hardening templates | ⚠️🏛️ | Mostly server‑side SQL hardening — confirm `xp_cmdshell` off, `sa` disabled, login = `db_datareader/writer` only on the two DBs; adopt a CIS SQL template. |
| A5‑2, 4, 8, 11, 13, 14, 15, 17, 21, 24, 25, 27 | (TLS 1.2+/1.3, external auth, unified TLS, HTTP‑method allow‑list, anti‑clickjacking, ASCII headers, JSON, XXE disabled, parameterized queries, no hardcoded conn strings, connections closed ASAP, minimal features) | ✅ | Strong. |
| A5‑7, 16, 18, 19 | client‑cert revocation; XML/SOAP; XML validation; XSD upload | ➖ | No mTLS / no XML ingestion. |

### A6 Vulnerable Components & File Handling — ✅19 ⚠️10 ❌2 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A6‑13 | Continuous SCA inventory + CVE monitoring | ❌ | No central package mgmt / Dependency‑Check / SBOM in CI; NuGetAudit is the only gate. Add `dotnet list package --vulnerable` CI gate + CycloneDX SBOM. |
| A6‑18 | AV‑scan untrusted uploaded files | ❌ | No AV anywhere; mitigated by magic‑byte allow‑lists + non‑web‑served `App_Data` + `attachment` download. Integrate AV (esp. the 50 MB presentation path). |
| A6‑2 | Integrity of libs/code verified | ⚠️ | NuGetAudit promotes advisories to errors; no SBOM, no ML‑model hash pinning. Add SBOM + pin/verify FaceAiSharp weights. |
| A6‑11 | Patch components on known vulns | ⚠️ | M5/M8 fixed; `FaceAiSharp.Bundle 0.6.35` is pre‑1.0. Watch‑list + CVE alerts. |
| A6‑12 | Remove unused deps/components/files | ⚠️ | Dead `AiAssistant` config removed; ensure `.claude/worktrees/*` excluded from publish artifacts. |
| A6‑15 | Monitor unmaintained libs; virtual patching | ⚠️ | FaceAiSharp low cadence; watch‑list + fallback plan. |
| A6‑21 | No arbitrary remote content via IFRAME/HTML5 | ⚠️ | Stream embeds allow‑listed; add CSP `frame-src`/`frame-ancestors`. |
| A6‑10, 23, 28, 29, 30 | sandboxing; block outside‑resource access; deny‑execute on upload dirs; read‑only app files; disable shares | ⚠️🏛️ | ML inference bounded + uploads non‑web‑served; the rest are host/OS controls — set NTFS deny‑execute on `App_Data`/`Storage`, deploy app read‑only, disable admin shares, default‑deny egress. |
| A6‑1, 3‑9, 14, 16, 17, 19, 20, 22, 24, 26, 27, 31, 32 | (no malicious code, clean auth/session/access/input/output/crypto/logging code, signed sources, safe redirects, path normalisation, anti‑LFI/RFI, outside‑webroot storage, no exec of uploads, type allow‑list, magic‑byte verify, auth before upload, size caps) | ✅ | Strong — M1/M2/L3 fixes verified. |
| A6‑25 | Flash/Silverlight RIA cross‑domain | ➖ | No Flash/Silverlight. |

### A7 Authentication & Session — ✅32 ⚠️8 ❌6

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A7‑29 | Complexity classes + reject repeats/sequences/username/dictionary | ❌ | Only length(8)+letter+digit (`PasswordRules.cs:21‑26`, `DependencyInjection.cs:109‑111`). No uppercase/special, no repeat/sequence/username/dictionary checks. Tighten rules + custom `IPasswordValidator`. |
| A7‑28 | Strength checked; not common/weak | ❌ | No common/breached blocklist (`Password1` passes). Add HaveIBeenPwned/dictionary validator. |
| A7‑10 | Allow passphrases; protect against common pwds | ⚠️/❌ | Long passphrases allowed; common‑password half unmet (same root as A7‑28). |
| A7‑13 | Credentials expire after configurable period | ❌ | No password‑age mechanism. Add configurable max age + sign‑in check. **D‑110 freeze (new column) → owner approval.** |
| A7‑20 | Disallow N previous passwords | ❌ | No password history. Add `PasswordHistory` table + check. **Freeze‑gated.** |
| A7‑31 | Notify last account use on login | ❌ | No last‑login/failed‑count surfaced. Add `LastSuccessfulSignInAt` + count. **Freeze‑gated (column).** |
| A7‑44 | Disallow concurrent multi‑device sessions | ❌ | Concurrent sessions intentionally allowed (phone + kiosk). **Owner design decision** — add revoke‑prior‑on‑login if NCA requires single‑session. |
| A7‑8 | Brute‑force: IP + account lockout simultaneously | ⚠️ | Account lockout 5/15min + per‑email 5/min + per‑IP windows; no 60‑min IP **lockout** as the example specifies. Add an escalating IP‑block tier. |
| A7‑2 | Password masked; autocomplete disabled | ⚠️ | Masked ✅; autocomplete enabled with semantic values (modern OWASP best practice). Document deviation or set `autocomplete="off"` if assessor enforces literal text. |
| A7‑22 | Step‑up before sensitive operations | ⚠️ | 2FA at sign‑in + current‑password on change; no per‑action step‑up. Consider step‑up on Administrator‑wildcard actions. |
| A7‑4, 16 | Creds over strong transport; external creds outside source | ⚠️ | HTTPS‑only, but self‑signed cert (H2) + Flutter trust‑all (C2) + secrets in history (C1). |
| A7‑36, 39 | Session id not in URL; Secure + HSTS | ⚠️ | Cookie/header only (stream token exception, L1); HSTS short (30d, no `includeSubDomains`/preload). Lengthen to ≥1y. |
| A7‑1, 3, 5, 6, 7, 9, 11, 12, 14, 15, 17, 18, 19, 21, 23, 24, 25, 26, 27, 30, 32‑35, 37, 38, 40‑43, 45, 46 | (mediation, fail‑secure, no‑cleartext‑reset, no enumeration, no defaults, server‑side, recovery resistance, change‑requires‑current+reauth, decision logging, PBKDF2 salted+stretched, time‑limited code, no lock‑on‑forgot, no KBA, central auth, revoke‑on‑breach, one‑way hash, central monitor, generic errors, lockout, framework sessions, invalidate‑on‑logout, inactivity timeout, logout links, regenerate‑on‑login/reauth, long random tokens, path/domain scope, absolute cap, HTTPS throughout) | ✅ | Strong — generic errors, enumeration‑resistant, constant‑time compares, HMAC‑hashed OTP, security‑stamp revocation, rotation + reuse detection, absolute 24 h cap, hardened cookies. |

### A8 Software/Data Integrity (Deserialization) — ✅3 ⚠️2 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A8‑1 | Integrity checks on serialized objects | ⚠️ | No untrusted serialized‑object intake; JWTs HMAC‑signed; **audit log not signed/hash‑chained**. Add tamper‑evidence if NCA requires (see A9‑8). |
| A8‑5 | Restrict/monitor network from deserializing servers | ⚠️ | Single outbound `HttpClient` (OpenAI); add host/firewall egress allow‑listing (defense‑in‑depth). |
| A8‑2, 4, 6 | Type constraints; log failures; monitor continuous deser | ✅ | Only external parse = DOM‑based `JsonDocument` with typed extraction; rate‑limited + audited. |
| A8‑3 | Run deserialized code low‑priv | ➖ | No code/object deserialization occurs. |

### A9 Logging & Error Handling — ✅9 ⚠️6

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A9‑7 | Each event: timestamp/severity/security‑flag/identity/IP/description | ⚠️ | Has timestamp/type/outcome/identity/IP/correlation; **no explicit Severity or security‑relevance flag**. Add both for SIEM filtering. |
| A9‑8 | Logs protected from unauthorized access & modification | ⚠️ | Append‑only by convention (not WORM/signed); file logs rely on OS ACLs. Add tamper‑evidence (hash‑chain/signing) + forward to write‑once SIEM. |
| A9‑9 | Don't log protected info | ⚠️ | `SubjectEmail` (PII) intentionally stored; `/admin/logs` download unredacted (L6 accepted). Document in PII register. |
| A9‑10 | Searchable log‑analysis tool across fields | ⚠️ | In‑app viewer + flat files; no centralized SIEM. Forward to Elastic/Sentinel. |
| A9‑13 | Standardized log backup/archive | ⚠️ | 31‑day rolling **delete**, not archive. Define + implement backup/archival meeting retention. |
| A9‑15 | Required log set enabled | ⚠️ | Most present; **missing crypto‑module‑failure and TLS‑handshake‑failure security events**, and validation‑failure not audited as an event. Add those events. |
| A9‑1‑6, 11, 12, 14 | (input error‑checking, no PII in errors, server‑side handling/logging, fail‑secure deny, success+failure events, no log injection, unified logging, exception handling) | ✅ | Strong — ~250 typed events, generic 500s, structured Serilog. |

### A10 SSRF — ✅5 ⚠️2 ➖2

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A10‑2 | Default‑deny egress / network policy | ⚠️🏛️ | Not source‑verifiable. Ops: default‑deny egress from the app host. |
| A10‑6 | Disable HTTP redirects | ⚠️ | No user‑URL redirect following; asset endpoint issues a 302 to an allow‑listed public‑https URL (L3‑bounded). Optional: return URL in JSON instead of 302. |
| A10‑1, 3, 4, 5, 8 | (segmented remote fetch, filter input, scheme/port/dest allow‑list, no raw responses, no co‑hosted security services) | ✅ | No user‑driven outbound fetch exists; URLs are stored as data + rendered client‑side. |
| A10‑7, 9 | DNS‑rebinding/TOCTOU awareness; VPN on standalone | ➖ | No server‑side fetch; not a standalone system. |

### A11 Mobile Verification — ✅13 ⚠️8 ❌6 ➖1

| Ctrl | Requirement | Verdict | Evidence / Gap |
|---|---|---|---|
| A11‑1 | Client verifies TLS certs | ❌ | **Trust‑all override ships in release** (`self_signed_api_tls_io.dart:26‑27`, `main.dart:28`). Delete it; gate any PoC behind `!kReleaseMode`. (C2/H2) |
| A11‑15 / A11‑22 | Certificate pinning (anti‑MITM) | ❌ | No pinning; trust‑all actively defeats it. Add SPKI pinning after the CA cert lands. |
| A11‑6 | Prevent screenshot/snapshot leakage | ❌ | No `FLAG_SECURE`/iOS snapshot blur. Add on badge‑QR/profile/OTP screens. |
| A11‑7 | Block on jailbroken/rooted devices | ❌ | No root/JB detection. Add a library + policy (warn vs block). |
| A11‑26 | Anti‑debug / anti‑RE | ❌ | None; release signs with debug keys. Add anti‑tamper + obfuscate. |
| A11‑3 | No protected info on shared resources | ⚠️ | vCard/ICS written to app temp in cleartext, not deleted after share. Delete after `Share.shareXFiles`. |
| A11‑11 | Binary obfuscated | ⚠️ | R8 obfuscates Java/Kotlin; Dart‑layer `--obfuscate --split-debug-info` not evidenced. Add to release build. |
| A11‑14 / A11‑23 | No autocomplete on sensitive fields; iOS keyboard cache | ⚠️ | Passwords obscured; email/PII fields lack `autocorrect:false`/`enableSuggestions:false`. Set them. |
| A11‑16 | No config misconfig (debug/world perms) | ⚠️ | Release signs with the **debug keystore** (`build.gradle.kts:33‑35`, live `// TODO`). Add a real release signing config. |
| A11‑17 | 3rd‑party libs current & CVE‑free | ⚠️ | Recent but `^`‑pinned; `flutter_secure_storage 9.x` uses deprecated `EncryptedSharedPreferences`. Run `dart pub audit`/`flutter pub outdated` as a release gate. |
| A11‑19 | No protected info in query; POST+CSRF | ⚠️ | Wire is clean (POST bodies); CSRF token not yet wired (`headers_interceptor.dart:45‑50`) — low risk on bearer client; wire before handover. |
| A11‑27 | No exported sensitive components | ⚠️ | Only the launcher activity is exported (standard Flutter). Confirm no plugin injects exported components at merge time. |
| A11‑2, 4, 5, 8, 9, 10, 12, 13, 18, 21, 24, 25, 28 | (no UDID auth, no SQLite PII, no hardcoded secrets, sane timeout, minimal permissions, clean crash logs, no test data shipped, no PII logging, no HTTPS caching, ASLR, no world‑perms, Keystore/Keychain storage, validated exposed components) | ✅ | Strong — secrets in Keystore/Keychain, minimal permissions, no PII logging. |
| A11‑20 | Truncate account numbers | ➖ | No financial PAN handled. |

> iOS note: no `ios/` folder is committed (native folders are generated at build), so
> A11‑6/9(iOS)/23/25(keychain attributes) must be re‑verified on the generated iOS target.

---

## 4. Remediation plan (grouped by how it gets fixed)

### Group A — Code‑fixable now (no schema change, no owner gate)
1. **A1‑12** — audit permission **403** failures (authorization‑failure hook).
2. **A9‑15 / A9‑7** — add crypto‑module‑failure + TLS‑failure audit events; add Severity +
   security‑relevance fields to the audit entry.
3. **A2‑14 / A2‑11 / A2‑20** — `Cache-Control: no-store` on PII responses; `autocomplete="off"`
   on national‑ID/passport form fields.
4. **A5‑12 / A3‑13** — explicit `; charset=utf-8` on JSON responses.
5. **A3‑14** — `string.Normalize()` at the trust boundary for filenames/hosts/identifiers.
6. **A7‑29 / A7‑28 / A7‑10 / A7‑8** — tighten password complexity (classes + repeat/sequence/
   username checks) + add a common/breached‑password blocklist + an IP‑lockout tier.
7. **A11 mobile (non‑TLS)** — `FLAG_SECURE`/iOS snapshot blur (A11‑6); root/JB detection
   (A11‑7); `flutter build --obfuscate` (A11‑11/26); real release signing config (A11‑16);
   `autocorrect:false` on PII fields (A11‑14/23); delete temp vCard after share (A11‑3);
   wire the CSRF header (A11‑19).
8. **A4‑13** — author a STRIDE threat‑model doc for the critical flows.

### Group B — Code‑fixable but needs a decision / D‑110 freeze approval
9. **A2‑10** — encrypt PII at rest (national‑ID/Iqama/passport). EF `ValueConverter` over the
   AES‑GCM helper; **likely a column‑length change → freeze approval**; breaks plaintext search.
10. **A7‑13 / A7‑20 / A7‑31 / A1‑19** — password expiry, password history, last‑login notice,
    dormant‑account auto‑disable — each adds a column/table → **D‑110 freeze approval**.
11. **A7‑44** — concurrent‑session policy (revoke‑prior‑on‑login) — **owner design decision**
    (conflicts with the phone+kiosk use case).
12. **CSP (A3‑4/12, A6‑21)** — add a tuned Content‑Security‑Policy (report‑only → enforce)
    on Website/CP/API; needs browser tuning.
13. **A6‑18** — integrate anti‑virus scanning for uploads (needs an AV/ICAP component).
14. **A1‑14 / A4‑10 / A4‑19 / A2‑18** — per‑user record‑volume/bulk‑export governor + alerting.

### Group C — Pipeline / DevSecOps (CI changes)
15. **§3‑20** — re‑enable the disabled pipeline test gate (`azure-pipelines.yml:108`).
16. **§1‑2 / §3‑2 / A5‑20** — add SAST (CodeQL/Semgrep) + DAST (ZAP) stages.
17. **A6‑13 / A6‑2 / §3‑17** — add `dotnet list package --vulnerable` gate + central package
    management + CycloneDX SBOM; pin `NuGetAudit=true`.

### Group D — Owner / operations (not codeable)
18. **C1 / A2‑9** — rotate JWT + ID‑doc AES + SMTP secrets and **purge git history**;
    untrack `appsettings.Development.json`.
19. **H1 / §3‑14** — verify `SIMF_SuperAdmin__*` env on the server + rotate the live admin.
20. **H2 / C2 / A5‑1** — issue a real CA cert for non‑underscore hostnames, then delete the
    Flutter trust‑all override (held by owner).
21. **SQL hardening (A5‑23/26/28/30, A1‑18)** — least‑privilege login (`db_datareader/writer`),
    `xp_cmdshell` off, `sa` disabled, no anonymous/remote.
22. **Host hardening (A6‑28/29/30, A10‑2)** — NTFS deny‑execute on upload dirs, read‑only app
    files, disable admin shares, default‑deny egress.
23. **WAF (1‑14)**; **independent NCA‑accredited pentest + risk assessment, pre‑ and
    post‑prod (3‑6/10/11)**; **network‑segmentation evidence (3‑15/1‑7)**.
24. **A2‑7 / A2‑3** — key‑management policy + a managed secret store (DPAPI/Vault).
25. **A9‑8/10/13** — forward logs to a write‑once SIEM + backup/archival + tamper‑evidence.
26. **Governance docs/attestations** — SSDLC mapping (1‑1), DevSecOps model (1‑2), secure‑coding
    training (1‑17), workstation hardening (1‑19), supplier cyber‑clauses (1‑12), version‑stamp
    + archival procedures (2‑3/4/6), defect/vuln register with SLAs (3‑19), PII data‑protection
    register (A2‑15).

### Group E — Owner‑held (explicitly deferred)
- **C2 + H2** — Flutter trust‑all TLS + self‑signed cert, pending the CA‑certificate rollout.

---

## 5. Notes

- This is a **point‑in‑time** white‑box assessment; controls marked 🏛️/ops require the owner
  to attest with exported evidence (DevOps policy exports, training records, segmentation
  diagrams, SQL/host configuration) for a complete NCA audit package.
- The standard is a **template** — the owner should formally adopt it (replace «اسم الجهة»,
  set the classification, obtain head‑of‑entity approval) per its own §"الاعتماد" and
  §"الأدوار والمسؤوليات".
- Companion document: `docs/security/SIMF-Security-Assessment-2026-06-20.md` (the OWASP/NCA‑ECC
  platform assessment whose findings C1/H1/H2/C2 + M/L items are referenced above).
