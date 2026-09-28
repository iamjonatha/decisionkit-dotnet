# Security Policy

## Supported versions

The project is pre-release. Until `1.0.0` is published, only the latest released version
receives security fixes.

| Version | Supported |
| --- | --- |
| latest `0.x` | yes |
| older `0.x` | no |

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

Report privately through
[GitHub Security Advisories](https://github.com/iamjonatha/decisionkit-dotnet/security/advisories/new).

Please include:

- the affected package and version;
- a description of the impact;
- a minimal reproduction, with every credential redacted;
- any suggested mitigation.

You can expect an acknowledgement within 5 working days and an assessment within 15
working days. Fixes are released as soon as practical, and the advisory is published once
a fixed version is available. Reporters are credited unless they ask not to be.

## Security properties this project commits to

These are treated as security requirements, not as best-effort behaviour. A regression in
any of them is a vulnerability.

1. **Credentials never leave the credential path.** API keys and `Authorization` headers are
   never written to a log, an exception message, a `ToString()` result, a diagnostic dump or
   a serialized options object.
2. **Payloads are not logged by default.** Request and response bodies may contain the data
   your application evaluates. Raw capture is opt-in and can be disabled entirely.
3. **Nothing is persisted automatically.** The library writes no cache, no log file and no
   request archive of its own.
4. **HTTPS is required for production endpoints.** Plain HTTP is rejected unless explicitly
   allowed for a local test endpoint.
5. **Retry never silently duplicates a non-idempotent operation.** Automatic retry of a
   non-idempotent request must be an explicit, documented configuration choice.
6. **Dependencies are auditable.** Shipping packages reference only Microsoft-maintained
   framework packages, pinned centrally, with NuGet auditing enabled at build time.

## What is out of scope

- Vulnerabilities in the TypeSafe JEV service itself. Report those to TypeSafe.
- Data you deliberately send to a configured provider. The library documents what it
  transmits; deciding what is acceptable to transmit is the application's responsibility.
- Misconfiguration such as embedding an API key in source control. See
  [`docs/guides/secrets.md`](docs/guides/secrets.md).
