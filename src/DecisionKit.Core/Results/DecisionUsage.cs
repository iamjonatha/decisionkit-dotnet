using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DecisionKit.Results;

/// <summary>
/// Reports what a decision consumed, without assuming that every provider bills the same way.
/// </summary>
/// <remarks>
/// Tokens, credits, request units and money are provider concepts. Modelling them as fixed
/// properties would force every provider to pretend it has all of them. Usage is therefore a set of
/// named metrics, with <see cref="DecisionUsageKeys"/> documenting the names DecisionKit expects
/// providers to use when the concept applies.
/// </remarks>
public sealed class DecisionUsage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionUsage"/> class.
    /// </summary>
    /// <param name="metrics">The metrics the provider reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="metrics"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A metric name is empty or whitespace, a metric name appears twice, or a metric value is not
    /// finite.
    /// </exception>
    public DecisionUsage(IEnumerable<KeyValuePair<string, double>> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Dictionary<string, double> collected = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, double> metric in metrics)
        {
            Validate(metric);

            if (!collected.TryAdd(metric.Key, metric.Value))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Usage reports the metric '{metric.Key}' more than once."),
                    nameof(metrics));
            }
        }

        Metrics = new ReadOnlyDictionary<string, double>(collected);
    }

    /// <summary>
    /// Gets an empty usage report, used when the provider reports nothing.
    /// </summary>
    public static DecisionUsage Empty { get; } = new([]);

    /// <summary>
    /// Gets the reported metrics, keyed by metric name.
    /// </summary>
    public IReadOnlyDictionary<string, double> Metrics { get; }

    /// <summary>
    /// Gets a value indicating whether the provider reported any metric.
    /// </summary>
    public bool IsEmpty => Metrics.Count == 0;

    /// <summary>
    /// Attempts to read one metric.
    /// </summary>
    /// <param name="name">The metric name, for example a constant from <see cref="DecisionUsageKeys"/>.</param>
    /// <param name="value">The metric value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the metric was reported; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool TryGetMetric(string name, out double value)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Metrics.TryGetValue(name, out value);
    }

    private static void Validate(KeyValuePair<string, double> metric)
    {
        if (string.IsNullOrWhiteSpace(metric.Key))
        {
            throw new ArgumentException("A usage metric must have a non-empty name.", nameof(metric));
        }

        if (double.IsNaN(metric.Value) || double.IsInfinity(metric.Value))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The usage metric '{metric.Key}' must be a finite number."),
                nameof(metric));
        }
    }
}
