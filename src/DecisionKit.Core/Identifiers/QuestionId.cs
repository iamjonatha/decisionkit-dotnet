using System;

namespace DecisionKit.Identifiers;

/// <summary>
/// Identifies a question inside a decision request and correlates it with the matching answer
/// in the decision result.
/// </summary>
/// <remarks>
/// A <see cref="QuestionId"/> is a validated value object rather than a bare string: it guarantees a
/// non-empty, trimmed value and provides value-based equality so that results can be looked up
/// reliably. Comparison is ordinal and case-sensitive, because provider protocols treat identifiers
/// as opaque tokens.
/// </remarks>
public readonly struct QuestionId : IEquatable<QuestionId>
{
    private readonly string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionId"/> struct.
    /// </summary>
    /// <param name="value">The identifier value. Leading and trailing whitespace is removed.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    public QuestionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A question identifier must be a non-empty, non-whitespace string.", nameof(value));
        }

        _value = value.Trim();
    }

    /// <summary>
    /// Gets the identifier value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The identifier was created through <see langword="default"/>.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The question identifier was not initialized.");

    /// <summary>
    /// Gets a value indicating whether this identifier holds a usable value.
    /// </summary>
    public bool IsEmpty => _value is null;

    /// <summary>
    /// Attempts to create a <see cref="QuestionId"/> without throwing.
    /// </summary>
    /// <param name="value">The candidate identifier value.</param>
    /// <param name="questionId">The created identifier when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid identifier; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(string? value, out QuestionId questionId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            questionId = default;
            return false;
        }

        questionId = new QuestionId(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(QuestionId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is QuestionId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both identifiers hold the same value.</returns>
    public static bool operator ==(QuestionId left, QuestionId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the identifiers hold different values.</returns>
    public static bool operator !=(QuestionId left, QuestionId right) => !left.Equals(right);
}
