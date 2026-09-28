using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Testing.Recording;

namespace DecisionKit.Testing.Tests.Recording;

public sealed class DecisionCallLogTests
{
    private static readonly ProbabilityQuestion s_question = new(new QuestionId("q"), "Prompt");

    [Fact]
    public void AnEmptyLogReportsItself()
    {
        DecisionCallLog log = new();

        Assert.True(log.IsEmpty);
        Assert.Empty(log);
        Assert.Throws<InvalidOperationException>(() => log.Last);
        Assert.Throws<InvalidOperationException>(log.Only);
    }

    [Fact]
    public void CallsAreNumberedInOrder()
    {
        DecisionCallLog log = new();

        RecordedDecisionCall first = log.Record(Request(), DateTimeOffset.UnixEpoch);
        RecordedDecisionCall second = log.Record(Request(), DateTimeOffset.UnixEpoch.AddSeconds(1));

        Assert.Equal(0, first.Ordinal);
        Assert.Equal(1, second.Ordinal);
        Assert.Equal(2, log.Count);
        Assert.Same(second, log.Last);
    }

    [Fact]
    public void TheRequestIsKeptByReference()
    {
        DecisionCallLog log = new();
        DecisionRequest request = Request();

        log.Record(request, DateTimeOffset.UnixEpoch);

        Assert.Same(request, log[0].Request);
        Assert.Same(request.Questions, log[0].Questions);
    }

    [Fact]
    public void OnlyRequiresExactlyOneCall()
    {
        DecisionCallLog log = new();
        log.Record(Request(), DateTimeOffset.UnixEpoch);

        Assert.Same(log[0], log.Only());

        log.Record(Request(), DateTimeOffset.UnixEpoch);

        Assert.Throws<InvalidOperationException>(log.Only);
    }

    [Fact]
    public void TheLogIsEnumerableInCallOrder()
    {
        DecisionCallLog log = new();
        log.Record(Request(), DateTimeOffset.UnixEpoch);
        log.Record(Request(), DateTimeOffset.UnixEpoch.AddSeconds(1));

        List<int> ordinals = [];

        foreach (RecordedDecisionCall call in log)
        {
            ordinals.Add(call.Ordinal);
        }

        Assert.Equal([0, 1], ordinals);
    }

    [Fact]
    public void RequestsProjectsTheCapturedRequests()
    {
        DecisionCallLog log = new();
        DecisionRequest request = Request();
        log.Record(request, DateTimeOffset.UnixEpoch);

        Assert.Same(request, Assert.Single(log.Requests));
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        DecisionCallLog log = new();
        log.Record(Request(), DateTimeOffset.UnixEpoch);

        log.Clear();

        Assert.True(log.IsEmpty);
    }

    [Fact]
    public void ANullRequestIsRejected()
    {
        DecisionCallLog log = new();

        Assert.Throws<ArgumentNullException>(() => log.Record(null!, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void ARecordedCallDescribesItself()
    {
        RecordedDecisionCall call = new(3, Request(), DateTimeOffset.UnixEpoch);

        Assert.Contains("#3", call.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANegativeOrdinalIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordedDecisionCall(-1, Request(), DateTimeOffset.UnixEpoch));

    private static DecisionRequest Request() => new(QuestionSet.Create(s_question));
}
