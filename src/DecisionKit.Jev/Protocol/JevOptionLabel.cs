using System;
using System.Globalization;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Projects the options of a choice question onto the strings the JEV protocol uses to name them.
/// </summary>
/// <remarks>
/// <para>
/// JEV names an option with a string, and it names it in two places: as a key of the question's
/// <c>criteria</c> map on the way out, and as the <c>choice</c> and the keys of
/// <c>probabilities</c> on the way back. The domain lets an option be any type, so the two worlds
/// meet here and nowhere else.
/// </para>
/// <para>
/// The projection is deliberately plain — the option's own text, culture-invariantly — so that what
/// the model is asked to choose between is what the application called it. That means the label
/// carries meaning to the model, and an option whose text is an opaque code will be judged on that
/// code.
/// </para>
/// </remarks>
public static class JevOptionLabel
{
    /// <summary>
    /// Names an option the way it travels on the wire.
    /// </summary>
    /// <param name="option">The option to name.</param>
    /// <returns>The wire label, or <see langword="null"/> when the option has no usable text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public static string? For(object option)
    {
        ArgumentNullException.ThrowIfNull(option);

        string? text = option switch
        {
            string value => value,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => option.ToString(),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Determines whether a wire label names an option.
    /// </summary>
    /// <param name="option">The option to test.</param>
    /// <param name="label">The label the provider reported.</param>
    /// <returns><see langword="true"/> when the label names the option.</returns>
    /// <remarks>
    /// The comparison is ordinal and exact. A provider that echoes a label it was given will match;
    /// one that returns something else is reporting an option that was never offered, which the
    /// answer mapper refuses rather than guesses at.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public static bool Matches(object option, string? label) =>
        label is not null && string.Equals(For(option), label, StringComparison.Ordinal);
}
