using System;
using System.Net;
using System.Net.Http;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Tests.Fixtures;
using Xunit;

namespace DecisionKit.Jev.Tests.Client;

public sealed class JevResponseHeadersTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadRequestId_ReadsTheHeaderTheServiceStampsTheCallWith()
    {
        using HttpResponseMessage response = Respond((JevProtocol.RequestIdHeader, "req_0f3a"));

        Assert.Equal("req_0f3a", JevResponseHeaders.ReadRequestId(response));
    }

    [Fact]
    public void ReadRequestId_IsNullWhenTheServiceStampedNothing()
    {
        using HttpResponseMessage response = Respond();

        Assert.Null(JevResponseHeaders.ReadRequestId(response));
    }

    [Fact]
    public void ReadRequestId_IgnoresABlankHeader()
    {
        using HttpResponseMessage response = Respond((JevProtocol.RequestIdHeader, "   "));

        Assert.Null(JevResponseHeaders.ReadRequestId(response));
    }

    [Fact]
    public void ReadRetryAfter_ReadsTheMillisecondHeader()
    {
        using HttpResponseMessage response = Respond((JevProtocol.RetryAfterMillisecondsHeader, "2500"));

        Assert.Equal(TimeSpan.FromMilliseconds(2500), ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_PrefersTheMillisecondHeaderOverTheStandardOne()
    {
        using HttpResponseMessage response = Respond(
            (JevProtocol.RetryAfterMillisecondsHeader, "250"),
            ("Retry-After", "120"));

        Assert.Equal(TimeSpan.FromMilliseconds(250), ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_FallsBackToTheStandardDelta()
    {
        using HttpResponseMessage response = Respond(("Retry-After", "120"));

        Assert.Equal(TimeSpan.FromSeconds(120), ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_ResolvesTheStandardDateAgainstTheInjectedClock()
    {
        using HttpResponseMessage response = Respond(("Retry-After", s_now.AddSeconds(45).ToString("R")));

        Assert.Equal(TimeSpan.FromSeconds(45), ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_NeverReportsADelayInThePast()
    {
        using HttpResponseMessage response = Respond(("Retry-After", s_now.AddSeconds(-45).ToString("R")));

        Assert.Equal(TimeSpan.Zero, ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_IgnoresAMillisecondHeaderItCannotRead()
    {
        using HttpResponseMessage response = Respond(
            (JevProtocol.RetryAfterMillisecondsHeader, "soon"),
            ("Retry-After", "30"));

        Assert.Equal(TimeSpan.FromSeconds(30), ReadRetryAfter(response));
    }

    [Fact]
    public void ReadRetryAfter_IsNullWhenTheServiceAskedForNothing()
    {
        using HttpResponseMessage response = Respond();

        Assert.Null(ReadRetryAfter(response));
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response) =>
        JevResponseHeaders.ReadRetryAfter(response, new FixedTimeProvider(s_now));

    private static HttpResponseMessage Respond(params (string Name, string Value)[] headers) =>
        StubHttpMessageHandler.Respond(HttpStatusCode.OK, "{}", headers);
}
