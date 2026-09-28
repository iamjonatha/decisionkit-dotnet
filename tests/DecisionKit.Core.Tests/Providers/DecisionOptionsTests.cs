using System;
using DecisionKit.Providers;

namespace DecisionKit.Core.Tests.Providers;

public sealed class DecisionOptionsTests
{
    [Fact]
    public void Default_PreservesUnknownTypesAndAsksForNothingExtra()
    {
        Assert.Null(DecisionOptions.Default.Language);
        Assert.False(DecisionOptions.Default.IncludeExplanations);
        Assert.Equal(UnknownTypeHandling.Preserve, DecisionOptions.Default.UnknownTypeHandling);
    }

    [Fact]
    public void Language_IsTrimmed()
    {
        DecisionOptions options = new() { Language = "  en-GB  " };

        Assert.Equal("en-GB", options.Language);
    }

    [Fact]
    public void Language_RejectsWhitespaceInsteadOfTreatingItAsUnset()
    {
        Assert.Throws<ArgumentException>(() => new DecisionOptions { Language = "   " });
    }

    [Fact]
    public void Language_AcceptsNullAsUnset()
    {
        Assert.Null(new DecisionOptions { Language = null }.Language);
    }
}
