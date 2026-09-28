using System;
using System.Collections.Generic;
using DecisionKit.Errors;

namespace DecisionKit.Core.Tests.Errors;

public sealed class DecisionErrorTests
{
    [Fact]
    public void Constructor_TrimsTheMessage()
    {
        DecisionError error = new(DecisionErrorCategory.Validation, "  Question 'q' is unknown.  ");

        Assert.Equal("Question 'q' is unknown.", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAnUnusableMessage(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DecisionError(DecisionErrorCategory.ProviderError, message!));
    }

    [Fact]
    public void PreservesEveryDiagnosticTheProviderSupplied()
    {
        DecisionError error = new(DecisionErrorCategory.RateLimit, "Too many requests.")
        {
            Code = "rate_limit_exceeded",
            ProviderName = "jev",
            ProviderRequestId = "req-42",
            DocumentationUrl = new Uri("https://example.test/errors/rate-limit"),
            Retry = DecisionRetryHint.After(TimeSpan.FromSeconds(30)),
            Properties = new Dictionary<string, object?> { ["limit"] = 100 },
        };

        Assert.Equal("rate_limit_exceeded", error.Code);
        Assert.Equal("jev", error.ProviderName);
        Assert.Equal("req-42", error.ProviderRequestId);
        Assert.Equal(new Uri("https://example.test/errors/rate-limit"), error.DocumentationUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), error.Retry.RetryAfter);
        Assert.Equal(100, error.Properties["limit"]);
    }

    [Fact]
    public void RetryDefaultsToUnknownRatherThanToAGuess()
    {
        DecisionError error = new(DecisionErrorCategory.Transport, "The host is unreachable.");

        Assert.Equal(DecisionRetryability.Unknown, error.Retry.Retryability);
    }

    [Fact]
    public void PropertiesAreCopiedOnAssignment()
    {
        Dictionary<string, object?> source = new() { ["attempt"] = 1 };

        DecisionError error = new(DecisionErrorCategory.Timeout, "The call timed out.") { Properties = source };

        source["attempt"] = 2;

        Assert.Equal(1, error.Properties["attempt"]);
    }

    [Fact]
    public void ToString_IncludesTheProviderCodeWhenThereIsOne()
    {
        DecisionError error = new(DecisionErrorCategory.ProviderError, "Model unavailable.") { Code = "model_down" };

        Assert.Equal("ProviderError [model_down]: Model unavailable.", error.ToString());
    }
}
