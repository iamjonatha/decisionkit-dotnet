using System;
using DecisionKit.Extensions.DependencyInjection;
using DecisionKit.Extensions.Tests.Fixtures;
using DecisionKit.Jev.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DecisionKit.Extensions.Tests.Configuration;

public sealed class JevStartupValidationTests
{
    [Fact]
    public void Startup_AcceptsAUsableRegistration()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings => settings.ApiKey = Settings.ApiKey);

        Validate(services);
    }

    [Fact]
    public void Startup_RefusesARegistrationWithoutACredential()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider();

        string failure = Failure(services);

        Assert.Contains("no API key is configured", failure, StringComparison.Ordinal);
        Assert.Contains("WithCredentials", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_AcceptsACredentialSuppliedInCode()
    {
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider()
            .WithCredentials(new StaticJevCredentialProvider(Settings.ApiKey));

        Validate(services);
    }

    [Fact]
    public void Startup_RefusesAnUnusableEndpoint()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.Endpoint = "not-an-address";
        });

        string failure = Failure(services);

        Assert.Contains("Endpoint", failure, StringComparison.Ordinal);
        Assert.Contains("not-an-address", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_RefusesAnImpossibleRetryPolicy()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.Retry.MaxAttempts = 3;
            settings.Retry.BackoffFactor = 0.5d;
        });

        string failure = Failure(services);

        Assert.Contains("Retry", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_RefusesATimeoutThatCannotElapse()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.AttemptTimeout = TimeSpan.Zero;
        });

        string failure = Failure(services);

        Assert.Contains("AttemptTimeout", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_NamesTheRegistrationThatIsWrong()
    {
        ServiceCollection services = [];
        IDecisionKitBuilder decisionKit = services.AddDecisionKit();
        decisionKit.AddJevProvider("healthy", settings => settings.ApiKey = Settings.ApiKey);
        decisionKit.AddJevProvider("broken");

        string failure = Failure(services);

        Assert.Contains("'broken'", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("'healthy'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_NeverRepeatsTheApiKey()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.Endpoint = "not-an-address";
        });

        Assert.DoesNotContain(Settings.ApiKey, Failure(services), StringComparison.Ordinal);
    }

    private static void Validate(IServiceCollection services)
    {
        using ServiceProvider container = services.BuildServiceProvider();

        container.GetRequiredService<IStartupValidator>().Validate();
    }

    private static string Failure(IServiceCollection services)
    {
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Validate(services));

        return exception.Message;
    }
}
