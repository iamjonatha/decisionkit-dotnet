using System;
using DecisionKit.Identifiers;

namespace DecisionKit.Core.Tests.Identifiers;

public sealed class QuestionIdTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsMissingValue(string? value)
    {
        Assert.Throws<ArgumentException>(() => new QuestionId(value!));
    }

    [Fact]
    public void Constructor_TrimsSurroundingWhitespace()
    {
        QuestionId questionId = new("  frustration  ");

        Assert.Equal("frustration", questionId.Value);
    }

    [Fact]
    public void Equality_IsValueBasedAndOrdinal()
    {
        QuestionId left = new("frustration");
        QuestionId right = new("frustration");
        QuestionId different = new("Frustration");

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left != different);
    }

    [Fact]
    public void TryCreate_ReportsInvalidValueWithoutThrowing()
    {
        Assert.False(QuestionId.TryCreate("  ", out QuestionId questionId));
        Assert.True(questionId.IsEmpty);
    }

    [Fact]
    public void Value_OnDefaultInstance_Throws()
    {
        QuestionId questionId = default;

        Assert.True(questionId.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => questionId.Value);
    }
}
