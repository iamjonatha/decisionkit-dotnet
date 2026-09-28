using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Providers;
using DecisionKit.Questions;
using Xunit;

namespace DecisionKit.Jev.Tests.Protocol;

public sealed class RequestMapperTests
{
    [Fact]
    public void ToWire_KeysQuestionsByTheirIdentifier()
    {
        JevRequest wire = RequestMapper.Default.ToWire(JevScenario.CreateRequest());

        Assert.Equal(
            ["is_urgent", "department", "frustration", "tone"],
            new List<string>(wire.Questions.Keys));
    }

    [Fact]
    public void ToWire_AsksForTheConfiguredModel()
    {
        RequestMapper mapper = new(new JevMappingOptions { Model = "jev-1.13.0" });

        JevRequest wire = mapper.ToWire(JevScenario.CreateRequest());

        Assert.Equal("jev-1.13.0", wire.Model);
    }

    [Fact]
    public void ToWire_AsksForTheLatestModelByDefault()
    {
        JevRequest wire = RequestMapper.Default.ToWire(JevScenario.CreateRequest());

        Assert.Equal("jev-latest", wire.Model);
    }

    [Fact]
    public void ToWire_SendsTextOnlyInputAsABareString()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("The customer is waiting."),
        };

        JevRequest wire = RequestMapper.Default.ToWire(request);

        JsonValue state = Assert.IsAssignableFrom<JsonValue>(wire.State);
        Assert.Equal("The customer is waiting.", state.GetValue<string>());
    }

    [Fact]
    public void ToWire_SendsStructuredInputAsAnObject()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromProperties(new Dictionary<string, object?> { ["channel"] = "email" }),
        };

        JevRequest wire = RequestMapper.Default.ToWire(request);

        JsonObject state = Assert.IsAssignableFrom<JsonObject>(wire.State);
        Assert.Equal("email", state["channel"]!.GetValue<string>());
        Assert.False(state.ContainsKey("text"));
    }

    [Fact]
    public void ToWire_FoldsTextIntoTheStateObjectWhenBothAreGiven()
    {
        JevRequest wire = RequestMapper.Default.ToWire(JevScenario.CreateRequest());

        JsonObject state = Assert.IsAssignableFrom<JsonObject>(wire.State);
        Assert.Equal("I was charged twice for the same order.", state["text"]!.GetValue<string>());
        Assert.Equal("email", state["channel"]!.GetValue<string>());
        Assert.Equal(2L, state["customer_tier"]!.GetValue<long>());
    }

    [Fact]
    public void ToWire_RefusesAnInputThatWouldOverwriteItsOwnTextProperty()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.Create("Hello.", new Dictionary<string, object?> { ["text"] = "Goodbye." }),
        };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => RequestMapper.Default.ToWire(request));

        Assert.Equal(JevProtocol.AmbiguousInputCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_RefusesARequestWithoutInput()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion));

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => RequestMapper.Default.ToWire(request));

        Assert.Equal(JevProtocol.InputRequiredCode, exception.Error.Code);
        Assert.Equal(DecisionErrorCategory.Validation, exception.Category);
    }

    [Fact]
    public void ToWire_RefusesARequestedLanguage()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("The customer is waiting."),
            Options = new DecisionOptions { Language = "en-GB" },
        };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => RequestMapper.Default.ToWire(request));

        Assert.Equal(JevProtocol.UnsupportedOptionCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_RefusesAnIdempotencyKey()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("The customer is waiting."),
            IdempotencyKey = new IdempotencyKey("order-4711-triage"),
        };

        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => RequestMapper.Default.ToWire(request));

        Assert.Equal(JevProtocol.UnsupportedOptionCode, exception.Error.Code);
    }

    [Fact]
    public void ToWire_IgnoresARequestForExplanations()
    {
        DecisionRequest request = new(QuestionSet.Create(JevScenario.UrgentQuestion))
        {
            Input = DecisionInput.FromText("The customer is waiting."),
            Options = new DecisionOptions { IncludeExplanations = true },
        };

        JevRequest wire = RequestMapper.Default.ToWire(request);

        Assert.Single(wire.Questions);
    }

    [Fact]
    public void ToWire_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => RequestMapper.Default.ToWire(null!));
    }
}
