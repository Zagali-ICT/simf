# SIMF SIEM Rules

Detection-engineering rules in Sigma YAML format that SOC operators
should deploy against the SIMF audit log (`OperationLogEntry`
table / event stream). Each rule maps to a specific abuse pattern
flagged by the per-module security + threat-detection review pass.

## Status — v1 reference set, needs Sigma 2.x translation

These twelve rule files are the **conceptual detection intent**, not
production-ready Sigma 2.x. The D-184 review-pass flagged systemic
syntax issues that need a focused rewrite (D-185 backlog):

- The pipe-aggregate form (`condition: selection | count() by X > N`
  with a separate `timeframe:`) is legacy Sigma v1. Sigma 2.x uses a
  top-level `correlation:` block (`type: event_count`, `group-by:`,
  `timespan:`, `condition: { gte: N }`). Both forms convert to the
  same SPL / KQL via pySigma but strict v2 linters reject the v1
  shape.
- AI-002 uses invented `aggregate:` + `threshold:` keys — needs full
  rewrite to either the v1 `condition: selection | sum(...) by X > N`
  or the v2 `correlation:` block.
- `Detail|json: { field: value }` is not a Sigma value modifier.
  Either pre-parse `Detail` JSON at ingest time and expose
  `Detail.field` directly, or rewrite as `Detail|contains:
  '"field":value'` (ugly but legal).
- Threshold tuning is intent-level, not production-baselined: AI-001
  (>2/10m), AI-003 (>1/5m), S-001 (>10/1m) need real-traffic
  calibration before page-out.
- Field name drift between rules: `ActorUserId` vs `CallerUserId`,
  `Detail.key` vs `Detail.promptKey`, `callerKind` vs `CallerKind`.
  Pick one set per field and apply consistently.
- AI-005 description mentions seven redaction markers; detection
  lists only four. Reconcile.
- AI-008 references an undefined `redactionTokens` field.
- One missing rule: AI-010 — bulk view of `AiInvocation.Viewed` for
  the collection-tactic gap on the audit drill-down surface.

SOC operators can lift the detection logic from these files now
(threshold + audit-event coverage is correct); the syntax pass
lands as D-185.

## Sources

Each rule's `logsource` references `simf-audit` — the canonical SIMF
audit-event stream where `EventType` is one of
`SIMF.Application.Auditing.AuditEvents` constants, `ActorUserId` is
the acting user (Guid, may be `Guid.Empty` for system / anonymous),
`Outcome` is `Success`/`Failure`, and `Detail` is a JSON-formatted
structured payload (D-179 changed this from free text to JSON; SIEM
rules can field-extract via `parse_json(Detail)`).

## Rule index

| ID | Name | Severity | Source decision |
|----|------|----------|-----------------|
| AI-001 | Prompt tampering burst | High | D-178, D-179 |
| AI-002 | Test-harness token abuse | High | D-178, D-179 |
| AI-003 | Feature-kill via bulk deactivate | Critical | D-178 |
| AI-004 | Silent prompt edit (drift without semantic change) | Medium | D-179 |
| AI-005 | Redaction-trigger rate (secret paste into AI input) | Medium | D-179 |
| AI-006 | Cross-feature prompt-key probing | Medium | D-179 |
| AI-007 | IBAN paste with intent verbs | High | D-181 |
| AI-008 | Bulk PII funnel | High | D-181 |
| AI-009 | Secret exfiltration via prompt | Critical | D-181 |
| S-001 | Admin bulk release of active seat reservations | Medium | D-182 |
| S-002 | Meeting-request reject burst | Medium | D-183 |
| S-003 | Delegation deactivation burst | Medium | D-183 |

## Deployment

These YAML files are the SOC-side source of truth. The SIMF repo
ships them so changes to the audit shape (event-name renames, JSON
schema bumps) can be tracked alongside the rules that depend on
them. Deploying to the SOC platform (Sentinel / Elastic / Splunk)
is operator-side — translate the Sigma syntax to the target
platform's query language and import via the platform's rule
catalogue.

## Cross-version drift hashes (D-181 `v1:` prefix)

The `AiPrompt.Updated` event Detail JSON carries `contentHashOld`
+ `contentHashNew` strings with a `v1:` version prefix (D-181
HMAC migration). Rules that compare hashes (AI-001 / AI-004) MUST
skip cross-version compares — once an HMAC key rotation bumps to
`v2:` no historical `v1:` baseline will match. Pattern:

```
where startswith(D.contentHashOld, "v1:") and startswith(D.contentHashNew, "v1:")
```
