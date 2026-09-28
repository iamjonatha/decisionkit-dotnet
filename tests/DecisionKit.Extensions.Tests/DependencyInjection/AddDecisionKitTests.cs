using System;
using System.Linq;
using DecisionKit.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DecisionKit.Extensions.Tests.DependencyInjection;

public sealed class AddDecisionKitTests
{
    [Fact]
    public void AddDecisionKit_RejectsAMissingServiceCollection()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(services.AddDecisionKit);
    }

    [Fact]
    public void AddDecisionKit_ReturnsABuilderOverTheSameCollection()
    {
        ServiceCollection services = [];

        IDecisionKitBuilder builder = services.AddDecisionKit();

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddDecisionKit_RegistersNoProviderOfItsOwn()
    {
        ServiceCollection services = [];

        services.AddDecisionKit();

        // The core knows about no service, so there is nothing for this call to register but the
        // options plumbing every registration needs.
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Namespace?.StartsWith("DecisionKit", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddDecisionKit_CanBeCalledMoreThanOnce()
    {
        ServiceCollection services = [];

        services.AddDecisionKit();
        services.AddDecisionKit();

        using ServiceProvider container = services.BuildServiceProvider();

        Assert.NotNull(container.GetRequiredService<IOptions<TestOptions>>().Value);
    }

    private sealed class TestOptions
    {
        public string? Value { get; set; }
    }
}
