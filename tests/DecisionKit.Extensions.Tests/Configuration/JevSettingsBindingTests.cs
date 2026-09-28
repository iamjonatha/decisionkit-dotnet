using System;
using System.Threading;
using DecisionKit.Extensions.Configuration;
using DecisionKit.Extensions.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DecisionKit.Extensions.Tests.Configuration;

public sealed class JevSettingsBindingTests
{
    [Fact]
    public void Binding_ReadsEverySetting()
    {
        JevProviderOptions options = Bind(
            ("ApiKey", Settings.ApiKey),
            ("Endpoint", "https://jev.internal"),
            ("ProviderName", "jev-eu"),
            ("Model", "jev-2"),
            ("PreserveRawPayloads", "true"),
            ("AttemptTimeout", "00:00:05"),
            ("OperationTimeout", "00:00:09"),
            ("RetryDelayTimeout", "00:00:03"),
            ("Retry:MaxAttempts", "4"),
            ("Retry:MaxElapsedTime", "00:00:20"),
            ("Retry:InitialDelay", "00:00:00.250"),
            ("Retry:BackoffFactor", "1.5"),
            ("Retry:MaxDelay", "00:00:08"),
            ("Retry:Jitter", "0.4"),
            ("Retry:RespectRetryAfter", "false"),
            ("Retry:RetryUnknownFailures", "true"),
            ("Retry:Idempotency", "disabled"));

        Assert.Equal(new Uri("https://jev.internal/"), options.Endpoint.BaseAddress);
        Assert.Equal("jev-eu", options.Mapping.ProviderName);
        Assert.Equal("jev-2", options.Mapping.Model);
        Assert.True(options.Mapping.PreserveRawPayloads);
        Assert.Equal(TimeSpan.FromSeconds(5), options.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(9), options.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), options.RetryDelayTimeout);
        Assert.Equal(4, options.Retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(20), options.Retry.MaxElapsedTime);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.Retry.InitialDelay);
        Assert.Equal(1.5d, options.Retry.BackoffFactor);
        Assert.Equal(TimeSpan.FromSeconds(8), options.Retry.MaxDelay);
        Assert.Equal(0.4d, options.Retry.Jitter);
        Assert.False(options.Retry.RespectRetryAfter);
        Assert.True(options.Retry.RetryUnknownFailures);
        Assert.Equal(DecisionIdempotencyPolicy.Disabled, options.Retry.Idempotency);
    }

    [Fact]
    public void Binding_LeavesAnUnmentionedSettingAtItsDefault()
    {
        JevProviderOptions options = Bind(("ApiKey", Settings.ApiKey));

        Assert.Equal(JevProviderOptions.Default.Endpoint, options.Endpoint);
        Assert.Equal(JevProtocol.DefaultModel, options.Mapping.Model);
        Assert.False(options.Mapping.PreserveRawPayloads);
        Assert.Equal(JevProviderOptions.Default.AttemptTimeout, options.AttemptTimeout);
        Assert.False(options.Retry.IsEnabled);
    }

    [Fact]
    public void Binding_NamesTheProviderAfterItsRegistration()
    {
        JevProviderOptions options = Bind(("ApiKey", Settings.ApiKey));

        Assert.Equal("jev", options.Mapping.ProviderName);
    }

    [Fact]
    public void Binding_ReadsTheStatusCodeLists()
    {
        JevProviderOptions options = Bind(
            ("ApiKey", Settings.ApiKey),
            ("Retry:MaxAttempts", "2"),
            ("Retry:RetryableStatusCodes:0", "418"),
            ("Retry:RetryableStatusCodes:1", "425"),
            ("Retry:NonRetryableStatusCodes:0", "409"));

        Assert.Equal(2, options.Retry.RetryableStatusCodes.Count);
        Assert.Contains(418, options.Retry.RetryableStatusCodes);
        Assert.Contains(425, options.Retry.RetryableStatusCodes);
        Assert.Contains(409, options.Retry.NonRetryableStatusCodes);
    }

    [Fact]
    public void Binding_TurnsRetryingOnFromTheRetrySectionAlone()
    {
        JevProviderOptions options = Bind(("ApiKey", Settings.ApiKey), ("Retry:MaxAttempts", "3"));

        Assert.True(options.Retry.IsEnabled);
    }

    [Fact]
    public void Binding_LeavesRetryingOffUntilMaxAttemptsSaysOtherwise()
    {
        // The remaining retry keys describe how to retry, not whether to, so a section full of
        // delays does nothing on its own.
        JevProviderOptions options = Bind(
            ("ApiKey", Settings.ApiKey),
            ("Retry:InitialDelay", "00:00:01"),
            ("Retry:MaxDelay", "00:00:05"));

        Assert.False(options.Retry.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(1), options.Retry.InitialDelay);
    }

    [Fact]
    public void Binding_AcceptsTheWordInfiniteAsADuration()
    {
        JevProviderOptions options = Bind(("ApiKey", Settings.ApiKey), ("OperationTimeout", "infinite"));

        Assert.Equal(Timeout.InfiniteTimeSpan, options.OperationTimeout);
    }

    [Fact]
    public void Binding_IsOverriddenByCodeThatRunsAfterIt()
    {
        IConfigurationSection configuration = Settings.Nested(("ApiKey", Settings.ApiKey), ("Model", "jev-from-configuration"));

        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(configuration)
            .Configure(settings => settings.Model = "jev-from-code");

        using ServiceProvider container = services.BuildServiceProvider();

        Assert.Equal("jev-from-code", Options(container).Mapping.Model);
    }

    [Theory]
    [InlineData("AttemptTimeout", "soon", "a duration")]
    [InlineData("PreserveRawPayloads", "yes", "true or false")]
    [InlineData("Retry:MaxAttempts", "many", "a whole number")]
    [InlineData("Retry:BackoffFactor", "double", "a number")]
    [InlineData("Retry:Idempotency", "best-effort", "Disabled, ExplicitOnly or Automatic")]
    [InlineData("Retry:RetryableStatusCodes:0", "teapot", "an HTTP status code")]
    public void Binding_RefusesAValueItCannotRead(string key, string value, string expectation)
    {
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => Bind(("ApiKey", Settings.ApiKey), (key, value)));

        Assert.Contains($"Jev:{key}", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectation, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Settings.ApiKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_RedactTheApiKey()
    {
        JevProviderSettings settings = new()
        {
            ApiKey = Settings.ApiKey,
            Endpoint = "https://jev.internal",
        };

        string description = settings.ToString();

        Assert.DoesNotContain(Settings.ApiKey, description, StringComparison.Ordinal);
        Assert.Contains("<redacted>", description, StringComparison.Ordinal);
        Assert.Contains("https://jev.internal", description, StringComparison.Ordinal);
    }

    private static JevProviderOptions Bind(params (string Key, string Value)[] values)
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(Settings.Nested(values));

        using ServiceProvider container = services.BuildServiceProvider();

        return Options(container);
    }

    private static JevProviderOptions Options(IServiceProvider container) =>
        Assert.IsType<JevDecisionProvider>(container.GetRequiredService<IDecisionProvider>()).Options;
}
