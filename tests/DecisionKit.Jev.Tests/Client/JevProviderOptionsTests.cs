using System;
using System.Threading;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;
using Xunit;

namespace DecisionKit.Jev.Tests.Client;

public sealed class JevProviderOptionsTests
{
    [Fact]
    public void Default_UsesThePublicServiceAndTheDefaultMapping()
    {
        Assert.Equal(JevEndpoint.Default, JevProviderOptions.Default.Endpoint);
        Assert.Same(JevMappingOptions.Default, JevProviderOptions.Default.Mapping);
    }

    [Fact]
    public void Default_BoundsBothTheAttemptAndTheOperation()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), JevProviderOptions.Default.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), JevProviderOptions.Default.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), JevProviderOptions.Default.RetryDelayTimeout);
    }

    [Fact]
    public void Default_RepeatsNothing() =>
        Assert.Same(JevRetryPolicy.None, JevProviderOptions.Default.Retry);

    [Fact]
    public void Retry_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new JevProviderOptions { Retry = null! });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RetryDelayTimeout_RejectsANonPositiveBudget(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevProviderOptions { RetryDelayTimeout = TimeSpan.FromSeconds(seconds) });

    [Fact]
    public void Endpoint_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new JevProviderOptions { Endpoint = null! });

    [Fact]
    public void Mapping_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new JevProviderOptions { Mapping = null! });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AttemptTimeout_RejectsANonPositiveBudget(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevProviderOptions { AttemptTimeout = TimeSpan.FromSeconds(seconds) });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OperationTimeout_RejectsANonPositiveBudget(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevProviderOptions { OperationTimeout = TimeSpan.FromSeconds(seconds) });

    [Fact]
    public void Timeouts_AcceptAnInfiniteBudget()
    {
        JevProviderOptions options = new()
        {
            AttemptTimeout = Timeout.InfiniteTimeSpan,
            OperationTimeout = Timeout.InfiniteTimeSpan,
            RetryDelayTimeout = Timeout.InfiniteTimeSpan,
        };

        Assert.Equal(Timeout.InfiniteTimeSpan, options.AttemptTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, options.OperationTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, options.RetryDelayTimeout);
    }
}
