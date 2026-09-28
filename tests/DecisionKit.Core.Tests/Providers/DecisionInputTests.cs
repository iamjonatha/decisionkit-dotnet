using System;
using System.Collections.Generic;
using DecisionKit.Providers;

namespace DecisionKit.Core.Tests.Providers;

public sealed class DecisionInputTests
{
    [Fact]
    public void Empty_CarriesNothing()
    {
        Assert.True(DecisionInput.Empty.IsEmpty);
        Assert.Null(DecisionInput.Empty.Text);
        Assert.Empty(DecisionInput.Empty.Properties);
    }

    [Fact]
    public void FromText_CarriesTheText()
    {
        DecisionInput input = DecisionInput.FromText("The customer asked for a refund twice.");

        Assert.Equal("The customer asked for a refund twice.", input.Text);
        Assert.False(input.IsEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromText_RejectsUnusableText(string? text)
    {
        Assert.ThrowsAny<ArgumentException>(() => DecisionInput.FromText(text!));
    }

    [Fact]
    public void FromProperties_CarriesTheState()
    {
        DecisionInput input = DecisionInput.FromProperties(new Dictionary<string, object?> { ["tier"] = "gold" });

        Assert.Equal("gold", input.Properties["tier"]);
        Assert.Null(input.Text);
        Assert.False(input.IsEmpty);
    }

    [Fact]
    public void PropertiesAreCopiedOnCreation()
    {
        Dictionary<string, object?> source = new() { ["tier"] = "gold" };

        DecisionInput input = DecisionInput.FromProperties(source);

        source["tier"] = "silver";

        Assert.Equal("gold", input.Properties["tier"]);
    }

    [Fact]
    public void Create_CarriesTextAndStateTogether()
    {
        DecisionInput input = DecisionInput.Create("ticket body", new Dictionary<string, object?> { ["tier"] = "gold" });

        Assert.Equal("ticket body", input.Text);
        Assert.Equal("gold", input.Properties["tier"]);
    }

    [Fact]
    public void Create_AcceptsNullTextButNotEmptyText()
    {
        DecisionInput input = DecisionInput.Create(null, new Dictionary<string, object?> { ["tier"] = "gold" });

        Assert.Null(input.Text);
        Assert.Throws<ArgumentException>(() => DecisionInput.Create("  ", new Dictionary<string, object?>()));
    }
}
