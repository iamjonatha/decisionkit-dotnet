using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using DecisionKit.Identifiers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DecisionKit.Extensions.Configuration;

/// <summary>
/// Reads a configuration section into <see cref="JevProviderSettings"/>, one known key at a time.
/// </summary>
/// <remarks>
/// <para>
/// The reflection-based binder would do this in one line, and is the obvious thing to reach for.
/// It is not used here for two reasons. The shipping packages are trimmable and ahead-of-time
/// compatible, and <c>ConfigurationBinder.Bind</c> is neither; annotating the whole registration
/// API as unsafe to satisfy it would push the problem onto every application that trims. And a
/// reflective binder reports a bad value as a type-conversion failure, which names a property but
/// not the configuration key, the registration or what a good value looks like.
/// </para>
/// <para>
/// Reading the keys by hand costs a few dozen lines and buys both. It also makes the configuration
/// surface explicit: a key that is not read here is not a setting, and a typo in a key name leaves
/// the default in place rather than silently doing nothing, which is why the documented keys are
/// worth keeping next to the properties they fill.
/// </para>
/// <para>
/// A duration accepts either the <c>hh:mm:ss</c> form the configuration system uses, or the word
/// <c>infinite</c>, because the timespan that means "no limit" is written
/// <c>-00:00:00.0010000</c> and nobody should have to know that.
/// </para>
/// </remarks>
internal static class JevSettingsBinder
{
    private const string Infinite = "infinite";

    public static void Bind(string name, IConfiguration configuration, JevProviderSettings settings)
    {
        settings.ApiKey = Text(configuration, "ApiKey") ?? settings.ApiKey;
        settings.Endpoint = Text(configuration, "Endpoint") ?? settings.Endpoint;
        settings.ProviderName = Text(configuration, "ProviderName") ?? settings.ProviderName;
        settings.Model = Text(configuration, "Model") ?? settings.Model;
        settings.PreserveRawPayloads = Flag(name, configuration, "PreserveRawPayloads") ?? settings.PreserveRawPayloads;
        settings.AttemptTimeout = Duration(name, configuration, "AttemptTimeout") ?? settings.AttemptTimeout;
        settings.OperationTimeout = Duration(name, configuration, "OperationTimeout") ?? settings.OperationTimeout;
        settings.RetryDelayTimeout = Duration(name, configuration, "RetryDelayTimeout") ?? settings.RetryDelayTimeout;

        BindRetry(name, configuration.GetSection("Retry"), settings.Retry);
    }

    private static void BindRetry(string name, IConfiguration configuration, JevRetrySettings settings)
    {
        settings.MaxAttempts = Count(name, configuration, "MaxAttempts") ?? settings.MaxAttempts;
        settings.MaxElapsedTime = Duration(name, configuration, "MaxElapsedTime") ?? settings.MaxElapsedTime;
        settings.InitialDelay = Duration(name, configuration, "InitialDelay") ?? settings.InitialDelay;
        settings.BackoffFactor = Number(name, configuration, "BackoffFactor") ?? settings.BackoffFactor;
        settings.MaxDelay = Duration(name, configuration, "MaxDelay") ?? settings.MaxDelay;
        settings.Jitter = Number(name, configuration, "Jitter") ?? settings.Jitter;
        settings.RespectRetryAfter = Flag(name, configuration, "RespectRetryAfter") ?? settings.RespectRetryAfter;
        settings.RetryUnknownFailures = Flag(name, configuration, "RetryUnknownFailures") ?? settings.RetryUnknownFailures;
        settings.Idempotency = Idempotency(name, configuration, "Idempotency") ?? settings.Idempotency;

        BindStatusCodes(name, configuration.GetSection("RetryableStatusCodes"), settings.RetryableStatusCodes);
        BindStatusCodes(name, configuration.GetSection("NonRetryableStatusCodes"), settings.NonRetryableStatusCodes);
    }

    private static void BindStatusCodes(string name, IConfigurationSection section, IList<int> target)
    {
        if (!section.Exists())
        {
            return;
        }

        // A configured list replaces what was there rather than adding to it. Two configuration
        // sources that both mention the section would otherwise merge into a list neither wrote.
        target.Clear();

        foreach (IConfigurationSection child in section.GetChildren())
        {
            target.Add(StatusCode(name, child));
        }
    }

    private static string? Text(IConfiguration configuration, string key)
    {
        string? value = configuration[key];

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool? Flag(string name, IConfiguration configuration, string key)
    {
        if (Read(configuration, key) is not { } section)
        {
            return null;
        }

        return bool.TryParse(section.Value, out bool parsed)
            ? parsed
            : throw Unparsable(name, section, "true or false");
    }

    private static int? Count(string name, IConfiguration configuration, string key)
    {
        if (Read(configuration, key) is not { } section)
        {
            return null;
        }

        return int.TryParse(section.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw Unparsable(name, section, "a whole number");
    }

    private static double? Number(string name, IConfiguration configuration, string key)
    {
        if (Read(configuration, key) is not { } section)
        {
            return null;
        }

        return double.TryParse(section.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : throw Unparsable(name, section, "a number");
    }

    private static TimeSpan? Duration(string name, IConfiguration configuration, string key)
    {
        if (Read(configuration, key) is not { } section)
        {
            return null;
        }

        if (string.Equals(section.Value, Infinite, StringComparison.OrdinalIgnoreCase))
        {
            return Timeout.InfiniteTimeSpan;
        }

        return TimeSpan.TryParse(section.Value, CultureInfo.InvariantCulture, out TimeSpan parsed)
            ? parsed
            : throw Unparsable(name, section, "a duration such as '00:00:30', or 'infinite'");
    }

    private static DecisionIdempotencyPolicy? Idempotency(string name, IConfiguration configuration, string key)
    {
        if (Read(configuration, key) is not { } section)
        {
            return null;
        }

        return Enum.TryParse(section.Value, ignoreCase: true, out DecisionIdempotencyPolicy parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : throw Unparsable(name, section, "Disabled, ExplicitOnly or Automatic");
    }

    private static int StatusCode(string name, IConfigurationSection section) =>
        int.TryParse(section.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw Unparsable(name, section, "an HTTP status code");

    private static IConfigurationSection? Read(IConfiguration configuration, string key)
    {
        IConfigurationSection section = configuration.GetSection(key);

        return string.IsNullOrWhiteSpace(section.Value) ? null : section;
    }

    private static OptionsValidationException Unparsable(string name, IConfigurationSection section, string expected) =>
        JevConfigurationErrors.Unparsable(name, section.Path, section.Value, expected);
}
