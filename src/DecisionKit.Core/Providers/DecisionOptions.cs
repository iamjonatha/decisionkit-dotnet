using System;

namespace DecisionKit.Providers;

/// <summary>
/// The options that change what a decision means, independently of who answers it.
/// </summary>
/// <remarks>
/// <para>
/// Only semantics belong here. A timeout, a retry budget, an endpoint, a header or a credential
/// changes how a call is made, not what is being asked, so it lives in the transport options of the
/// provider package. A JEV-specific switch lives in the JEV options. The test is simple: if the
/// setting would be meaningless for a second provider, it does not belong in this type.
/// </para>
/// <para>
/// Instances are immutable. Reuse <see cref="Default"/> when nothing needs changing.
/// </para>
/// </remarks>
public sealed class DecisionOptions
{
    /// <summary>
    /// Gets the options every request uses when the caller sets none.
    /// </summary>
    public static DecisionOptions Default { get; } = new();

    /// <summary>
    /// Gets the language the provider should answer in, as a BCP 47 tag such as <c>en-GB</c>.
    /// </summary>
    /// <remarks>
    /// This is semantic: it changes the content of an answer, not the way the call travels.
    /// </remarks>
    /// <exception cref="ArgumentException">The assigned language is empty or whitespace.</exception>
    public string? Language
    {
        get;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A language cannot be empty or whitespace. Pass null instead.", nameof(value));
            }

            field = value?.Trim();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the provider is asked to explain its answers.
    /// </summary>
    /// <remarks>
    /// Providers that cannot explain ignore this. Check
    /// <see cref="DecisionProviderCapabilities"/> rather than assuming an explanation will arrive.
    /// </remarks>
    public bool IncludeExplanations { get; init; }

    /// <summary>
    /// Gets what the provider does with a type it does not recognize. Defaults to
    /// <see cref="UnknownTypeHandling.Preserve"/>.
    /// </summary>
    public UnknownTypeHandling UnknownTypeHandling { get; init; }
}
