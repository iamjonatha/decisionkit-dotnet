using System;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using Xunit;

namespace DecisionKit.Jev.Tests.Protocol;

public sealed class ResponseMapperTests
{
    private static readonly DateTimeOffset s_now = new(2026, 3, 14, 9, 15, 0, TimeSpan.Zero);

    [Fact]
    public void ToDomain_AnswersEveryQuestionTheServiceRepliedTo()
    {
        DecisionResult result = Map("response.json");

        Assert.Equal(4, result.Count);
        Assert.True(result.Contains(JevScenario.Urgent));
        Assert.True(result.Contains(JevScenario.Tone));
    }

    [Fact]
    public void ToDomain_LeavesAnUnansweredQuestionOutOfTheResult()
    {
        DecisionResult result = Map("response-partial.json");

        Assert.Equal(1, result.Count);
        Assert.False(result.Contains(JevScenario.Team));
        Assert.False(result.TryGet(JevScenario.Frustration, out _));
    }

    [Fact]
    public void ToDomain_ReportsTheModelTheServiceResolvedTo()
    {
        DecisionResult result = Map("response.json");

        Assert.Equal("jev-1.13.0", result.Metadata.ModelVersion);
    }

    [Fact]
    public void ToDomain_StampsTheResultFromTheSuppliedClock()
    {
        DecisionResult result = Map("response.json");

        Assert.Equal(s_now, result.Metadata.Timestamp);
    }

    [Fact]
    public void ToDomain_KeepsTheClientRequestIdentifier()
    {
        DecisionResult result = Map("response.json");

        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Metadata.ClientRequestId?.Value);
    }

    [Fact]
    public void ToDomain_RecordsTheIdentifierTheServiceAssigned()
    {
        ResponseMapper mapper = new(AnswerMapper.Default, new FixedTimeProvider(s_now));
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        DecisionResult result = mapper.ToDomain(JevScenario.CreateRequest(), response, "req_01HZX");

        Assert.Equal("req_01HZX", result.Metadata.ProviderRequestId);
    }

    [Fact]
    public void ToDomain_ReadsTheTokenCounts()
    {
        DecisionResult result = Map("response.json");

        Assert.True(result.Usage.TryGetMetric(DecisionUsageKeys.InputTokens, out double input));
        Assert.Equal(392, input);
        Assert.True(result.Usage.TryGetMetric(DecisionUsageKeys.OutputTokens, out double output));
        Assert.Equal(65, output);
    }

    [Fact]
    public void ToDomain_ReportsNoUsageWhenTheServiceReportedNone()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("I was charged twice."),
        };

        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response-no-usage.json"));

        DecisionResult result = CreateMapper().ToDomain(request, response);

        Assert.True(result.Usage.IsEmpty);
    }

    [Fact]
    public void ToDomain_RefusesAnAnswerToAQuestionThatWasNeverAsked()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("I was charged twice."),
        };

        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => CreateMapper().ToDomain(request, response));

        Assert.Equal(DecisionErrorCategory.UnknownResponse, exception.Category);
    }

    [Fact]
    public void ToDomain_PreservesAnUnrecognizedAnswerByDefault()
    {
        DecisionResult result = Map("response.json");

        Assert.IsType<UnknownAnswer>(result.Get(JevScenario.Tone));
    }

    [Fact]
    public void ToDomain_FailsOnAnUnrecognizedAnswerWhenAskedTo()
    {
        DecisionRequest request = new(JevScenario.CreateQuestions())
        {
            Input = DecisionInput.FromText("I was charged twice."),
            Options = new DecisionOptions { UnknownTypeHandling = UnknownTypeHandling.Fail },
        };

        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => CreateMapper().ToDomain(request, response));

        Assert.Equal(DecisionErrorCategory.UnknownResponse, exception.Category);
    }

    [Fact]
    public void ToDomain_RefusesAnAnswerItCannotRead()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("I was charged twice."),
        };

        JevResponse response = JevJsonSerialization.DeserializeResponse(
            RecordedPayload.Read("response-out-of-range.json"));

        Assert.ThrowsAny<DecisionException>(() => CreateMapper().ToDomain(request, response));
    }

    [Fact]
    public void ToDomain_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ResponseMapper.Default.ToDomain(null!, new JevResponse()));
        Assert.Throws<ArgumentNullException>(
            () => ResponseMapper.Default.ToDomain(JevScenario.CreateRequest(), null!));
    }

    private static ResponseMapper CreateMapper() => new(AnswerMapper.Default, new FixedTimeProvider(s_now));

    private static DecisionResult Map(string payload)
    {
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read(payload));

        return CreateMapper().ToDomain(JevScenario.CreateRequest(), response);
    }
}
