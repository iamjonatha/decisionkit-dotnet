using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace DecisionKit.Extensions.Configuration;

/// <summary>
/// Builds the failures a misconfigured registration is reported with.
/// </summary>
/// <remarks>
/// Every message names the registration it belongs to, because an application with two providers
/// gets two of everything and "the endpoint is invalid" would not say which one. No message ever
/// contains a credential: the values that are quoted back are addresses, model names, durations and
/// numbers, and the API key is never one of them.
/// </remarks>
internal static class JevConfigurationErrors
{
    public static string Describe(string name, string message) =>
        string.Create(CultureInfo.InvariantCulture, $"DecisionKit JEV provider '{name}': {message}");

    public static OptionsValidationException Invalid(string name, string message) =>
        new(name, typeof(JevProviderSettings), [Describe(name, message)]);

    public static OptionsValidationException Setting(string name, string setting, Exception cause) =>
        Invalid(name, string.Create(CultureInfo.InvariantCulture, $"the '{setting}' setting is not usable. {cause.Message}"));

    public static OptionsValidationException Value(string name, Exception cause) =>
        Invalid(name, cause.Message);

    public static OptionsValidationException Unparsable(string name, string path, string? value, string expected) =>
        Invalid(name, string.Create(CultureInfo.InvariantCulture, $"the configuration value '{path}' is '{value}', which is not {expected}."));

    public static ValidateOptionsResult Fail(IReadOnlyCollection<string> failures) =>
        failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
}
