using System;
using DecisionKit.Answers;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Questions;

public sealed class ChoiceQuestionTests
{
    [Fact]
    public void Constructor_KeepsTheOptionsInDeclarationOrder()
    {
        ChoiceQuestion<Department> question = new(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales, Department.Billing]);

        Assert.Equal([Department.Sales, Department.Billing], question.Options);
    }

    [Fact]
    public void Constructor_RejectsAnEmptyOptionList()
    {
        Assert.Throws<ArgumentException>(() => new ChoiceQuestion<Department>(new QuestionId("routing"), "Which department?", []));
    }

    [Fact]
    public void Constructor_RejectsDuplicatedOptions()
    {
        Assert.Throws<ArgumentException>(() => new ChoiceQuestion<string>(
            new QuestionId("routing"),
            "Which department?",
            ["billing", "billing"]));
    }

    [Fact]
    public void Constructor_RejectsANullOption()
    {
        Assert.Throws<ArgumentException>(() => new ChoiceQuestion<string>(
            new QuestionId("routing"),
            "Which department?",
            ["billing", null!]));
    }

    [Fact]
    public void StringOptions_SupportRuntimeDrivenConfigurations()
    {
        ChoiceQuestion<string> question = new(
            new QuestionId("routing"),
            "Which queue?",
            ["first-line", "second-line"]);

        Assert.Equal(typeof(ChoiceAnswer<string>), question.AnswerType);
        Assert.Equal(2, question.Options.Count);
    }

    [Fact]
    public void OptionValues_ExposeTheOptionsToCodeThatDoesNotKnowTheOptionType()
    {
        ChoiceQuestion<Department> question = new(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales, Department.Billing]);

        Assert.Equal(typeof(Department), ((IChoiceQuestion)question).OptionType);
        Assert.Equal([Department.Sales, Department.Billing], ((IChoiceQuestion)question).OptionValues);
    }

    [Fact]
    public void OptionValues_AreTheSameInstanceOnEveryRead()
    {
        ChoiceQuestion<string> question = new(new QuestionId("routing"), "Which queue?", ["first-line"]);

        Assert.Same(((IChoiceQuestion)question).OptionValues, ((IChoiceQuestion)question).OptionValues);
    }

    [Fact]
    public void CreateAnswer_BuildsTheAnswerTheQuestionExpects()
    {
        ChoiceQuestion<Department> question = new(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales, Department.Billing]);

        ChoiceAnswer<Department> answer = question.CreateAnswer(Department.Billing);

        Assert.Equal(question.Id, answer.QuestionId);
        Assert.Equal(Department.Billing, answer.Value.Selection);
    }

    [Fact]
    public void CreateAnswer_RejectsAnOptionTheQuestionDoesNotOffer()
    {
        ChoiceQuestion<Department> question = new(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales, Department.Billing]);

        Assert.Throws<ArgumentException>(() => question.CreateAnswer(Department.Technical));
    }

    [Fact]
    public void CreateAnswer_FromTheNonGenericViewKeepsTheOptionType()
    {
        IChoiceQuestion question = new ChoiceQuestion<Department>(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales, Department.Billing]);

        Answer answer = question.CreateAnswer(Department.Sales);

        ChoiceAnswer<Department> typed = Assert.IsType<ChoiceAnswer<Department>>(answer);
        Assert.Equal(Department.Sales, typed.Value.Selection);
    }

    [Fact]
    public void CreateAnswer_FromTheNonGenericViewRejectsTheWrongOptionType()
    {
        IChoiceQuestion question = new ChoiceQuestion<Department>(
            new QuestionId("routing"),
            "Which department should handle this?",
            [Department.Sales]);

        Assert.Throws<ArgumentException>(() => question.CreateAnswer("sales"));
    }
}
