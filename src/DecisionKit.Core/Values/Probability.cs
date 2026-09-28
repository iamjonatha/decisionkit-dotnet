using System;
using System.Globalization;

namespace DecisionKit.Values;

/// <summary>
/// Represents a probability in the closed interval <c>[0.0, 1.0]</c>.
/// </summary>
/// <remarks>
/// Providers routinely return probabilities as bare doubles, which makes it easy to store a
/// percentage, a logit or a score in a field that is documented as a probability. This type makes
/// the range part of the type: <see cref="double.NaN"/> and the infinities are rejected, and the
/// default value is <c>0.0</c>, which is a valid probability.
/// </remarks>
public readonly struct Probability : IEquatable<Probability>, IComparable<Probability>, IFormattable
{
    /// <summary>
    /// The smallest value a probability can hold.
    /// </summary>
    public const double MinimumValue = 0.0;

    /// <summary>
    /// The largest value a probability can hold.
    /// </summary>
    public const double MaximumValue = 1.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Probability"/> struct.
    /// </summary>
    /// <param name="value">The probability value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="double.NaN"/>, infinite, or outside
    /// <c>[0.0, 1.0]</c>.
    /// </exception>
    public Probability(double value)
    {
        if (!IsInRange(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A probability must be a finite number between 0.0 and 1.0 inclusive.");
        }

        Value = value;
    }

    /// <summary>
    /// Gets the probability value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the probability <c>0.0</c>.
    /// </summary>
    public static Probability Zero => default;

    /// <summary>
    /// Gets the probability <c>1.0</c>.
    /// </summary>
    public static Probability One => new(MaximumValue);

    /// <summary>
    /// Attempts to create a <see cref="Probability"/> without throwing.
    /// </summary>
    /// <param name="value">The candidate probability value.</param>
    /// <param name="probability">The created probability when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid probability; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(double value, out Probability probability)
    {
        if (!IsInRange(value))
        {
            probability = default;
            return false;
        }

        probability = new Probability(value);
        return true;
    }

    /// <summary>
    /// Returns the complement of this probability, that is <c>1.0 - Value</c>.
    /// </summary>
    /// <returns>The complementary probability.</returns>
    public Probability Complement() => new(MaximumValue - Value);

    private static bool IsInRange(double value) =>
        !double.IsNaN(value) && value >= MinimumValue && value <= MaximumValue;

    /// <inheritdoc />
    public bool Equals(Probability other) => Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Probability other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(Probability other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    /// <summary>Determines whether two probabilities are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both probabilities hold the same value.</returns>
    public static bool operator ==(Probability left, Probability right) => left.Equals(right);

    /// <summary>Determines whether two probabilities are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the probabilities hold different values.</returns>
    public static bool operator !=(Probability left, Probability right) => !left.Equals(right);

    /// <summary>Determines whether the left probability is smaller than the right one.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is smaller.</returns>
    public static bool operator <(Probability left, Probability right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left probability is smaller than or equal to the right one.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is smaller or equal.</returns>
    public static bool operator <=(Probability left, Probability right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left probability is greater than the right one.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is greater.</returns>
    public static bool operator >(Probability left, Probability right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left probability is greater than or equal to the right one.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is greater or equal.</returns>
    public static bool operator >=(Probability left, Probability right) => left.CompareTo(right) >= 0;
}
