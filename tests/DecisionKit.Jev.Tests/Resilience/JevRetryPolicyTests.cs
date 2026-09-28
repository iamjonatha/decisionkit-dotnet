using System;
using System.Collections.Generic;
using System.Threading;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;
using Xunit;

namespace DecisionKit.Jev.Tests.Resilience;

public sealed class JevRetryPolicyTests
{
    [Fact]
    public void None_NeverRepeatsACall()
    {
        Assert.Equal(1, JevRetryPolicy.None.MaxAttempts);
        Assert.False(JevRetryPolicy.None.IsEnabled);
    }

    [Fact]
    public void Standard_RepeatsARetryableFailureTwice()
    {
        Assert.Equal(3, JevRetryPolicy.Standard.MaxAttempts);
        Assert.True(JevRetryPolicy.Standard.IsEnabled);
        Assert.Equal(TimeSpan.FromMilliseconds(500), JevRetryPolicy.Standard.InitialDelay);
        Assert.Equal(2d, JevRetryPolicy.Standard.BackoffFactor);
    }

    [Fact]
    public void MaxAttempts_RejectsAnOperationThatCannotEvenTryOnce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { MaxAttempts = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { MaxAttempts = -1 });
    }

    [Fact]
    public void MaxElapsedTime_AcceptsOnlyAPositiveBudgetOrNone()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { MaxElapsedTime = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { MaxElapsedTime = TimeSpan.FromSeconds(-1) });

        JevRetryPolicy unbounded = new() { MaxElapsedTime = Timeout.InfiniteTimeSpan };

        Assert.Equal(Timeout.InfiniteTimeSpan, unbounded.MaxElapsedTime);
    }

    [Fact]
    public void Delays_MustBeFiniteAndNotNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { InitialDelay = Timeout.InfiniteTimeSpan });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { MaxDelay = TimeSpan.FromSeconds(-1) });

        // Zero is a deliberate configuration: repeat at once, without waiting.
        JevRetryPolicy immediate = new() { InitialDelay = TimeSpan.Zero };

        Assert.Equal(TimeSpan.Zero, immediate.InitialDelay);
    }

    [Fact]
    public void BackoffFactor_CannotShrinkTheWaitOrBeUndefined()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { BackoffFactor = 0.5d });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { BackoffFactor = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { BackoffFactor = double.PositiveInfinity });

        JevRetryPolicy constant = new() { BackoffFactor = 1d };

        Assert.Equal(1d, constant.BackoffFactor);
    }

    [Fact]
    public void Jitter_IsAFractionOfTheWait()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { Jitter = -0.1d });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { Jitter = 1.1d });
        Assert.Throws<ArgumentOutOfRangeException>(() => new JevRetryPolicy { Jitter = double.NaN });
    }

    [Fact]
    public void StatusCodeSets_AreRequiredAndCopied()
    {
        Assert.Throws<ArgumentNullException>(() => new JevRetryPolicy { RetryableStatusCodes = null! });
        Assert.Throws<ArgumentNullException>(() => new JevRetryPolicy { NonRetryableStatusCodes = null! });

        HashSet<int> mutable = [503];
        JevRetryPolicy policy = new() { RetryableStatusCodes = mutable };

        mutable.Add(418);

        Assert.Contains(503, policy.RetryableStatusCodes);
        Assert.DoesNotContain(418, policy.RetryableStatusCodes);
    }

    [Fact]
    public void Idempotency_AcceptsOnlyWhatJevCanHonour()
    {
        Assert.Equal(DecisionIdempotencyPolicy.Disabled, JevRetryPolicy.Standard.Idempotency);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevRetryPolicy { Idempotency = DecisionIdempotencyPolicy.ExplicitOnly });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevRetryPolicy { Idempotency = DecisionIdempotencyPolicy.Automatic });
    }

    [Fact]
    public void Classify_RepeatsWhateverTheProviderSignalled()
    {
        JevRetryPolicy policy = JevRetryPolicy.Standard;

        Assert.Equal(DecisionRetryability.Retryable, policy.Classify(Signalled(DecisionRetryHint.Retryable)));
        Assert.Equal(DecisionRetryability.NotRetryable, policy.Classify(Signalled(DecisionRetryHint.NotRetryable)));
        Assert.Equal(DecisionRetryability.Unknown, policy.Classify(Signalled(DecisionRetryHint.Unknown)));
    }

    [Fact]
    public void Classify_LetsTheDeploymentOverrideTheSignal()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 3,
            RetryableStatusCodes = new HashSet<int> { 422 },
            NonRetryableStatusCodes = new HashSet<int> { 503 },
        };

        Assert.Equal(DecisionRetryability.Retryable, policy.Classify(Reported(422, DecisionRetryHint.NotRetryable)));
        Assert.Equal(DecisionRetryability.NotRetryable, policy.Classify(Reported(503, DecisionRetryHint.Retryable)));
    }

    [Fact]
    public void Classify_RefusesACodeListedInBothSets()
    {
        JevRetryPolicy policy = new()
        {
            RetryableStatusCodes = new HashSet<int> { 429 },
            NonRetryableStatusCodes = new HashSet<int> { 429 },
        };

        Assert.Equal(DecisionRetryability.NotRetryable, policy.Classify(Reported(429, DecisionRetryHint.Retryable)));
    }

    [Fact]
    public void Classify_IgnoresTheSetsWhenNoStatusCodeArrived()
    {
        JevRetryPolicy policy = new() { NonRetryableStatusCodes = new HashSet<int> { 503 } };

        Assert.Equal(DecisionRetryability.Retryable, policy.Classify(Signalled(DecisionRetryHint.Retryable)));
    }

    [Fact]
    public void Classify_RequiresAnError()
    {
        Assert.Throws<ArgumentNullException>(() => JevRetryPolicy.Standard.Classify(null!));
    }

    [Fact]
    public void GetDelay_GrowsTheWaitByTheBackoffFactor()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            BackoffFactor = 2d,
            Jitter = 0d,
        };

        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.GetDelay(1, null));
        Assert.Equal(TimeSpan.FromMilliseconds(1000), policy.GetDelay(2, null));
        Assert.Equal(TimeSpan.FromMilliseconds(2000), policy.GetDelay(3, null));
    }

    [Fact]
    public void GetDelay_StopsGrowingAtTheCeiling()
    {
        JevRetryPolicy policy = new()
        {
            MaxAttempts = 20,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(4),
            Jitter = 0d,
        };

        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(3, null));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(19, null));
    }

    [Fact]
    public void GetDelay_KeepsAConstantWaitWhenTheFactorIsOne()
    {
        JevRetryPolicy policy = new() { InitialDelay = TimeSpan.FromSeconds(2), BackoffFactor = 1d, Jitter = 0d };

        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(1, null));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(7, null));
    }

    [Fact]
    public void GetDelay_ObeysTheServiceExactly()
    {
        JevRetryPolicy policy = new() { InitialDelay = TimeSpan.FromSeconds(1), Jitter = 0.5d };

        // Neither backoff, nor ceiling, nor jitter applies: the service named a delay.
        Assert.Equal(TimeSpan.FromSeconds(42), policy.GetDelay(4, TimeSpan.FromSeconds(42)));
    }

    [Fact]
    public void GetDelay_IgnoresTheServiceWhenToldTo()
    {
        JevRetryPolicy policy = new()
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            Jitter = 0d,
            RespectRetryAfter = false,
        };

        Assert.Equal(TimeSpan.FromSeconds(1), policy.GetDelay(1, TimeSpan.FromSeconds(42)));
    }

    [Fact]
    public void GetDelay_NeverReturnsANegativeWait()
    {
        JevRetryPolicy policy = new() { Jitter = 0d };

        Assert.Equal(TimeSpan.Zero, policy.GetDelay(1, TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void GetDelay_OnlyEverShortensAWaitWithJitter()
    {
        JevRetryPolicy policy = new()
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(1),
            Jitter = 0.25d,
        };

        bool varied = false;

        for (int sample = 0; sample < 200; sample++)
        {
            TimeSpan delay = policy.GetDelay(1, null);

            Assert.InRange(delay, TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(1));

            varied |= delay != TimeSpan.FromSeconds(1);
        }

        Assert.True(varied, "Jitter never moved the delay, so it is not being applied.");
    }

    [Fact]
    public void GetDelay_RequiresAtLeastOneCompletedAttempt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JevRetryPolicy.Standard.GetDelay(0, null));
    }

    private static DecisionError Signalled(DecisionRetryHint retry) =>
        new(DecisionErrorCategory.ProviderError, "The service declined.") { Retry = retry };

    private static DecisionError Reported(int statusCode, DecisionRetryHint retry) =>
        new(DecisionErrorCategory.ProviderError, "The service declined.")
        {
            Retry = retry,
            Properties = new Dictionary<string, object?> { [JevProtocol.StatusCodeProperty] = statusCode },
        };
}
