# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the major version is `0`, the public API may change in any minor release.

## [Unreleased]

### Added

- Repository foundation: solution layout, central package management, multi-targeting
  for `net8.0` and `net10.0`, analyzers and code style enforced at build time.
- Contribution rules, code of conduct, security policy, issue and pull request templates.
- Continuous integration on Linux and Windows: format verification, build, test and pack.
- `QuestionId`, the validated value-object identifier used to correlate a question with
  its answer.
- `RequestId` and `IdempotencyKey`, the remaining identity value objects.
- `Probability`, `Score`, `Choice<TOption>` and `OptionProbability<TOption>`: range-checked,
  immutable value objects. A probability distribution is never collapsed into a single
  selection automatically.
- `Question`, `Question<TAnswer>`, `ProbabilityQuestion`, `ScoreQuestion`,
  `ChoiceQuestion<TOption>` and `UnknownQuestion`.
- `QuestionSet`: ordered storage with indexed lookup that rejects duplicate identifiers.
- `Answer`, `Answer<TValue>`, `ProbabilityAnswer`, `ScoreAnswer`, `ChoiceAnswer<TOption>`
  and `UnknownAnswer`, which preserves everything a newer provider sends.
- `DecisionResult` with answer lookup typed by the question, a dynamic lookup by
  identifier, `DecisionMetadata` and the extensible `DecisionUsage` metric set.
- Architecture documentation, the decision records and the public roadmap.
- Domain model documentation and ADR-0005 on inferring the answer type from the question.
- `IDecisionProvider`: the single asynchronous, transport-free contract every provider
  implements and every application depends on.
- `DecisionRequest`, carrying the questions, the input, provider-neutral options,
  correlation identifiers and metadata, and rejecting a request that asks nothing.
- `DecisionInput` for the text or state a decision is about, and `DecisionOptions` for the
  semantics of an answer. Transport and provider settings are kept out of both.
- `DecisionProviderCapabilities`, so a caller can check support for question types,
  idempotency, usage reporting, metadata, batching and explanations instead of guessing.
- `DecisionError`, `DecisionErrorCategory` and `DecisionRetryHint`, which preserve the
  provider's error code, request identifier, documentation link and retry advice.
- `DecisionException` and the four failure types below it: `DecisionValidationException`,
  `DecisionAuthenticationException`, `DecisionTransientException` and
  `DecisionProviderException`. A cancelled call throws `OperationCanceledException`.
- Provider contract documentation and ADR-0006 on the error model.
- `DecisionKit.Testing`: `FakeDecisionProvider` with fluent canned answers, derived
  capabilities, simulated latency on a `TimeProvider`, error injection and
  `FailsTimes(n, error)` for retry tests.
- `DeterministicDecisionProvider`, which derives stable, plausible answers from the request
  through a fixed hash, for snapshots, samples and load tests.
- `DecisionCallLog`, `RecordedDecisionCall` and `RecordingDecisionProvider` for capturing
  what any provider was asked.
- `DecisionAssert`, framework-agnostic assertion helpers for results and captured requests.
- Testing guide and ADR-0007 on using the BCL `TimeProvider` as the only clock abstraction.
- `DecisionKit.Jev` protocol layer: the JEV wire DTOs, source-generated `System.Text.Json`
  serialization and the `JevJsonSerialization` facade. No transport yet. The contract is
  verified against the published reference at <https://docs.typesafe.ai/api.md>, the official
  Python SDK schemas and the community .NET SDK, not inferred.
- `QuestionMapper`, `RequestMapper`, `AnswerMapper`, `ResponseMapper` and `ErrorMapper`:
  explicit, hand-written translation in both directions, configured by `JevMappingOptions`.
- Mapping for the three JEV question types: `noul`, the boolean-like primitive whose answer
  is a probability rather than a verdict with a confidence; `choice`; and `score`, judged
  against an ordered rubric rather than a numeric range.
- `JevScoreQuestion`, the rubric-based score question JEV requires. A plain `ScoreQuestion`
  is rejected rather than converted, because a rubric cannot be invented from a range.
- `JevAnswers`, which reads back the three things JEV reports that the provider-neutral
  domain does not model: the opaque confidence, the score legend and the per-level
  distribution.
- Unknown-type handling that preserves the provider's type name, every unrecognized property
  and the raw JSON, so an answer DecisionKit does not model does not fail the response.
- `ChoiceOutcome`, the non-generic description of a choice — selection, distribution and
  metadata — and a non-generic `OptionProbability`, so a provider can build a choice answer
  without knowing the option type.
- JEV wire protocol documentation, with its sources, and ADR-0008 on keeping the wire format
  behind dedicated DTOs and source-generated JSON.
- ADR-0009 on keeping provider-specific question types in the provider package.
- `JevDecisionProvider`, the `IDecisionProvider` implementation that calls the service over
  an injected `HttpClient`. The client is never created or disposed by the provider.
- `IJevCredentialProvider`, asked for a key on every call so that rotation takes effect
  without rebuilding the provider, with `StaticJevCredentialProvider` and
  `DelegateJevCredentialProvider`.
- `JevApiKey`, a value object whose `ToString` returns `<redacted>` and whose value is
  internal, so a credential cannot reach a log or an exception by accident.
- `JevEndpoint`, which validates the base address at construction: HTTPS outside loopback,
  no userinfo, no query, no fragment, and no `v1` segment, because the library appends it.
- `JevProviderOptions` with independent per-attempt and per-operation timeouts, both driven
  by the injected `TimeProvider` so that a test can expire them without waiting.
