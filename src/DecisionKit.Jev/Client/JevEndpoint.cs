using System;
using System.Globalization;
using DecisionKit.Jev.Protocol;

namespace DecisionKit.Jev.Client;

/// <summary>
/// Where the JEV service lives, validated once instead of on every call.
/// </summary>
/// <remarks>
/// <para>
/// A base address is easy to get subtly wrong, and every way of getting it wrong fails at run time
/// with a message about something else. Pointing at <c>http://</c> sends the API key in clear text.
/// Including the version segment produces <c>/v1/v1/systemone</c> and a 404 that looks like an
/// outage. A missing trailing slash silently drops the last segment of a proxy prefix, because that
/// is what <see cref="Uri"/> relative resolution does.
/// </para>
/// <para>
/// This type refuses all three at construction, where the mistake is still attached to the line of
/// configuration that caused it. A bad address is a programming or deployment error rather than a
/// failed decision, so it is reported as an <see cref="ArgumentException"/> and never as a
/// <see cref="DecisionKit.Errors.DecisionException"/>.
/// </para>
/// <para>
/// Plain HTTP is allowed for a loopback address only, so that a local stub or a recording proxy
/// stays usable in tests without a certificate.
/// </para>
/// </remarks>
public sealed class JevEndpoint : IEquatable<JevEndpoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JevEndpoint"/> class.
    /// </summary>
    /// <param name="baseAddress">The address the service is reached at, without the version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseAddress"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The address is not usable as a JEV base address.</exception>
    public JevEndpoint(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        BaseAddress = Validate(baseAddress, nameof(baseAddress));
        Evaluate = new Uri(BaseAddress, JevProtocol.EvaluatePath);
        Models = new Uri(BaseAddress, JevProtocol.ModelsPath);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JevEndpoint"/> class.
    /// </summary>
    /// <param name="baseAddress">The address the service is reached at, without the version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseAddress"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The address is empty, not absolute, or not usable.</exception>
    public JevEndpoint(string baseAddress)
        : this(Parse(baseAddress))
    {
    }

    /// <summary>
    /// Gets the address of the public JEV service.
    /// </summary>
    public static JevEndpoint Default { get; } = new(new Uri(JevProtocol.DefaultBaseAddress));

    /// <summary>
    /// Gets the configured address, normalized to end with a slash.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Gets the absolute address of the evaluation endpoint.
    /// </summary>
    public Uri Evaluate { get; }

    /// <summary>
    /// Gets the absolute address of the model listing endpoint.
    /// </summary>
    public Uri Models { get; }

    /// <summary>
    /// Determines whether two endpoints address the same service.
    /// </summary>
    /// <param name="other">The endpoint to compare with.</param>
    /// <returns><see langword="true"/> when both have the same base address.</returns>
    public bool Equals(JevEndpoint? other) => other is not null && BaseAddress == other.BaseAddress;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as JevEndpoint);

    /// <inheritdoc />
    public override int GetHashCode() => BaseAddress.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => BaseAddress.AbsoluteUri;

    private static Uri Parse(string baseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        if (!Uri.TryCreate(baseAddress.Trim(), UriKind.Absolute, out Uri? parsed))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"'{baseAddress}' is not an absolute URI."),
                nameof(baseAddress));
        }

        return parsed;
    }

    private static Uri Validate(Uri baseAddress, string parameterName)
    {
        if (!baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "A JEV base address must be absolute, because it is what every request is resolved against.",
                parameterName);
        }

        RequireSupportedScheme(baseAddress, parameterName);
        RequireNoCredentials(baseAddress, parameterName);
        RequireNoQueryOrFragment(baseAddress, parameterName);
        RequireNoVersionSegment(baseAddress, parameterName);

        return EnsureTrailingSlash(baseAddress);
    }

    private static void RequireSupportedScheme(Uri baseAddress, string parameterName)
    {
        if (baseAddress.Scheme == Uri.UriSchemeHttps)
        {
            return;
        }

        if (baseAddress.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"A JEV base address must use http or https, not '{baseAddress.Scheme}'."),
                parameterName);
        }

        if (!baseAddress.IsLoopback)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"'{baseAddress}' uses plain HTTP, which would send the API key in clear text. HTTPS is required for anything but a loopback address."),
                parameterName);
        }
    }

    private static void RequireNoCredentials(Uri baseAddress, string parameterName)
    {
        if (!string.IsNullOrEmpty(baseAddress.UserInfo))
        {
            throw new ArgumentException(
                "A JEV base address carries no user information. Credentials travel on the Authorization header, where they can be redacted, not in a URI that ends up in logs and diagnostics.",
                parameterName);
        }
    }

    private static void RequireNoQueryOrFragment(Uri baseAddress, string parameterName)
    {
        if (!string.IsNullOrEmpty(baseAddress.Query) || !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new ArgumentException(
                "A JEV base address carries no query string and no fragment; both would be dropped when a request path is appended.",
                parameterName);
        }
    }

    private static void RequireNoVersionSegment(Uri baseAddress, string parameterName)
    {
        foreach (string segment in baseAddress.Segments)
        {
            if (!segment.Trim('/').Equals(JevProtocol.VersionSegment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"'{baseAddress}' already contains the '{JevProtocol.VersionSegment}' path segment, which DecisionKit appends itself. Configure the address without it."),
                parameterName);
        }
    }

    private static Uri EnsureTrailingSlash(Uri baseAddress)
    {
        // Relative resolution drops everything after the last slash, so an address configured as a
        // proxy prefix would silently lose its last segment without this.
        if (baseAddress.AbsolutePath.EndsWith('/'))
        {
            return baseAddress;
        }

        UriBuilder builder = new(baseAddress) { Path = baseAddress.AbsolutePath + "/" };

        return builder.Uri;
    }
}
