using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Results;

namespace DecisionKit.Testing.Recording;

/// <summary>
/// Wraps any provider and records what it was asked, without changing what it answers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Providers.FakeDecisionProvider"/> already records its own calls, so this decorator is
/// for the cases it cannot cover: capturing what a deterministic provider, a real provider, or a
/// consumer's own provider was asked, and asserting on it afterwards.
/// </para>
/// <para>
/// It is also the smallest possible proof that <see cref="IDecisionProvider"/> is decorable, which
/// the architecture relies on for retry, caching, logging and metrics.
/// </para>
/// </remarks>
public sealed class RecordingDecisionProvider : IDecisionProvider
{
    private readonly IDecisionProvider _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingDecisionProvider"/> class.
    /// </summary>
    /// <param name="inner">The provider to record and delegate to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public RecordingDecisionProvider(IDecisionProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public DecisionProviderCapabilities Capabilities => _inner.Capabilities;

    /// <summary>
    /// Gets the log of the calls this decorator observed.
    /// </summary>
    public DecisionCallLog Calls { get; } = new();

    /// <summary>
    /// Gets the number of calls this decorator observed.
    /// </summary>
    public int CallCount => Calls.Count;

    /// <summary>
    /// Gets or sets the clock used to timestamp recorded calls.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned provider is <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = TimeProvider.System;

    /// <inheritdoc />
    public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Calls.Record(request, TimeProvider.GetUtcNow());

        return _inner.DecideAsync(request, cancellationToken);
    }
}