- Transport failures the service never gets to report: `jev_credential_missing`,
  `jev_timeout` carrying the expired scope, and `jev_transport_failure`.
- Structured, source-generated logging of provider, operation, status, elapsed time and
  provider request identifier, and of nothing else.
- Transport documentation covering the endpoint, the credential, header precedence, the two
  timeout budgets and the failures that arrive without a status code.
- `JevRetryPolicy`, an immutable, validated retry policy with attempt and elapsed-time
  limits, exponential backoff, a delay ceiling, jitter, `Retry-After` support and per-status
  overrides. `JevRetryPolicy.None` is the default and makes exactly one attempt;
  `JevRetryPolicy.Standard` is the ready-made alternative.
- `JevProviderOptions.RetryDelayTimeout`, the third budget, bounding a single wait between
  two attempts. A wait that exceeds it abandons the retry instead of being shortened, so a
  `Retry-After` is obeyed exactly or not at all.
- `DecisionIdempotencyPolicy`, the provider-neutral vocabulary for deduplicating a repeated
  call. JEV accepts only `Disabled`, and says so while the options are being built.
- `jev.retry_attempts` on a failure that survived more than one attempt, so a caller can
  tell a single failed request from a retried one that never recovered.
- Retry logging: a scheduled retry, exhausted attempts, and a once-per-operation warning
  that a repeated evaluation is charged again and may answer differently.
- Retry documentation and ADR-0010 on why retrying is opt-in.
- `DecisionKit.Extensions`: `AddDecisionKit()` and six `AddJevProvider(...)` overloads that
  register the JEV provider as a singleton with an `IHttpClientFactory` client, the
  application's logger and the application's `TimeProvider`.
- `JevProviderSettings` and `JevRetrySettings`, the bindable description of a JEV
  registration. Every member is nullable, so an unmentioned key keeps its default.
- Configuration binding without reflection, so the package stays trim- and AOT-clean. A value
  that cannot be read is reported by configuration path, value and expectation.
- Startup validation through `ValidateOnStart()`: a missing credential, an unusable endpoint,
  a timeout that cannot elapse and an impossible retry policy stop the host from starting,
  each message naming the registration it belongs to and never quoting the API key.
- `WithApiKey(...)` and `WithCredentials(...)` for a credential that does not come from
  configuration, and `Configure(...)` for settings supplied in code after binding.
- `Decorate(...)`, which wraps the registered provider without the application re-registering
  it. The last decorator added is the outermost one.
- Named registrations: options, HTTP client, credential provider and provider are keyed by
  the registration name, so several JEV providers coexist. The first is also resolvable
  without a key.
- `IHttpClientBuilder` access through `IJevProviderBuilder.HttpClient`, so an application can
  add its own handlers. The client's own timeout is infinite so that DecisionKit's three
  budgets are the only ones that fire.
- Dependency injection documentation and ADR-0011 on binding configuration onto a separate
  mutable settings type.
- Public API tracking with `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Every public symbol
  of every shipping package is declared in `PublicAPI.Shipped.txt` or
  `PublicAPI.Unshipped.txt`, so an addition or a removal is visible in the diff of the pull
  request that made it.
- Package validation, and the `DecisionKitBaselineVersion` property that switches on
  validation against the previously released package once `0.1.0` exists on nuget.org.
- `samples/Basic`: minimal decision, typed choice, score, multiple questions, error
  handling, cancellation and unknown question types, all running offline.
- `samples/DependencyInjection`: an ASP.NET Core minimal API binding configuration, resolving
  the provider from the container and mapping every failure category onto a `ProblemDetails`
  response.
- `samples/CustomProvider`: a complete `IDecisionProvider` implementation backed by an
  in-house rules engine, which references no provider package.
- `samples/Testing`: application logic asserted with `FakeDecisionProvider` and
  `DecisionAssert`, exiting non-zero when an expectation fails.
- `samples/Resilience`: the backoff table, `Retry-After` precedence, failure classification
  and the refusal to pretend JEV supports idempotency keys.
- `samples/Aot`: a Native AOT smoke test covering configuration binding, container
  composition, wire serialization and generic answer mapping. It publishes with `TrimMode=full`
  and `TrimmerSingleWarn=false`, so any reflection introduced in a shipping package fails the
  publish.
- `benchmarks/DecisionKit.Benchmarks`: BenchmarkDotNet measurements for question set and
  request construction, typed and untyped answer lookup, and the JEV mapping and
  serialization round trip.
- CI jobs for the Native AOT publish, the offline samples and a dry run of every benchmark.
- Documentation set: getting started, type safety, error handling, AOT and trimming, FAQ,
  the JEV provider guide and writing a provider.

### Changed

- `DecisionProviderCapabilities.SupportedQuestionTypes` accepts a generic type definition.
  `typeof(ChoiceQuestion<>)` declares every closed form of it, which is the only way a
  provider can declare support for a question type the caller closes.
- `ChoiceQuestion<TOption>` implements the new `IChoiceQuestion`, so code holding a
  `Question` base reference can read the options and build the answer without reflection.
  It also exposes a typed `CreateAnswer(TOption)`.
- `IChoiceQuestion` gained `CreateAnswer(ChoiceOutcome)`, which is the only non-generic way
  to build a choice answer that keeps its metadata. `ChoiceQuestion<TOption>` also exposes
  typed distribution overloads of `CreateAnswer`.

[Unreleased]: https://github.com/iamjonatha/decisionkit-dotnet/commits/main
