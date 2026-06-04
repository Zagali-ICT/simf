# SIMF SIEM Rules

Detection-engineering rules in Sigma 2.x YAML format that SOC operators
should deploy against the SIMF audit log (`OperationLogEntry` table /
event stream). Each rule maps to a specific abuse pattern flagged by
the per-module security + threat-detection review pass.

## Status — D-185 quality pass complete (2026-05-30)

Every rule was rewritten in this pass to:

- Use the **Sigma 2.x `correlation:` block** (replaces the legacy
  `condition: selection | count() by X > N` + `timeframe:` form).
  Each correlated rule is a **multi-doc YAML file**: the first
  document is the base selection rule (level: informational), the
  second is the correlation rule that references it by id.
- Use **canonical, field-extracted Detail keys** (`Detail.promptKey`,
  `Detail.promptId`, `Detail.callerKind`, `Detail.contentChanged`,
  `Detail.contentHashOld`, `Detail.contentHashNew`, `Detail.errorCode`,
  `Detail.redactionKinds`, `Detail.redactionCount`, `Detail.inputPreview`,
  `Detail.tokensInput`, `Detail.tokensOutput`, `Detail.status`,
  `Detail.reservationId`, `Detail.row`, `Detail.meetingRequestId`,
  `Detail.delegationId`, `Detail.invocationId`, `Detail.feature`).
  No more bare `Detail|json: { field: value }` (which is not a Sigma
  modifier) and no field-name drift between rules.
- Use **`ActorUserId`** for every actor reference (not `CallerUserId`).
  Top-level event fields are `EventType`, `Outcome`, `ActorUserId`,
  `Detail`. SOC ingestion must `parse_json(Detail)` so the nested
  keys above become first-class fields.
- Have **explicit thresholds** documented per rule, tuned by the D-185
  security review pass.
- Document the **SOC-platform-native equivalent** in the `notes:` block
  for rules where the Sigma `correlation:` form is a stand-in for a
  more expressive aggregation (AI-002 token-sum, AI-008 multi-value
  count-distinct).

## Canonical Detail JSON shape

The D-179 audit migration stores `Detail` as JSON. SOCs MUST configure
ingestion to parse it so the keys below are addressable as
`Detail.<key>`. Keys are stable across rule files:

| Key | Type | Emitted on | Meaning |
|-----|------|------------|---------|
| `promptId` | guid | AiPrompt.* | The prompt's primary key |
| `promptKey` | string | AiPrompt.*, AiInvocation.* | Stable prompt slug (e.g. `assistance.it`) |
| `callerKind` | string | AiInvocation.* | `Anonymous` \| `Guest` \| `Visitor` \| `Admin` \| `Other` |
| `contentChanged` | bool | AiPrompt.Updated | true → user-facing text changed |
| `contentHashOld` | string | AiPrompt.Updated | `v1:<hmac>` or `v2:<hmac>` of previous content |
| `contentHashNew` | string | AiPrompt.Updated | `v1:<hmac>` or `v2:<hmac>` of new content |
| `version` | int | AiPrompt.Updated | Optimistic-concurrency version after update |
| `errorCode` | string | AiInvocation.Failed | e.g. `AI_PROMPT_NOT_FOUND`, `AI_RATE_LIMITED` |
| `redactionKinds` | string[] | AiInvocation.Succeeded | Subset of `{KEY,JWT,PAN,PEM,NID,PHONE,EMAIL,IBAN}` |
| `redactionCount` | int | AiInvocation.Succeeded | Total redaction-marker count across all kinds |
| `inputPreview` | string | AiInvocation.Succeeded | Sanitised, length-capped input snippet (redacted) |
| `tokensInput` | int | AiInvocation.Succeeded | Prompt tokens billed |
| `tokensOutput` | int | AiInvocation.Succeeded | Completion tokens billed |
| `invocationId` | guid | AiInvocation.Succeeded, AiInvocation.Viewed | Drill-down target id |
| `feature` | string | AiInvocation.* | `question-filter` \| `faq` \| `assistance` \| `translate` \| `live-translation` \| `live-sign-language` |
| `provider` | string | AiInvocation.* | Provider name (e.g. `OpenAi`, `Echo`) |
| `model` | string | AiInvocation.* | Model id used for the call |
| `latencyMs` | int | AiInvocation.* | End-to-end provider latency |
| `status` | string | MeetingRequest.Responded | `Accepted` \| `Rejected` |
| `meetingRequestId` | guid | MeetingRequest.*, Admin.MeetingRequestViewed | |
| `sessionId` | guid | MeetingRequest.Submitted | |
| `count` | int | Admin.MeetingRequestsListed | Rows returned this call |
| `total` | int | Admin.MeetingRequestsListed | Total matching rows |
| `top` | int | Admin.MeetingRequestsListed | Page-size requested |
| `skip` | int | Admin.MeetingRequestsListed | Offset requested |
| `statusFilter` | string | Admin.MeetingRequestsListed | Validated enum name or `""` |
| `sessionFilter` | string | Admin.MeetingRequestsListed | Validated Guid or `""` |
| `reservationId` | guid | SeatReservation.* | |
| `row` | string | SeatReservation.* | Row label e.g. `A`, `B12` |
| `delegationId` | guid | Delegation.* | |

