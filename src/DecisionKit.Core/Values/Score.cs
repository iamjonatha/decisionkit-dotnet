using System;
using System.Globalization;

namespace DecisionKit.Values;

/// <summary>
/// Represents a numeric evaluation together with the scale it was measured on.
/// </summary>
/// <remarks>
/// A bare <see cref="double"/> is ambiguous: <c>7</c> means nothing until the reader knows whether
/// the scale is <c>0..10</c>, <c>1..5</c> or <c>0..100</c>. A <see cref="Score"/> therefore always
/// carries its own bounds, and normalization to <c>[0.0, 1.0]</c> is an explicit operation rather
/// than an assumption baked into the consumer.
/// </remarks>
public readonly struct Score : IEquatable<Score>, IFormattable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Score"/> struct.
    /// </summary>
    /// <param name="value">The measured value.</param>
    /// <param name="minimum">The smallest value the scale can produce.</param>
    /// <param name="maximum">The largest value the scale can produce.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is <see cref="double.NaN"/> or infinite, <paramref name="minimum"/> is not
    /// smaller than <paramref name="maximum"/>, or <paramref name="value"/> falls outside the
    /// scale.
    /// </exception>
    public Score(double value, double minimum, double maximum)
    {
        ValidateBounds(minimum, maximum);
        ValidateValue(value, minimum, maximum);

        Value = value;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// Gets the measured value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the smallest value the scale can produce.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the largest value the scale can produce.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets a value indicating whether this instance carries a usable scale.
    /// </summary>
    /// <remarks>
    /// Only a <see langword="default"/> instance is scaleless. Every score created through the
    /// constructor has <see cref="Minimum"/> strictly smaller than <see cref="Maximum"/>.
    /// </remarks>
    public bool HasScale => Maximum > Minimum;

    /// <summary>
    /// Converts the score to the closed interval <c>[0.0, 1.0]</c>.
    /// </summary>
    /// <returns>The normalized value.</returns>
    /// <exception cref="InvalidOperationException">The score was created through <see langword="default"/>.</exception>
    public double Normalize()
    {
        if (!HasScale)
        {
            throw new InvalidOperationException("The score was not initialized and therefore has no scale to normalize against.");
        }

        return (Value - Minimum) / (Maximum - Minimum);
    }

    /// <summary>
    /// Attempts to create a <see cref="Score"/> without throwing.
    /// </summary>
    /// <param name="value">The measured value.</param>
    /// <param name="minimum">The smallest value the scale can produce.</param>
    /// <param name="maximum">The largest value the scale can produce.</param>
    /// <param name="score">The created score when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the arguments describe a valid score; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(double value, double minimum, double maximum, out Score score)
    {
        if (!IsFinite(minimum) || !IsFinite(maximum) || !IsFinite(value) || minimum >= maximum || value < minimum || value > maximum)
        {
            score = default;
            return false;
        }

        score = new Score(value, minimum, maximum);
        return true;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void ValidateBounds(double minimum, double maximum)
    {
        if (!IsFinite(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "A score bound must be a finite number.");
        }

        if (!IsFinite(maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "A score bound must be a finite number.");
        }

        if (minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "The minimum of a score must be strictly smaller than its maximum.");
        }
    }

    private static void ValidateValue(double value, double minimum, double maximum)
    {
        if (!IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A score value must be a finite number.");
        }

        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A score value must fall inside its own scale.");
        }
    }

    /// <inheritdoc />
    public bool Equals(Score other) =>
        Value.Equals(other.Value) && Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Score other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Value, Minimum, Maximum);

    /// <inheritdoc />
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        IFormatProvider provider = formatProvider ?? CultureInfo.InvariantCulture;

        return string.Create(
            provider,
            $"{Value.ToString(format, provider)} [{Minimum.ToString(format, provider)}..{Maximum.ToString(format, provider)}]");
    }

    /// <summary>Determines whether two scores are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both scores hold the same value and the same scale.</returns>
    public static bool operator ==(Score left, Score right) => left.Equals(right);

    /// <summary>Determines whether two scores are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the scores differ in value or in scale.</returns>
    public static bool operator !=(Score left, Score right) => !left.Equals(right);
}
