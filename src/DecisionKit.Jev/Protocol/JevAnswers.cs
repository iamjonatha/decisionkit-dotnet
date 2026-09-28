using System;
using System.Collections.Generic;
using DecisionKit.Answers;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Reads the JEV-specific detail an answer carries.
/// </summary>
/// <remarks>
/// <para>
/// JEV reports three things the provider-neutral domain does not model: how sure it was, the rubric
/// a score was measured against, and the distribution behind a score. They travel as answer
/// metadata so that <see cref="Answer"/> stays the same for every provider, and they are read back
/// here rather than by each caller reaching for a string key.
/// </para>
/// <para>
/// Every method reports absence instead of throwing, because every one of these values is optional:
/// a <c>noul</c> answer carries no confidence at all, by design.
/// </para>
/// </remarks>
public static class JevAnswers
{
    /// <summary>
    /// Reads how sure the model was about an answer.
    /// </summary>
    /// <param name="answer">The answer to read.</param>
    /// <param name="confidence">How sure the model was, from 0 to 1.</param>
    /// <returns>
    /// <see langword="true"/> when the answer carries a confidence. A <c>noul</c> answer never
    /// does.
    /// </returns>
    /// <remarks>
    /// How the value is computed is not published. Treat it as the service's own opinion, compare
    /// it only against itself, and never attempt to reproduce it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static bool TryGetConfidence(Answer answer, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Metadata.TryGetValue(JevProtocol.ConfidenceMetadataKey, out object? value) &&
            value is double reported)
        {
            confidence = reported;
            return true;
        }

        confidence = 0;
        return false;
    }

    /// <summary>
    /// Reads the rubric a score answer was measured against, keyed by level index as text.
    /// </summary>
    /// <param name="answer">The answer to read.</param>
    /// <param name="legend">The rubric the service echoed back.</param>
    /// <returns><see langword="true"/> when the answer carries a rubric.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static bool TryGetLegend(Answer answer, out IReadOnlyDictionary<string, object?> legend) =>
        TryGetMap(answer, JevProtocol.LegendMetadataKey, out legend);

    /// <summary>
    /// Reads the distribution behind a score answer, keyed by level index as text.
    /// </summary>
    /// <param name="answer">The answer to read.</param>
    /// <param name="probabilities">The probability assigned to each level.</param>
    /// <returns><see langword="true"/> when the answer carries a distribution.</returns>
    /// <remarks>
    /// A choice answer's distribution is not read here. It reaches the domain typed, as
    /// <see cref="DecisionKit.Values.Choice{TOption}.Distribution"/>, which is where it should be
    /// read from.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static bool TryGetLevelProbabilities(Answer answer, out IReadOnlyDictionary<string, object?> probabilities) =>
        TryGetMap(answer, JevProtocol.ProbabilitiesMetadataKey, out probabilities);

    /// <summary>
    /// Reads the exact JSON an answer was read from.
    /// </summary>
    /// <param name="answer">The answer to read.</param>
    /// <param name="payload">The JSON payload.</param>
    /// <returns>
    /// <see langword="true"/> when the payload was retained, which requires
    /// <see cref="JevMappingOptions.PreserveRawPayloads"/> to have been on.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static bool TryGetRawPayload(Answer answer, out string payload)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer is UnknownAnswer { RawPayload: { } retained })
        {
            payload = retained;
            return true;
        }

        if (answer.Metadata.TryGetValue(JevProtocol.RawPayloadMetadataKey, out object? value) && value is string json)
        {
            payload = json;
            return true;
        }

        payload = string.Empty;
        return false;
    }

    private static bool TryGetMap(Answer answer, string key, out IReadOnlyDictionary<string, object?> map)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Metadata.TryGetValue(key, out object? value) &&
            value is IReadOnlyDictionary<string, object?> reported)
        {
            map = reported;
            return true;
        }

        map = EmptyMap;
        return false;
    }

    private static IReadOnlyDictionary<string, object?> EmptyMap { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