## Sources

Each rule's `logsource` references `simf-audit` — the canonical SIMF
audit-event stream where `EventType` is one of
`SIMF.Application.Auditing.AuditEvents` constants, `ActorUserId` is
the acting user (Guid, may be `Guid.Empty` for system / anonymous),
`Outcome` is `Success`/`Failure`, and `Detail` is a JSON-formatted
structured payload (D-179 changed this from free text to JSON; SIEM
ingestion field-extracts via `parse_json(Detail)`).

## Rule index

| ID | Name | Severity | Source decision |
|----|------|----------|-----------------|
| AI-001 | Prompt tampering burst (≥4 distinct promptIds / 10m) | High | D-178, D-179, D-185 |
| AI-002 | Test-harness token abuse (≥200 admin dry-runs / 1h) | High | D-178, D-179, D-185 |
| AI-003 | Feature-kill via bulk deactivate (≥3 distinct promptIds / 5m) | Critical | D-178, D-185 |
| AI-004 | Silent prompt edit (metadata-only change) | Medium | D-179 |
| AI-005 | Redaction-trigger rate (≥5 redacted invocations / 1h) | Medium | D-179, D-181, D-185 |
| AI-006 | Cross-feature prompt-key probing (≥5 distinct unknown keys / 10m) | Medium | D-179, D-185 |
| AI-007 | IBAN paste with intent verbs | High | D-181, D-185 |
| AI-008 | Bulk PII funnel (≥3 distinct PII kinds / 15m) | High | D-181, D-185 |
| AI-009 | Secret exfiltration via prompt (any hit) | Critical | D-181, D-185 |
| AI-010 | Invocation bulk-view (≥50 distinct invocationIds / 30m) | High | D-179, D-185 |
| S-001 | Admin bulk release of active seat reservations (≥10/1m) | Medium | D-182 |
| S-001b | Seat bulk release — burst variant (≥3/10s) | High | D-182, D-185 |
| S-002 | Meeting-request reject burst (≥10/5m) | Medium | D-183 |
| M-001 | Meeting-request bulk-view (≥20 distinct meetingRequestIds / 10m) | High | D-185 |
| M-002 | Meeting-requests bulk-list scrape (count≥100 + empty filters) | High | D-185 |
| M-002b | Meeting-requests list repeat scrape (≥5 list calls / 10m) | Medium | D-185 |
| M-003 | ProfileType IsVisitor flip (audience↔partner queue re-route) | High | D-186 |
| M-004 | Admin approval-scope probe (per-event informational) | Informational | D-186 |
| M-004b | Admin approval-scope probe burst (≥10 mismatches / 10m) | High | D-186 |
| M-005 | Walk-in partner-side registration burst (≥5 / 15m) | Medium | D-186 |

## Deployment

These YAML files are the SOC-side source of truth. The SIMF repo
ships them so changes to the audit shape (event-name renames, JSON
schema bumps) can be tracked alongside the rules that depend on
them. Deploying to the SOC platform (Sentinel / Elastic / Splunk)
is operator-side — translate the Sigma 2.x correlation blocks to
the target platform's query language via pySigma 0.11+ and import
via the platform's rule catalogue.

The smoke test under `tests/SIMF.Api.Tests/SiemRulesShapeTests.cs`
guards the structural invariants of every rule file (top-level keys
present, `correlation:` blocks well-formed, no legacy v1 pipe-aggregate
syntax). Run it before importing into the SOC platform.

## Cross-version drift hashes (D-181 `v1:` prefix)

The `AiPrompt.Updated` event Detail JSON carries `contentHashOld`
+ `contentHashNew` strings with a `v1:` version prefix (D-181
HMAC migration). Rules that compare hashes (AI-001 / AI-004) MUST
skip cross-version compares — once an HMAC key rotation bumps to
`v2:` no historical `v1:` baseline will match. Pattern:

```
where startswith(Detail.contentHashOld, "v1:") and startswith(Detail.contentHashNew, "v1:")
```

## HMAC rotation playbook (D-181 carry-over → D-185)

When operators rotate the `Ai:PromptHashHmacKey` server secret, the
hash version prefix MUST bump from `v1:` to `v2:` (and so on). Steps:

1. Generate the new 256-bit key (`openssl rand -base64 32`).
2. Stand up the new key alongside the existing one in the secrets
   store (Azure Key Vault / AWS Secrets Manager — keep both for the
   cutover window).
3. Bump `Ai:PromptHashKeyVersion` (Options-bound key under
   `AiOptions`) from `1` to `2`.
4. Restart the API — new prompt updates now emit `v2:` hashes.
5. SOC rules AI-001 / AI-004: pin `validFrom` to the cutover UTC
   timestamp on the **new** rule version that compares `v2:` hashes;
   keep the old rule alive for historical replay until the audit log
   retention window expires.
6. After retention rolls over, delete the `v1:` key from the secrets
   store and remove the legacy rules.

A persisted history table (`AiPromptHistory`) for the `v1:`/`v2:`
hash audit trail remains an open architectural choice — owner decision
pending. The carry-over backlog under D-185 captures it.
