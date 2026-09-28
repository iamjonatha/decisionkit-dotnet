using System.Threading;
using System.Threading.Tasks;

namespace DecisionKit.Jev.Authentication;

/// <summary>
/// Supplies the API key the transport authenticates with.
/// </summary>
/// <remarks>
/// <para>
/// The transport asks for a key on every call rather than capturing one at construction. That is
/// what makes key rotation possible without rebuilding the provider: an implementation backed by a
/// secret store, a token service or a configuration reload returns the current value, and the next
/// request uses it.
/// </para>
/// <para>
/// The method is asynchronous because fetching a secret can involve I/O, and returns a
/// <see cref="ValueTask{TResult}"/> because the common case — a key already in memory — should
/// allocate nothing.
/// </para>
/// <para>
/// Implementations must be safe for concurrent use. The provider is normally a singleton and calls
/// this from every request in flight.
/// </para>
/// </remarks>
public interface IJevCredentialProvider
{
    /// <summary>
    /// Gets the key to authenticate the next request with.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the lookup.</param>
    /// <returns>
    /// The current key, or <see cref="JevApiKey.None"/> when no key is available. Returning none is
    /// reported as an authentication failure; it never results in an unauthenticated request.
    /// </returns>
    /// <exception cref="System.OperationCanceledException">The lookup was cancelled.</exception>
    ValueTask<JevApiKey> GetApiKeyAsync(CancellationToken cancellationToken);
}
