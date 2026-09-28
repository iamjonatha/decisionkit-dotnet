using System;
using System.Collections.Generic;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Core.Tests.Providers;

public sealed class DecisionProviderCapabilitiesTests
{
    [Fact]
    public void None_DeclaresNothing()
    {
        Assert.Empty(DecisionProviderCapabilities.None.SupportedQuestionTypes);
        Assert.False(DecisionProviderCapabilities.None.SupportsIdempotencyKeys);
        Assert.False(DecisionProviderCapabilities.None.SupportsUsageReporting);
        Assert.False(DecisionProviderCapabilities.None.SupportsRequestMetadata);
        Assert.False(DecisionProviderCapabilities.None.SupportsBatching);
        Assert.False(DecisionProviderCapabilities.None.SupportsExplanations);
    }

    [Fact]
    public void Supports_AnswersForADeclaredType()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(ProbabilityQuestion)],
        };

        Type probabilityQuestionType = typeof(ProbabilityQuestion);

        Assert.True(capabilities.Supports<ProbabilityQuestion>());
        Assert.True(capabilities.Supports(probabilityQuestionType));
        Assert.False(capabilities.Supports<ScoreQuestion>());
    }

    [Fact]
    public void Supports_IsByExactTypeSoABaseTypeDoesNotCoverItsSubclasses()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(Question)],
        };

        Assert.False(capabilities.Supports<ProbabilityQuestion>());
    }

    [Fact]
    public void Supports_RejectsANullType()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionProviderCapabilities.None.Supports(null!));
    }

    [Fact]
    public void SupportedQuestionTypes_RejectsATypeThatIsNotAQuestion()
    {
        Assert.Throws<ArgumentException>(() => new DecisionProviderCapabilities
        {
            SupportedQuestionTypes = [typeof(string)],
        });
    }

    [Fact]
    public void SupportedQuestionTypes_AreCopiedOnAssignment()
    {
        List<Type> source = [typeof(ProbabilityQuestion)];

        DecisionProviderCapabilities capabilities = new() { SupportedQuestionTypes = source };

        source.Add(typeof(ScoreQuestion));

        Assert.False(capabilities.Supports<ScoreQuestion>());
    }

    [Fact]
    public void SupportsAll_IsFalseWhenOneQuestionTypeIsMissing()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(ProbabilityQuestion)],
        };

        QuestionSet supported = QuestionSet.Create(new ProbabilityQuestion(new QuestionId("a"), "Prompt a"));
        QuestionSet mixed = QuestionSet.Create(
            new ProbabilityQuestion(new QuestionId("a"), "Prompt a"),
            new ScoreQuestion(new QuestionId("b"), "Prompt b", 0, 10));

        Assert.True(capabilities.SupportsAll(supported));
        Assert.False(capabilities.SupportsAll(mixed));
    }

    [Fact]
    public void SupportsAll_RejectsANullSet()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionProviderCapabilities.None.SupportsAll(null!));
    }

    [Fact]
    public void AGenericTypeDefinition_DeclaresEveryClosedFormOfIt()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(ChoiceQuestion<>)],
        };

        Assert.True(capabilities.Supports<ChoiceQuestion<Department>>());
        Assert.True(capabilities.Supports<ChoiceQuestion<string>>());
        Assert.False(capabilities.Supports<ProbabilityQuestion>());
    }

    [Fact]
    public void AGenericTypeDefinition_SatisfiesAWholeQuestionSet()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(ChoiceQuestion<>), typeof(ProbabilityQuestion)],
        };

        QuestionSet questions = QuestionSet.Create(
            new ProbabilityQuestion(new QuestionId("a"), "Prompt a"),
            new ChoiceQuestion<Department>(new QuestionId("b"), "Prompt b", [Department.Billing]));

        Assert.True(capabilities.SupportsAll(questions));
    }

    [Fact]
    public void AClosedFormDoesNotDeclareTheOtherClosedForms()
    {
        DecisionProviderCapabilities capabilities = new()
        {
            SupportedQuestionTypes = [typeof(ChoiceQuestion<Department>)],
        };

        Assert.True(capabilities.Supports<ChoiceQuestion<Department>>());
        Assert.False(capabilities.Supports<ChoiceQuestion<string>>());
    }
}
