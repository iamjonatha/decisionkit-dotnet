# Documentation

| Document | Purpose |
| --- | --- |
| [Getting started](guides/getting-started.md) | From installing a package to reading a typed answer |
| [Roadmap](roadmap.md) | What exists today, and what is being considered next |
| [Architecture overview](architecture/overview.md) | Layers, boundaries, data flow and non-functional constraints |
| [Domain model](architecture/domain-model.md) | Questions, answers, values and results, and the rules they follow |
| [Provider contract](architecture/provider-contract.md) | The request, the provider interface, capabilities and the failure model |
| [Type safety](guides/type-safety.md) | How a question decides the type of its answer, and where the type system stops |
| [Error handling](guides/error-handling.md) | Categories, exception types, retry hints and cancellation |
| [Testing](guides/testing.md) | Testing application code with fake and deterministic providers |
| [Dependency injection](guides/dependency-injection.md) | Registering a provider in a host, and every setting it reads |
| [AOT and trimming](guides/aot-and-trimming.md) | What is guaranteed, how it is enforced, and what you have to do |
| [JEV provider](providers/jev.md) | Using the JEV provider: credentials, rubrics, timeouts, retrying, failure codes |
| [JEV wire protocol](providers/jev-protocol.md) | The JEV payload, the transport and resilience around it, and the rules that map it to and from the domain |
| [Writing a provider](providers/custom-provider.md) | Implementing `IDecisionProvider` for a service DecisionKit does not ship |
| [Releasing](guides/releasing.md) | Versioning rules and the release procedure |
| [Handling secrets](guides/secrets.md) | Where to store a provider credential, and what the library guarantees |
| [FAQ](guides/faq.md) | The questions that come up before the first pull request |
| [Decision records](adr/) | Why the architecture is what it is, and what was rejected |

Runnable code lives outside this folder: [`samples/`](../samples) for six worked examples and
[`benchmarks/`](../benchmarks) for the measured hot paths.

## Rules

- Documentation is written in English, like everything else in this repository.
- A change is not finished until the documentation matches it.
- `architecture/overview.md` is normative. Contradicting it requires an ADR, not a pull
  request comment.
- `adr/` is append-only. A decision is superseded by a new record, never edited into a
  different decision.
