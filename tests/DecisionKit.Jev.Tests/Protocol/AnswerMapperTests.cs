using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Values;
using Xunit;

namespace DecisionKit.Jev.Tests.Protocol;

public sealed class AnswerMapperTests
{
    [Fact]
    public void ToDomain_ReadsANoulAnswerAsAProbability()
    {
        JevAnswer wire = new() { Type = "noul", Noul = 0.97 };

        ProbabilityAnswer answer = Assert.IsType<ProbabilityAnswer>(
            AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire));

        Assert.Equal(0.97, answer.Value.Value);
    }

    [Fact]
    public void ToDomain_LeavesANoulAnswerWithoutAConfidence()
    {
        JevAnswer wire = new() { Type = "noul", Noul = 0.97 };

        Answer answer = AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire);

        Assert.False(JevAnswers.TryGetConfidence(answer, out _));
    }

    [Fact]
    public void ToDomain_RefusesANoulAnswerWithoutItsValue()
    {
        JevAnswer wire = new() { Type = "noul" };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire));

        Assert.Equal(DecisionErrorCategory.UnknownResponse, exception.Category);
    }

    [Fact]
    public void ToDomain_RefusesANoulValueThatIsNotAProbability()
    {
        JevAnswer wire = new() { Type = "noul", Noul = 1.4 };

        Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire));
    }

    [Fact]
    public void ToDomain_ReadsAChoiceAnswerAsTheSelectedOption()
    {
        JevAnswer wire = new() { Type = "choice", Choice = "Billing" };

        ChoiceAnswer<Department> answer = Assert.IsType<ChoiceAnswer<Department>>(
            AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire));

        Assert.True(answer.Value.TryGetSelection(out Department selection));
        Assert.Equal(Department.Billing, selection);
    }

    [Fact]
    public void ToDomain_ReadsTheChoiceDistributionIntoTheAnswer()
    {
        JevAnswer wire = new()
        {
            Type = "choice",
            Choice = "Billing",
            Probabilities = new JsonObject
            {
                ["Sales"] = 0.02,
                ["Billing"] = 0.93,
                ["Support"] = 0.05,
            },
        };

        ChoiceAnswer<Department> answer = Assert.IsType<ChoiceAnswer<Department>>(
            AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire));

        Assert.Equal(3, answer.Value.Distribution.Count);
        Assert.True(answer.Value.TryGetProbability(Department.Billing, out Probability probability));
        Assert.Equal(0.93, probability.Value);
    }

    [Fact]
    public void ToDomain_DoesNotRepeatTheChoiceDistributionInMetadata()
    {
        JevAnswer wire = new()
        {
            Type = "choice",
            Choice = "Billing",
            Probabilities = new JsonObject { ["Billing"] = 1.0 },
        };

        Answer answer = AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire);

        Assert.False(JevAnswers.TryGetLevelProbabilities(answer, out _));
    }

    [Fact]
    public void ToDomain_CarriesTheChoiceConfidenceAsMetadata()
    {
        JevAnswer wire = new() { Type = "choice", Choice = "Billing", Confidence = 0.78 };

        Answer answer = AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire);

        Assert.True(JevAnswers.TryGetConfidence(answer, out double confidence));
        Assert.Equal(0.78, confidence);
    }

    [Fact]
    public void ToDomain_ReadsAChoiceAnswerThatCarriesOnlyADistribution()
    {
        JevAnswer wire = new()
        {
            Type = "choice",
            Probabilities = new JsonObject { ["Sales"] = 0.4, ["Billing"] = 0.6 },
        };

        ChoiceAnswer<Department> answer = Assert.IsType<ChoiceAnswer<Department>>(
            AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire));

        Assert.False(answer.Value.HasSelection);
        Assert.Equal(2, answer.Value.Distribution.Count);
    }

    [Fact]
    public void ToDomain_RefusesAChoiceThatTheQuestionNeverOffered()
    {
        JevAnswer wire = new() { Type = "choice", Choice = "Legal" };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire));

        Assert.Equal(DecisionErrorCategory.UnknownResponse, exception.Category);
    }

    [Fact]
    public void ToDomain_RefusesAProbabilityThatIsNotANumber()
    {
        JevAnswer wire = new()
        {
            Type = "choice",
            Choice = "Billing",
            Probabilities = new JsonObject { ["Billing"] = "very likely" },
        };

        Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.TeamQuestion, wire));
    }

    [Fact]
    public void ToDomain_ReadsAScoreAsAPositionAcrossTheRubric()
    {
        JevAnswer wire = new() { Type = "score", Score = 1.42 };

        ScoreAnswer answer = Assert.IsType<ScoreAnswer>(
            AnswerMapper.Default.ToDomain(JevScenario.FrustrationQuestion, wire));

        Assert.Equal(1.42, answer.Value.Value);
        Assert.Equal(0, answer.Value.Minimum);
        Assert.Equal(2, answer.Value.Maximum);
    }

    [Fact]
    public void ToDomain_RefusesAScoreOutsideTheRubric()
    {
        JevAnswer wire = new() { Type = "score", Score = 7 };

        Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.FrustrationQuestion, wire));
    }

    [Fact]
    public void ToDomain_CarriesTheScoreLegendAndDistributionAsMetadata()
    {
        JevAnswer wire = new()
        {
            Type = "score",
            Score = 1.42,
            Legend = new JsonObject { ["0"] = "Calm", ["1"] = "Frustrated", ["2"] = "Angry" },
            Probabilities = new JsonObject { ["0"] = 0.12, ["1"] = 0.34, ["2"] = 0.54 },
        };

        Answer answer = AnswerMapper.Default.ToDomain(JevScenario.FrustrationQuestion, wire);

        Assert.True(JevAnswers.TryGetLegend(answer, out IReadOnlyDictionary<string, object?> legend));
        Assert.Equal("Frustrated", legend["1"]);

        Assert.True(JevAnswers.TryGetLevelProbabilities(answer, out IReadOnlyDictionary<string, object?> levels));
        Assert.Equal(0.54, levels["2"]);
    }

    [Fact]
    public void ToDomain_KeepsAnAnswerOfAnUnknownTypeIntact()
    {
        JevAnswer wire = new() { Type = "sentiment", Extensions = new Dictionary<string, System.Text.Json.JsonElement>() };

        UnknownAnswer answer = Assert.IsType<UnknownAnswer>(
            AnswerMapper.Default.ToDomain(JevScenario.ToneQuestion, wire));

        Assert.Equal("sentiment", answer.ProviderType);
        Assert.True(JevAnswers.TryGetRawPayload(answer, out string payload));
        Assert.Contains("sentiment", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_NamesAnAnswerThatArrivedWithoutAType()
    {
        JevAnswer wire = new();

        UnknownAnswer answer = Assert.IsType<UnknownAnswer>(
            AnswerMapper.Default.ToDomain(JevScenario.ToneQuestion, wire));

        Assert.Equal(JevProtocol.UnnamedAnswerType, answer.ProviderType);
    }

    [Fact]
    public void ToDomain_RefusesAnAnswerOfTheWrongKnownType()
    {
        JevAnswer wire = new() { Type = "choice", Choice = "Billing" };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire));

        Assert.Contains("noul", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_KeepsTheRawPayloadWhenAskedTo()
    {
        AnswerMapper mapper = new(new JevMappingOptions { PreserveRawPayloads = true });
        JevAnswer wire = new() { Type = "noul", Noul = 0.97 };

        Answer answer = mapper.ToDomain(JevScenario.UrgentQuestion, wire);

        Assert.True(JevAnswers.TryGetRawPayload(answer, out string payload));
        Assert.Contains("0.97", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_DiscardsTheRawPayloadByDefault()
    {
        JevAnswer wire = new() { Type = "noul", Noul = 0.97 };

        Answer answer = AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, wire);

        Assert.False(JevAnswers.TryGetRawPayload(answer, out _));
    }

    [Fact]
    public void ToDomain_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => AnswerMapper.Default.ToDomain(null!, new JevAnswer()));
        Assert.Throws<ArgumentNullException>(() => AnswerMapper.Default.ToDomain(JevScenario.UrgentQuestion, null!));
    }
}
