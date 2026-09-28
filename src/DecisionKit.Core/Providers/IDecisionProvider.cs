using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Results;

namespace DecisionKit.Providers;

/// <summary>
/// Answers decision requests. This is the one contract an application depends on, and the one
/// contract a provider implements.
/// </summary>
/// <remarks>
/// <para>
/// The interface is intentionally small: a name, a capability declaration, and one asynchronous
/// operation. Everything a provider needs beyond that — endpoints, credentials, retry budgets,
/// serialization — is its own construction detail and never appears here.
/// </para>
/// <para>
/// Being this small also makes the interface easy to wrap. Logging, metrics, retry and caching are
/// added by decorating an implementation, not by editing one.
/// </para>
/// <para>
/// Implementations are expected to be safe for concurrent use, because they are normally registered
/// as singletons. Per-request state belongs to the request, never to the provider.
/// </para>
/// </remarks>
public interface IDecisionProvider
{
    /// <summary>
    /// Gets the name of this provider, as it appears in
    /// <see cref="DecisionMetadata.ProviderName"/> and in diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what this provider supports.
    /// </summary>
    DecisionProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Evaluates a decision request.
    /// </summary>
    /// <param name="request">The decision to evaluate.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The answers the provider produced.</returns>
    /// <remarks>
    /// Cancellation is part of this contract, not an implementation detail: the token outranks any
    /// retry policy, and a cancelled call throws
    /// <see cref="System.OperationCanceledException"/> without making a further attempt. Every other
    /// failure is reported as a <see cref="Errors.DecisionException"/>, so that the caller can tell
    /// a bad request from an expired credential from a rate limit without parsing a message.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="Errors.DecisionException">The provider could not answer the request.</exception>
    /// <exception cref="System.OperationCanceledException">The call was cancelled.</exception>
    Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);
}
