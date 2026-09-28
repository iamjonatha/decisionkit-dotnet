using System;
using System.Collections.Generic;
using System.Globalization;

namespace DecisionKit.Values;

/// <summary>
/// The non-generic view of an option and the probability assigned to it.
/// </summary>
/// <remarks>
/// Mappers and test doubles receive a choice question as <see cref="Questions.IChoiceQuestion"/>,
/// which does not expose the option type. They still have to describe a distribution, so this type
/// carries the same pair with the option typed as <see cref="object"/>. The typed
/// <see cref="OptionProbability{TOption}"/> stays the primary API.
/// </remarks>
public readonly struct OptionProbability : IEquatable<OptionProbability>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionProbability"/> struct.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="probability">The probability assigned to the option.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public OptionProbability(object option, Probability probability)
    {
        ArgumentNullException.ThrowIfNull(option);

        Option = option;
        Probability = probability;
    }

    /// <summary>
    /// Gets the option.
    /// </summary>
    public object? Option { get; }

    /// <summary>
    /// Gets the probability assigned to the option.
    /// </summary>
    public Probability Probability { get; }

    /// <summary>
    /// Gets a value indicating whether this instance carries an option.
    /// </summary>
    public bool IsEmpty => Option is null;

    /// <inheritdoc />
    public bool Equals(OptionProbability other) =>
        Equals(Option, other.Option) && Probability.Equals(other.Probability);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OptionProbability other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Option, Probability);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Option} = {Probability}");

    /// <summary>Determines whether two option probabilities are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both hold the same option and the same probability.</returns>
    public static bool operator ==(OptionProbability left, OptionProbability right) => left.Equals(right);

    /// <summary>Determines whether two option probabilities are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when they differ in option or in probability.</returns>
    public static bool operator !=(OptionProbability left, OptionProbability right) => !left.Equals(right);
}

/// <summary>
/// Associates one option of a choice with the probability the provider assigned to it.
/// </summary>
/// <typeparam name="TOption">The type of the option.</typeparam>
public readonly struct OptionProbability<TOption> : IEquatable<OptionProbability<TOption>>
    where TOption : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionProbability{TOption}"/> struct.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="probability">The probability assigned to the option.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public OptionProbability(TOption option, Probability probability)
    {
        ArgumentNullException.ThrowIfNull(option);

        Option = option;
        Probability = probability;
    }

    /// <summary>
    /// Gets the option.
    /// </summary>
    public TOption Option { get; }

    /// <summary>
    /// Gets the probability assigned to the option.
    /// </summary>
    public Probability Probability { get; }

    /// <summary>
    /// Gets a value indicating whether this instance carries an option.
    /// </summary>
    public bool IsEmpty => Option is null;

    /// <inheritdoc />
    public bool Equals(OptionProbability<TOption> other) =>
        EqualityComparer<TOption>.Default.Equals(Option, other.Option) && Probability.Equals(other.Probability);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OptionProbability<TOption> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Option, Probability);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Option} = {Probability}");

    /// <summary>Determines whether two option probabilities are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both hold the same option and the same probability.</returns>
    public static bool operator ==(OptionProbability<TOption> left, OptionProbability<TOption> right) => left.Equals(right);

    /// <summary>Determines whether two option probabilities are different.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when they differ in option or in probability.</returns>
    public static bool operator !=(OptionProbability<TOption> left, OptionProbability<TOption> right) => !left.Equals(right);
}
