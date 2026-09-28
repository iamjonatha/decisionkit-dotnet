using System;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// What is known about a failed JEV call besides its body.
/// </summary>
/// <remarks>
/// JEV reports very little about a failure in the payload: what kind of failure it was comes from
/// the HTTP status code, and when to try again comes from the response headers. Neither is part of
/// the body, so both are supplied here rather than read from it. Modelling them as plain values —
/// a number and a duration — keeps <see cref="ErrorMapper"/> free of any HTTP type, so it can be
/// exercised without a transport.
/// </remarks>
public readonly struct JevErrorContext : IEquatable<JevErrorContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JevErrorContext"/> struct.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the service replied with.</param>
    public JevErrorContext(int statusCode)
        : this(statusCode, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JevErrorContext"/> struct.
    /// </summary>
    /// <param name="statusCode">
    /// The HTTP status code the service replied with, or <see langword="null"/> when the call
    /// failed before one arrived.
    /// </param>
    /// <param name="retryAfter">
    /// How long the service asked the caller to wait, as reported by its headers, or
    /// <see langword="null"/> when it asked for nothing.
    /// </param>
    /// <param name="requestId">
    /// The identifier the service assigned the call, which travels in a response header.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryAfter"/> is negative.</exception>
    public JevErrorContext(int? statusCode, TimeSpan? retryAfter, string? requestId)
    {
        if (retryAfter is { } delay && delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter), retryAfter, "A retry delay cannot be negative.");
        }

        StatusCode = statusCode;
        RetryAfter = retryAfter;
        RequestId = requestId;
    }

    /// <summary>
    /// Gets a context that knows nothing, for a failure that produced no response at all.
    /// </summary>
    public static JevErrorContext None => default;

    /// <summary>
    /// Gets the HTTP status code the service replied with.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets how long the service asked the caller to wait.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the identifier the service assigned the call.
    /// </summary>
    public string? RequestId { get; }

    /// <inheritdoc />
    public bool Equals(JevErrorContext other) =>
        StatusCode == other.StatusCode &&
        RetryAfter == other.RetryAfter &&
        string.Equals(RequestId, other.RequestId, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JevErrorContext other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StatusCode, RetryAfter, RequestId);

    /// <summary>
    /// Determines whether two contexts describe the same failure.
    /// </summary>
    /// <param name="left">The first context.</param>
    /// <param name="right">The second context.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool operator ==(JevErrorContext left, JevErrorContext right) => left.Equals(right);

    /// <summary>
    /// Determines whether two contexts describe different failures.
    /// </summary>
    /// <param name="left">The first context.</param>
    /// <param name="right">The second context.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(JevErrorContext left, JevErrorContext right) => !left.Equals(right);
}
