# SIMF Decisions Log

Implementation decisions and assumptions that are not (or not yet) captured in a
controlled document. Each entry is dated and explains the reasoning, per the
project working rules. Newest first.

When a decision is later absorbed into a controlled document, the entry keeps a
note pointing to that document.

| ID | Date | Decision | Reasoning |
|----|------|----------|-----------|
| D-005 | 2026-05-21 | ASP.NET Core Identity enforces the SIMF-API-001 §12.5 password baseline (length 8, at least one digit); the request validators add the remaining rules (a letter, not equal to the email). | The increment-3 security review found that leaving Identity's password options permissive meant any credential path not going through the FluentValidation validator — the super-admin seeder, the future password-reset flow — got an effective 1-character policy. §12.5's "enforced in one place" is therefore read as defence-in-depth: the baseline at the Identity credential layer, the full policy with field-level messages in the validators. |
| D-004 | 2026-05-21 | Per-endpoint rate limiting (FastEndpoints `Throttle`) is deferred from increment 3 to increment 4. | FastEndpoints' `Throttle` returns 403 when a request carries no `X-Forwarded-For` header, which breaks direct (non-proxied) callers and the test host. Rate limiting belongs in the increment-4 middleware pipeline (SIMF-Sprint1 plan §7), applied uniformly. |
| D-003 | 2026-05-21 | .NET test assertions use xUnit's built-in `Assert`; FluentAssertions is not adopted. | FluentAssertions v8 carries a commercial-licence model that, combined with the warnings-as-errors build, risks breaking the build. xUnit's own assertions are sufficient and dependency-free. Recorded in SIMF-TST-001 v1.1 §5. |
| D-002 | 2026-05-21 | The solution uses the `.slnx` solution format (`SIMF.slnx`). | The .NET 10 SDK defaults `dotnet new sln` to the new XML solution format. Kept rather than forcing the legacy `.sln` format. |
| D-001 | 2026-05-21 | FastEndpoints registration (`AddFastEndpoints` / `UseFastEndpoints`) is deferred from increment 1 to increment 3. | FastEndpoints throws at startup when no endpoint is declared, and the first endpoint arrives in increment 3 (the authentication endpoints). The `FastEndpoints` package stays referenced by `SIMF.Api`; only the wiring is deferred. |
