using System;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionKit.Jev.Authentication;

/// <summary>
/// Asks a delegate for the key on every request.
/// </summary>
/// <remarks>
/// <para>
/// This is the implementation to use when the key can change while the process runs: it is read
/// from a secret store, refreshed by a background job, or reloaded from configuration. Because the
/// delegate is called per request, a rotation takes effect on the next call with nothing to
/// restart.
/// </para>
/// <para>
/// The delegate runs on the request path, so anything expensive in it belongs behind a cache the
/// delegate owns. This type deliberately does not cache: a cache it owned would have to guess an
/// expiry, and guessing wrong is exactly the failure this type exists to avoid.
/// </para>
/// </remarks>
public sealed class DelegateJevCredentialProvider : IJevCredentialProvider
{
    private readonly Func<CancellationToken, ValueTask<JevApiKey>> _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateJevCredentialProvider"/> class.
    /// </summary>
    /// <param name="source">The delegate that produces the current key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public DelegateJevCredentialProvider(Func<CancellationToken, ValueTask<JevApiKey>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateJevCredentialProvider"/> class over a
    /// synchronous delegate.
    /// </summary>
    /// <param name="source">The delegate that produces the current key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public DelegateJevCredentialProvider(Func<JevApiKey> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = _ => new ValueTask<JevApiKey>(source());
    }

    /// <inheritdoc />
    public ValueTask<JevApiKey> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _source(cancellationToken);
    }
}
