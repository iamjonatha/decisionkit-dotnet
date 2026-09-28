using System;
using DecisionKit.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// Checks one registration's settings, and ignores every other registration's.
/// </summary>
/// <remarks>
/// Named options share a validator list, so a validator that answered for every name would report
/// a second provider's missing API key against the first one's configuration. Skipping is not an
/// optimization here; it is what keeps the message true.
/// </remarks>
internal sealed class JevSettingsValidator : IValidateOptions<JevProviderSettings>
{
    private readonly JevProviderRegistration _registration;

    public JevSettingsValidator(JevProviderRegistration registration)
    {
        _registration = registration;
    }

    public ValidateOptionsResult Validate(string? name, JevProviderSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.Equals(name, _registration.Name, StringComparison.Ordinal)
            ? JevSettingsMapper.Validate(_registration.Name, options, _registration.HasCredentials)
            : ValidateOptionsResult.Skip;
    }
}
