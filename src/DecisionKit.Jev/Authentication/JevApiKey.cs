using System;
using System.Diagnostics;

namespace DecisionKit.Jev.Authentication;

/// <summary>
/// A JEV API key, held in a form that cannot be logged by accident.
/// </summary>
/// <remarks>
/// <para>
/// A key passed around as a <see cref="string"/> leaks the moment anything interpolates it: a log
/// message, an exception message, a debugger watch, a serialized options object. Wrapping it in a
/// type whose <see cref="ToString"/> is redacted removes the accident. The value itself is visible
/// only inside this package, and only the transport reads it, to build one header.
/// </para>
/// <para>
/// The key is not validated against a format. The service issues keys prefixed with <c>ts_</c>
/// today, but a prefix is a convention the service is free to change, and rejecting a key the
/// service would have accepted is worse than forwarding one it rejects.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct JevApiKey : IEquatable<JevApiKey>
{
    private const string RedactedText = "<redacted>";

    /// <summary>
    /// Initializes a new instance of the <see cref="JevApiKey"/> struct.
    /// </summary>
    /// <param name="value">The key issued by the service.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public JevApiKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Gets the absence of a key.
    /// </summary>
    /// <remarks>
    /// A credential provider returns this to say it has nothing to offer, which the transport
    /// reports as an authentication failure rather than sending an unauthenticated request.
    /// </remarks>
    public static JevApiKey None => default;

    /// <summary>
    /// Gets a value indicating whether this instance holds a key.
    /// </summary>
    public bool IsPresent => Value is not null;

    /// <summary>
    /// Gets the key itself. Visible only to the transport that puts it on a header.
    /// </summary>
    internal string? Value { get; }

    /// <summary>
    /// Determines whether two keys are the same.
    /// </summary>
    /// <param name="other">The key to compare with.</param>
    /// <returns><see langword="true"/> when both hold the same value, or both hold none.</returns>
    public bool Equals(JevApiKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JevApiKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    /// Returns a redacted description of the key.
    /// </summary>
    /// <returns>
    /// Always a fixed placeholder. The key never appears in the text of anything, including a
    /// message this library did not write.
    /// </returns>
    public override string ToString() => RedactedText;

    /// <summary>
    /// Determines whether two keys are the same.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool operator ==(JevApiKey left, JevApiKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two keys differ.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(JevApiKey left, JevApiKey right) => !left.Equals(right);
}
