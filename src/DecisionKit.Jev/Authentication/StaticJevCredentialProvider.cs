using System;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionKit.Jev.Authentication;

/// <summary>
/// Supplies one key, fixed for the lifetime of the instance.
/// </summary>
/// <remarks>
/// This is the right implementation when the key comes from configuration read at startup and the
/// process is restarted to rotate it. When the key can change while the process runs, use
/// <see cref="DelegateJevCredentialProvider"/> instead, which is re-read on every request.
/// </remarks>
public sealed class StaticJevCredentialProvider : IJevCredentialProvider
{
    private readonly JevApiKey _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticJevCredentialProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The key to authenticate every request with.</param>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> holds no key.</exception>
    public StaticJevCredentialProvider(JevApiKey apiKey)
    {
        if (!apiKey.IsPresent)
        {
            throw new ArgumentException(
                "A static credential provider needs a key. Use a delegate provider when the key is not known yet.",
                nameof(apiKey));
        }

        _apiKey = apiKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticJevCredentialProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The key to authenticate every request with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is empty or whitespace.</exception>
    public StaticJevCredentialProvider(string apiKey)
        : this(new JevApiKey(apiKey))
    {
    }

    /// <inheritdoc />
    public ValueTask<JevApiKey> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new ValueTask<JevApiKey>(_apiKey);
    }
}
