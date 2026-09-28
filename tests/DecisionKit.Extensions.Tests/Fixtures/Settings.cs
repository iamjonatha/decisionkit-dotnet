using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace DecisionKit.Extensions.Tests.Fixtures;

/// <summary>
/// Builds the configuration a registration test binds against, and lets a test change it
/// afterwards so that reload behaviour has something to observe.
/// </summary>
public static class Settings
{
    public const string ApiKey = "ts_live_do_not_log_this_value";

    public static IConfigurationRoot Section(params (string Key, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<string, string?> data = [];

        foreach ((string key, string value) in values)
        {
            data[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    public static IConfigurationRoot WithApiKey() => Section(("ApiKey", ApiKey));

    /// <summary>
    /// Builds the same settings under a section, so that a failure message has a realistic
    /// configuration path to quote back.
    /// </summary>
    public static IConfigurationSection Nested(params (string Key, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<string, string?> data = [];

        foreach ((string key, string value) in values)
        {
            data[$"Jev:{key}"] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build().GetSection("Jev");
    }

    public static void Replace(IConfigurationRoot configuration, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration[key] = value;
        configuration.Reload();
    }
}
