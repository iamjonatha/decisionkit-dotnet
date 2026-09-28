using System;
using System.Threading.Tasks;
using DecisionKit.Extensions.DependencyInjection;
using DecisionKit.Extensions.Tests.Fixtures;
using DecisionKit.Jev.Authentication;
using DecisionKit.Providers;
using DecisionKit.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DecisionKit.Extensions.Tests.DependencyInjection;

public sealed class JevProviderCompositionTests
{
    [Fact]
    public async Task WithApiKey_AuthenticatesTheCall()
    {
        using JevStub stub = new();
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider()
            .WithApiKey(Settings.ApiKey)
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        await DecideAsync(services);

        Assert.Equal(Settings.ApiKey, stub.LastCall.ApiKey);
    }

    [Fact]
    public async Task WithCredentials_ReplacesTheKeyReadFromConfiguration()
    {
        using JevStub stub = new();
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = "ts_from_configuration")
            .WithCredentials(new StaticJevCredentialProvider("ts_from_the_vault"))
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        await DecideAsync(services);

        Assert.Equal("ts_from_the_vault", stub.LastCall.ApiKey);
    }

    [Fact]
    public async Task WithCredentials_ResolvesTheProviderFromTheContainer()
    {
        using JevStub stub = new();
        ServiceCollection services = [];
        services.AddSingleton(new StaticJevCredentialProvider("ts_from_the_container"));
        services.AddDecisionKit()
            .AddJevProvider()
            .WithCredentials(container => container.GetRequiredService<StaticJevCredentialProvider>())
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        await DecideAsync(services);

        Assert.Equal("ts_from_the_container", stub.LastCall.ApiKey);
    }

    [Fact]
    public void WithCredentials_RejectsAMissingSource()
    {
        IJevProviderBuilder jev = new ServiceCollection().AddDecisionKit().AddJevProvider();

        Assert.Throws<ArgumentNullException>(() => jev.WithCredentials((Func<IServiceProvider, IJevCredentialProvider>)null!));
        Assert.Throws<ArgumentNullException>(() => jev.WithCredentials((IJevCredentialProvider)null!));
    }

    [Fact]
    public async Task ApiKey_IsReadAgainAfterAConfigurationReload()
    {
        using JevStub stub = new();
        IConfigurationRoot configuration = Settings.Section(("ApiKey", "ts_first"));

        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(configuration)
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        using ServiceProvider container = services.BuildServiceProvider();
        IDecisionProvider provider = container.GetRequiredService<IDecisionProvider>();

        await provider.DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);

        Settings.Replace(configuration, "ApiKey", "ts_second");

        await provider.DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);

        // The key is asked for on every call, so a rotation takes effect without a restart and
        // without rebuilding the provider.
        Assert.Equal(2, stub.Calls.Count);
        Assert.Equal("ts_second", stub.LastCall.ApiKey);
    }

    [Fact]
    public void Decorate_WrapsTheProviderOutermostLast()
    {
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .Decorate((_, inner) => new TaggingProvider(inner, "inner"))
            .Decorate((_, inner) => new TaggingProvider(inner, "outer"));

        using ServiceProvider container = services.BuildServiceProvider();

        Assert.Equal("outer(inner(jev))", container.GetRequiredService<IDecisionProvider>().Name);
    }

    [Fact]
    public async Task Decorate_SeesEveryCall()
    {
        using JevStub stub = new();
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .Decorate((_, inner) => new TaggingProvider(inner, "counted"))
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        using ServiceProvider container = services.BuildServiceProvider();
        IDecisionProvider provider = container.GetRequiredService<IDecisionProvider>();

        await provider.DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, Assert.IsType<TaggingProvider>(provider).Calls);
        Assert.Single(stub.Calls);
    }

    [Fact]
    public void Decorate_RejectsAMissingDecorator()
    {
        IJevProviderBuilder jev = new ServiceCollection().AddDecisionKit().AddJevProvider();

        Assert.Throws<ArgumentNullException>(() => jev.Decorate(null!));
    }

    [Fact]
    public void Decorate_RefusesADecoratorThatReturnsNothing()
    {
        ServiceCollection services = [];
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .Decorate((_, _) => null!);

        using ServiceProvider container = services.BuildServiceProvider();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(container.GetRequiredService<IDecisionProvider>);

        Assert.Contains("returned null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_LogsToTheApplicationLog()
    {
        using JevStub stub = new();
        RecordingLoggerProvider log = new();

        ServiceCollection services = [];
        services.AddLogging(logging => logging.AddProvider(log));
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        await DecideAsync(services);

        Assert.NotEmpty(log.Entries);
    }

    [Fact]
    public async Task Provider_UsesTheClockFromTheContainer()
    {
        using JevStub stub = new();
        FakeTimeProvider time = new();

        ServiceCollection services = [];
        services.AddSingleton<TimeProvider>(time);
        services.AddDecisionKit()
            .AddJevProvider(settings => settings.ApiKey = Settings.ApiKey)
            .HttpClient.ConfigurePrimaryHttpMessageHandler(() => stub);

        DecisionResult result = await DecideAsync(services);

        Assert.Equal(time.GetUtcNow(), result.Metadata.Timestamp);
    }

    private static async Task<DecisionResult> DecideAsync(IServiceCollection services)
    {
        await using ServiceProvider container = services.BuildServiceProvider();

        return await container.GetRequiredService<IDecisionProvider>()
            .DecideAsync(JevStub.CreateRequest(), TestContext.Current.CancellationToken);
    }
}
