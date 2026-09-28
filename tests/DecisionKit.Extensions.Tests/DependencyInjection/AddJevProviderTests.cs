using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Extensions.DependencyInjection;
using DecisionKit.Extensions.Tests.Fixtures;
using DecisionKit.Jev.Client;
using DecisionKit.Providers;
using DecisionKit.Results;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DecisionKit.Extensions.Tests.DependencyInjection;

public sealed class AddJevProviderTests
{
    [Fact]
    public void AddJevProvider_RejectsAMissingBuilder()
    {
        IDecisionKitBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(builder.AddJevProvider);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddJevProvider_RejectsANameThatNamesNothing(string name)
    {
        IDecisionKitBuilder builder = new ServiceCollection().AddDecisionKit();

        Assert.Throws<ArgumentException>(() => builder.AddJevProvider(name));
    }

    [Fact]
    public void AddJevProvider_ResolvesOneInstanceByType_ByKey_AndInASequence()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings => settings.ApiKey = Settings.ApiKey);

        using ServiceProvider container = services.BuildServiceProvider();

        IDecisionProvider byType = container.GetRequiredService<IDecisionProvider>();
        IDecisionProvider byKey = container.GetRequiredKeyedService<IDecisionProvider>("jev");

        Assert.Same(byType, byKey);
        Assert.Same(byType, Assert.Single(container.GetServices<IDecisionProvider>()));
    }

    [Fact]
    public void AddJevProvider_NamesTheProviderAfterItsRegistration()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider("fast", settings => settings.ApiKey = Settings.ApiKey);

        using ServiceProvider container = services.BuildServiceProvider();

        Assert.Equal("fast", container.GetRequiredKeyedService<IDecisionProvider>("fast").Name);
    }

    [Fact]
    public void AddJevProvider_KeepsAConfiguredProviderNameOverTheRegistrationName()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider("fast", settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.ProviderName = "jev";
        });

        using ServiceProvider container = services.BuildServiceProvider();

        Assert.Equal("jev", container.GetRequiredKeyedService<IDecisionProvider>("fast").Name);
    }

    [Fact]
    public void AddJevProvider_RefusesToRegisterOneNameTwice()
    {
        IDecisionKitBuilder builder = new ServiceCollection().AddDecisionKit();
        builder.AddJevProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(builder.AddJevProvider);

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GetRequiredKeyedService", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddJevProvider_AppliesEverySettingToTheProvider()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings =>
        {
            settings.ApiKey = Settings.ApiKey;
            settings.Endpoint = "https://jev.internal";
            settings.Model = "jev-2";
            settings.PreserveRawPayloads = true;
            settings.AttemptTimeout = TimeSpan.FromSeconds(5);
            settings.OperationTimeout = TimeSpan.FromSeconds(9);
            settings.RetryDelayTimeout = TimeSpan.FromSeconds(3);
            settings.Retry.MaxAttempts = 4;
            settings.Retry.RetryUnknownFailures = true;
            settings.Retry.RetryableStatusCodes.Add(418);
        });

        using ServiceProvider container = services.BuildServiceProvider();
        JevProviderOptions options = Provider(container).Options;

        Assert.Equal(new Uri("https://jev.internal/"), options.Endpoint.BaseAddress);
        Assert.Equal("jev-2", options.Mapping.Model);
        Assert.True(options.Mapping.PreserveRawPayloads);
        Assert.Equal(TimeSpan.FromSeconds(5), options.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(9), options.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), options.RetryDelayTimeout);
        Assert.Equal(4, options.Retry.MaxAttempts);
        Assert.True(options.Retry.RetryUnknownFailures);
        Assert.Contains(418, options.Retry.RetryableStatusCodes);
    }

    [Fact]
    public void AddJevProvider_LeavesAnUnconfiguredRegistrationOnTheLibraryDefaults()
    {
        ServiceCollection services = [];
        services.AddDecisionKit().AddJevProvider(settings => settings.ApiKey = Settings.ApiKey);

        using ServiceProvider container = services.BuildServiceProvider();
        JevProviderOptions options = Provider(container).Options;

        Assert.Equal(JevProviderOptions.Default.Endpoint, options.Endpoint);
        Assert.Equal(JevProviderOptions.Default.AttemptTimeout, options.AttemptTimeout);
        Assert.Equal(JevProviderOptions.Default.OperationTimeout, options.OperationTimeout);
        Assert.Equal(JevProviderOptions.Default.RetryDelayTimeout, options.RetryDelayTimeout);

        // Retrying stays off until it is asked for, exactly as it is for a hand-built provider.
        Assert.False(options.Retry.IsEnabled);
    }

    [Fact]
    public void AddJevProvider_RegistersAClientWithNoTimeoutOfItsOwn()
    {
        ServiceCollection services = [];
        IJevProviderBuilder jev = services.AddDecisionKit().AddJevProvider(settings => settings.ApiKey = Settings.ApiKey);

        using ServiceProvider container = services.BuildServiceProvider();
        using HttpClient client = container.GetRequiredService<IHttpClientFactory>().CreateClient(jev.HttpClient.Name);

        // DecisionKit already owns three budgets. A fourth one, enforced somewhere else and
        // reported as something else, would be the one that actually fires.
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public async Task AddJevProvider_SendsThroughTheRegisteredHandlerAndAuthenticates()
    {
        using JevStub stub = new();
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        using ServiceProvider container = services.BuildServiceProvider();

        DecisionResult result = await container.GetRequiredService<IDecisionProvider>()
            .DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("jev", result.Metadata.ProviderName);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", stub.LastCall.Uri.AbsoluteUri);
        Assert.Equal("Bearer", stub.LastCall.AuthorizationScheme);
        Assert.Equal(Settings.ApiKey, stub.LastCall.ApiKey);
    }

    [Fact]
    public async Task AddJevProvider_KeepsTwoRegistrationsApart()
    {
        using JevStub fast = new();
        using JevStub careful = new();

        ServiceCollection services = [];
        IDecisionKitBuilder kit = services.AddDecisionKit();

        kit.AddJevProvider("fast", settings => settings.ApiKey = "ts_fast")
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => fast);
        kit.AddJevProvider("careful", settings => settings.ApiKey = "ts_careful")
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => careful);

        using ServiceProvider container = services.BuildServiceProvider();

        await container.GetRequiredKeyedService<IDecisionProvider>("fast")
            .DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Single(fast.Calls);
        Assert.Empty(careful.Calls);
        Assert.Equal("ts_fast", fast.LastCall.ApiKey);
        Assert.Equal(2, container.GetServices<IDecisionProvider>().Count());
    }

    private static JevDecisionProvider Provider(IServiceProvider container) =>
        Assert.IsType<JevDecisionProvider>(container.GetRequiredService<IDecisionProvider>());
}
