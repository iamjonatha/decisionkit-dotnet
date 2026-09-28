using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Core.Tests.Providers;

public sealed class DecisionRequestTests
{
    [Fact]
    public void Constructor_KeepsTheQuestionsInOrder()
    {
        DecisionRequest request = new(QuestionSet.Create(Probability("first"), Probability("second")));

        Assert.Equal("first", request.Questions[0].Id.Value);
        Assert.Equal("second", request.Questions[1].Id.Value);
    }

    [Fact]
    public void Constructor_RejectsARequestThatAsksNothing()
    {
        Assert.Throws<ArgumentException>(() => new DecisionRequest(QuestionSet.Empty));
    }

    [Fact]
    public void Constructor_RejectsANullQuestionSet()
    {
        Assert.Throws<ArgumentNullException>(() => new DecisionRequest((QuestionSet)null!));
    }

    [Fact]
    public void Constructor_AcceptsASequenceOfQuestions()
    {
        DecisionRequest request = new(new List<Question> { Probability("first") });

        Assert.Single(request.Questions);
    }

    [Fact]
    public void Defaults_AreEmptyInputAndDefaultOptions()
    {
        DecisionRequest request = new(QuestionSet.Create(Probability("first")));

        Assert.True(request.Input.IsEmpty);
        Assert.Same(DecisionOptions.Default, request.Options);
        Assert.Null(request.ClientRequestId);
        Assert.Null(request.IdempotencyKey);
        Assert.Empty(request.Metadata);
    }

    [Fact]
    public void CarriesCorrelationIdentifiers()
    {
        RequestId clientRequestId = RequestId.New();
        IdempotencyKey idempotencyKey = new("decision-42");

        DecisionRequest request = new(QuestionSet.Create(Probability("first")))
        {
            ClientRequestId = clientRequestId,
            IdempotencyKey = idempotencyKey,
        };

        Assert.Equal(clientRequestId, request.ClientRequestId);
        Assert.Equal(idempotencyKey, request.IdempotencyKey);
    }

    [Fact]
    public void RejectsAnUninitializedClientRequestId()
    {
        Assert.Throws<ArgumentException>(() => new DecisionRequest(QuestionSet.Create(Probability("first")))
        {
            ClientRequestId = default(RequestId),
        });
    }

    [Fact]
    public void RejectsAnUninitializedIdempotencyKey()
    {
        Assert.Throws<ArgumentException>(() => new DecisionRequest(QuestionSet.Create(Probability("first")))
        {
            IdempotencyKey = default(IdempotencyKey),
        });
    }

    [Fact]
    public void RejectsANullInputOrNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new DecisionRequest(QuestionSet.Create(Probability("first")))
        {
            Input = null!,
        });

        Assert.Throws<ArgumentNullException>(() => new DecisionRequest(QuestionSet.Create(Probability("first")))
        {
            Options = null!,
        });
    }

    [Fact]
    public void MetadataIsCopiedOnAssignment()
    {
        Dictionary<string, object?> source = new() { ["tenant"] = "acme" };

        DecisionRequest request = new(QuestionSet.Create(Probability("first"))) { Metadata = source };

        source["tenant"] = "other";

        Assert.Equal("acme", request.Metadata["tenant"]);
    }

    private static ProbabilityQuestion Probability(string id) => new(new QuestionId(id), $"Prompt for {id}");
}
