using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DecisionKit.Internal;

/// <summary>
/// Creates immutable snapshots of the provider-neutral metadata bags carried by questions,
/// answers and results.
/// </summary>
internal static class MetadataSnapshot
{
    /// <summary>
    /// Gets the shared empty metadata bag.
    /// </summary>
    internal static IReadOnlyDictionary<string, object?> Empty { get; } = ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>
    /// Copies <paramref name="source"/> into an immutable, ordinal-keyed dictionary so that later
    /// mutations of the caller's collection cannot change the snapshot.
    /// </summary>
    /// <param name="source">The metadata to copy.</param>
    /// <returns>An immutable metadata bag.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyDictionary<string, object?> Create(IReadOnlyDictionary<string, object?> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Count == 0)
        {
            return Empty;
        }

        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(source, StringComparer.Ordinal));
    }
}
