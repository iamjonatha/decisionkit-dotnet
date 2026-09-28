using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Results;

namespace DecisionKit.Core.Tests.Results;

public sealed class DecisionMetadataTests
{
    [Fact]
    public void Constructor_KeepsProviderAndTimestamp()
    {
        DateTimeOffset timestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        DecisionMetadata metadata = new("  jev  ", timestamp);

        Assert.Equal("jev", metadata.ProviderName);
        Assert.Equal(timestamp, metadata.Timestamp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsAMissingProviderName(string? providerName)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DecisionMetadata(providerName!, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void ThreeCorrelationIdentifiersAreKeptApart()
    {
        DecisionMetadata metadata = new("jev", DateTimeOffset.UnixEpoch)
        {
            ClientRequestId = new RequestId("client-1"),
            ProviderRequestId = "provider-1",
            TraceId = "trace-1",
        };

        Assert.Equal("client-1", metadata.ClientRequestId?.Value);
        Assert.Equal("provider-1", metadata.ProviderRequestId);
        Assert.Equal("trace-1", metadata.TraceId);
    }

    [Fact]
    public void OptionalMembersDefaultToNull()
    {
        DecisionMetadata metadata = new("jev", DateTimeOffset.UnixEpoch);

        Assert.Null(metadata.ClientRequestId);
        Assert.Null(metadata.ProviderRequestId);
        Assert.Null(metadata.TraceId);
        Assert.Null(metadata.ModelVersion);
        Assert.Null(metadata.Latency);
        Assert.Empty(metadata.Properties);
    }

    [Fact]
    public void Latency_RejectsANegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionMetadata("jev", DateTimeOffset.UnixEpoch)
        {
            Latency = TimeSpan.FromMilliseconds(-1),
        });
    }

    [Fact]
    public void Properties_AreCopiedOnAssignment()
    {
        Dictionary<string, object?> source = new(StringComparer.Ordinal)
        {
            ["currency"] = "EUR",
        };

        DecisionMetadata metadata = new("jev", DateTimeOffset.UnixEpoch)
        {
            Properties = source,
        };

        source["currency"] = "USD";

        Assert.Equal("EUR", metadata.Properties["currency"]);
    }
}
