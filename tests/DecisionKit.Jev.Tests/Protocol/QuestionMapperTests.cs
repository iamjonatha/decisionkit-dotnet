using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Questions;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Questions;
using Xunit;

namespace DecisionKit.Jev.Tests.Protocol;

public sealed class QuestionMapperTests
{
    [Fact]
    public void ToWire_SendsAProbabilityQuestionAsNoul()
    {
        JevQuestion wire = QuestionMapper.Default.ToWire(JevScenario.UrgentQuestion);

        Assert.Equal("noul", wire.Type);
        Assert.Equal("Whether the ticket needs attention today", wire.Instructions!.GetValue<string>());
        Assert.Null(wire.Criteria);
    }

    [Fact]
    public void ToWire_SendsChoiceOptionsAsCriteriaKeys()
    {
        JevQuestion wire = QuestionMapper.Default.ToWire(JevScenario.TeamQuestion);

        Assert.Equal("choice", wire.Type);

        JsonObject criteria = Assert.IsAssignableFrom<JsonObject>(wire.Criteria);
        List<string> options = [];

        foreach (KeyValuePair<string, JsonNode?> entry in criteria)
        {
            Assert.Null(entry.Value);
            options.Add(entry.Key);
        }

        Assert.Equal(["Sales", "Billing", "Support"], options);
    }

    [Fact]
    public void ToWire_SendsAScoreRubricAsAnOrderedArray()
    {
        JevQuestion wire = QuestionMapper.Default.ToWire(JevScenario.FrustrationQuestion);

        Assert.Equal("score", wire.Type);

        JsonArray criteria = Assert.IsAssignableFrom<JsonArray>(wire.Criteria);
        Assert.Equal(3, criteria.Count);
        Assert.Equal("Calm, just stating facts", criteria[0]!.GetValue<string>());
        Assert.Equal("Very angry, strong language", criteria[2]!.GetValue<string>());
    }

    [Fact]
    public void ToWire_RefusesAScoreQuestionWithoutARubric()
    {
        ScoreQuestion question = new(new QuestionId("priority"), "How urgent is this message?", 1, 5);

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal(JevProtocol.UnsupportedQuestionCode, exception.Error.Code);
        Assert.Contains(nameof(JevScoreQuestion), exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToWire_PassesAnUnknownQuestionThroughWithItsOwnDefinition()
    {
        JevQuestion wire = QuestionMapper.Default.ToWire(JevScenario.ToneQuestion);

        Assert.Equal("sentiment", wire.Type);

        JsonObject criteria = Assert.IsAssignableFrom<JsonObject>(wire.Criteria);
        Assert.Equal(2, criteria["labels"]!.AsArray().Count);
    }

    [Fact]
    public void ToWire_RefusesAnUnknownQuestionWhoseDefinitionIsNotJson()
    {
        UnknownQuestion question = new(new QuestionId("tone"), "What is the tone?", "sentiment")
        {
            RawDefinition = "{not json",
        };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal(JevProtocol.InvalidQuestionDefinitionCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_RefusesTwoOptionsThatTravelUnderTheSameName()
    {
        ChoiceQuestion<TextOption> question = new(
            new QuestionId("colour"),
            "Which colour?",
            [new TextOption("red"), new TextOption("red")]);

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal(JevProtocol.AmbiguousOptionsCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_RefusesAnOptionWithoutText()
    {
        ChoiceQuestion<TextOption> question = new(
            new QuestionId("colour"),
            "Which colour?",
            [new TextOption("red"), new TextOption("   ")]);

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal(JevProtocol.AmbiguousOptionsCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_RefusesAQuestionTypeItCannotExpress()
    {
        ExoticQuestion question = new(new QuestionId("exotic"), "Anything at all?");

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal(JevProtocol.UnsupportedQuestionCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_ReportsTheQuestionThatCouldNotBeSent()
    {
        ExoticQuestion question = new(new QuestionId("exotic"), "Anything at all?");

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => QuestionMapper.Default.ToWire(question));

        Assert.Equal("exotic", Assert.Contains("question_id", exception.Error.Properties));
    }

    [Fact]
    public void ToWire_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => QuestionMapper.Default.ToWire(null!));
    }
}
