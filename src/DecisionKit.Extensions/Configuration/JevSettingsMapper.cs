using System;
using System.Collections.Generic;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;
using Microsoft.Extensions.Options;

namespace DecisionKit.Extensions.Configuration;

/// <summary>
/// Turns bound settings into the validated options the provider is built from.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam between a shape a binder can fill and a shape the library can trust. The
/// immutable options types do the validating, as they do for a caller who constructs them by hand;
/// this type only translates what they refuse into a message that names the registration and the
/// setting responsible.
/// </para>
/// <para>
/// A setting left unset takes the library default, read from the default options rather than
/// repeated here, so there is exactly one place a default lives.
/// </para>
/// </remarks>
internal static class JevSettingsMapper
{
    /// <summary>
    /// Builds the provider options described by these settings.
    /// </summary>
    /// <exception cref="OptionsValidationException">The settings describe an unusable provider.</exception>
    public static JevProviderOptions ToOptions(string name, JevProviderSettings settings)
    {
        JevProviderOptions defaults = JevProviderOptions.Default;

        try
        {
            return new JevProviderOptions
            {
                Endpoint = Endpoint(name, settings.Endpoint),
                Mapping = Mapping(name, settings),
                Retry = Retry(name, settings.Retry),
                AttemptTimeout = settings.AttemptTimeout ?? defaults.AttemptTimeout,
                OperationTimeout = settings.OperationTimeout ?? defaults.OperationTimeout,
                RetryDelayTimeout = settings.RetryDelayTimeout ?? defaults.RetryDelayTimeout,
            };
        }
        catch (ArgumentException exception)
        {
            throw JevConfigurationErrors.Value(name, exception);
        }
    }

    /// <summary>
    /// States whether these settings describe a provider that can be built and called.
    /// </summary>
    /// <param name="name">The registration the settings belong to.</param>
    /// <param name="settings">The settings to check.</param>
    /// <param name="credentialsRegistered">
    /// Whether the application registered a credential provider of its own, which is the only way a
    /// registration without an API key in configuration can authenticate.
    /// </param>
    public static ValidateOptionsResult Validate(string name, JevProviderSettings settings, bool credentialsRegistered)
    {
        List<string> failures = [];

        if (!credentialsRegistered && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            failures.Add(JevConfigurationErrors.Describe(
                name,
                "no API key is configured. Set the 'ApiKey' configuration value, or call WithCredentials(...) to supply one from a secret store."));
        }

        try
        {
            ToOptions(name, settings);
        }
        catch (OptionsValidationException exception)
        {
            failures.AddRange(exception.Failures);
        }

        return JevConfigurationErrors.Fail(failures);
    }

    private static JevEndpoint Endpoint(string name, string? value)
    {
        if (value is null)
        {
            return JevProviderOptions.Default.Endpoint;
        }

        try
        {
            return new JevEndpoint(value);
        }
        catch (ArgumentException exception)
        {
            throw JevConfigurationErrors.Setting(name, "Endpoint", exception);
        }
    }

    private static JevMappingOptions Mapping(string name, JevProviderSettings settings)
    {
        JevMappingOptions defaults = JevMappingOptions.Default;

        try
        {
            return new JevMappingOptions
            {
                // A registration that does not name itself reports the name it was registered
                // under, so two JEV providers in one application are told apart in a log without
                // any configuration at all.
                ProviderName = settings.ProviderName ?? name,
                Model = settings.Model ?? defaults.Model,
                PreserveRawPayloads = settings.PreserveRawPayloads ?? defaults.PreserveRawPayloads,
            };
        }
        catch (ArgumentException exception)
        {
            throw JevConfigurationErrors.Value(name, exception);
        }
    }

    private static JevRetryPolicy Retry(string name, JevRetrySettings settings)
    {
        if (!settings.IsConfigured)
        {
            return JevProviderOptions.Default.Retry;
        }

        JevRetryPolicy defaults = JevRetryPolicy.None;

        try
        {
            return new JevRetryPolicy
            {
                MaxAttempts = settings.MaxAttempts ?? defaults.MaxAttempts,
                MaxElapsedTime = settings.MaxElapsedTime ?? defaults.MaxElapsedTime,
                InitialDelay = settings.InitialDelay ?? defaults.InitialDelay,
                BackoffFactor = settings.BackoffFactor ?? defaults.BackoffFactor,
                MaxDelay = settings.MaxDelay ?? defaults.MaxDelay,
                Jitter = settings.Jitter ?? defaults.Jitter,
                RespectRetryAfter = settings.RespectRetryAfter ?? defaults.RespectRetryAfter,
                RetryUnknownFailures = settings.RetryUnknownFailures ?? defaults.RetryUnknownFailures,
                RetryableStatusCodes = new HashSet<int>(settings.RetryableStatusCodes),
                NonRetryableStatusCodes = new HashSet<int>(settings.NonRetryableStatusCodes),
                Idempotency = settings.Idempotency ?? defaults.Idempotency,
            };
        }
        catch (ArgumentException exception)
        {
            throw JevConfigurationErrors.Setting(name, "Retry", exception);
        }
    }
}
