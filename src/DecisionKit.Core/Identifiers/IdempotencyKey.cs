using System;
using System.Globalization;

namespace DecisionKit.Identifiers;

/// <summary>
/// Marks a decision request as safely repeatable, so that a retried call is recognized by the
/// provider as the same logical operation instead of a second one.
/// </summary>
/// <remarks>
/// Retrying a non-idempotent request is an explicit decision, never an implicit side effect of a
/// transport policy. An idempotency key is therefore part of the domain, not of the HTTP client.
/// The same key must be reused for every attempt of one logical operation, and must not be reused
/// across different operations.
/// </remarks>
public readonly struct IdempotencyKey : IEquatable<IdempotencyKey>
{
    private readonly string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyKey"/> struct.
    /// </summary>
    /// <param name="value">The key value. Leading and trailing whitespace is removed.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An idempotency key must be a non-empty, non-whitespace string.", nameof(value));
        }

        _value = value.Trim();
    }

    /// <summary>
    /// Gets the key value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The key was created through <see langword="default"/>.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The idempotency key was not initialized.");

    /// <summary>
    /// Gets a value indicating whether this key holds a usable value.
    /// </summary>
    public bool IsEmpty => _value is null;

    /// <summary>
    /// Creates a new, unique idempotency key.
    /// </summary>
    /// <returns>An idempotency key backed by a freshly generated globally unique value.</returns>
    public static IdempotencyKey New() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    /// <summary>
    /// Attempts to create an <see cref="IdempotencyKey"/> without throwing.
    /// </summary>
    /// <param name="value">The candidate key value.</param>
    /// <param name="idempotencyKey">The created key when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid key; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(string? value, out IdempotencyKey idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            idempotencyKey = default;
            return false;
        }

        idempotencyKey = new IdempotencyKey(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(IdempotencyKey other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IdempotencyKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    /// <summary>Determines whether two keys are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both keys hold the same value.</returns>
    public static bool operator ==(IdempotencyKey left, IdempotencyKey right) => left.Equals(right);

    /// <summary>Determines whether two keys are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the keys hold different values.</returns>
    public static bool operator !=(IdempotencyKey left, IdempotencyKey right) => !left.Equals(right);
}
