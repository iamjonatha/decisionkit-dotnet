using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Jev.Authentication;
using Xunit;

namespace DecisionKit.Jev.Tests.Authentication;

public sealed class CredentialProviderTests
{
    private const string Secret = "ts_live_do_not_log_this_value";

    [Fact]
    public async Task Static_ReturnsTheConfiguredKey()
    {
        StaticJevCredentialProvider credentials = new(Secret);

        Assert.Equal(new JevApiKey(Secret), await credentials.GetApiKeyAsync(CancellationToken.None));
    }

    [Fact]
    public void Static_RefusesAnAbsentKey() =>
        Assert.Throws<ArgumentException>(() => new StaticJevCredentialProvider(JevApiKey.None));

    [Fact]
    public async Task Static_ObservesCancellation()
    {
        StaticJevCredentialProvider credentials = new(Secret);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await credentials.GetApiKeyAsync(cancellation.Token));
    }

    [Fact]
    public void Delegate_RejectsANullSource()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DelegateJevCredentialProvider((Func<CancellationToken, ValueTask<JevApiKey>>)null!));
        Assert.Throws<ArgumentNullException>(() => new DelegateJevCredentialProvider((Func<JevApiKey>)null!));
    }

    [Fact]
    public async Task Delegate_IsAskedOnEveryCallSoThatRotationTakesEffect()
    {
        int calls = 0;
        DelegateJevCredentialProvider credentials = new(() => new JevApiKey($"{Secret}_{++calls}"));

        Assert.Equal(new JevApiKey($"{Secret}_1"), await credentials.GetApiKeyAsync(CancellationToken.None));
        Assert.Equal(new JevApiKey($"{Secret}_2"), await credentials.GetApiKeyAsync(CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Delegate_PassesTheTokenToAnAsynchronousSource()
    {
        CancellationToken observed = default;
        DelegateJevCredentialProvider credentials = new(token =>
        {
            observed = token;

            return new ValueTask<JevApiKey>(new JevApiKey(Secret));
        });

        using CancellationTokenSource cancellation = new();

        await credentials.GetApiKeyAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, observed);
    }
}
