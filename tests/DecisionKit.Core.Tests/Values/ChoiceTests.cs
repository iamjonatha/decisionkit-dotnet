using System;
using System.Collections.Generic;
using DecisionKit.Core.Tests.Fixtures;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Values;

public sealed class ChoiceTests
{
    [Fact]
    public void Selected_CarriesTheOptionAndNoDistribution()
    {
        Choice<Department> choice = Choice.Selected(Department.Billing);

        Assert.True(choice.HasSelection);
        Assert.Equal(Department.Billing, choice.Selection);
        Assert.False(choice.HasDistribution);
        Assert.Empty(choice.Distribution);
    }

    [Fact]
    public void Distributed_DoesNotCollapseToASelection()
    {
        Choice<Department> choice = Choice.Distributed(
        [
            new OptionProbability<Department>(Department.Billing, new Probability(0.2)),
            new OptionProbability<Department>(Department.Technical, new Probability(0.7)),
        ]);

        Assert.False(choice.HasSelection);
        Assert.True(choice.HasDistribution);
        Assert.Throws<InvalidOperationException>(() => choice.Selection);
    }

    [Fact]
    public void Distribution_KeepsProviderOrder()
    {
        Choice<string> choice = Choice.Distributed(
        [
            new OptionProbability<string>("c", new Probability(0.1)),
            new OptionProbability<string>("a", new Probability(0.6)),
            new OptionProbability<string>("b", new Probability(0.3)),
        ]);

        Assert.Equal(["c", "a", "b"], [.. Enumerate(choice)]);
    }

    [Fact]
    public void TryGetMostLikely_CollapsesTheDistributionExplicitly()
    {
        Choice<Department> choice = Choice.Distributed(
        [
            new OptionProbability<Department>(Department.Billing, new Probability(0.2)),
            new OptionProbability<Department>(Department.Technical, new Probability(0.7)),
            new OptionProbability<Department>(Department.Sales, new Probability(0.1)),
        ]);

        Assert.True(choice.TryGetMostLikely(out OptionProbability<Department> mostLikely));
        Assert.Equal(Department.Technical, mostLikely.Option);
        Assert.Equal(0.7, mostLikely.Probability.Value);
    }

    [Fact]
    public void TryGetMostLikely_OnASelectionOnlyChoice_ReturnsFalse()
    {
        Choice<Department> choice = Choice.Selected(Department.Sales);

        Assert.False(choice.TryGetMostLikely(out _));
    }

    [Fact]
    public void TryGetProbability_FindsTheOption()
    {
        Choice<Department> choice = Choice.Distributed(
        [
            new OptionProbability<Department>(Department.Billing, new Probability(0.25)),
        ]);

        Assert.True(choice.TryGetProbability(Department.Billing, out Probability probability));
        Assert.Equal(0.25, probability.Value);
        Assert.False(choice.TryGetProbability(Department.Sales, out _));
    }

    [Fact]
    public void Distributed_RejectsAnEmptyDistribution()
    {
        Assert.Throws<ArgumentException>(() => Choice.Distributed(Array.Empty<OptionProbability<Department>>()));
    }

    [Fact]
    public void Distributed_RejectsADuplicatedOption()
    {
        OptionProbability<Department>[] distribution =
        [
            new(Department.Billing, new Probability(0.5)),
            new(Department.Billing, new Probability(0.5)),
        ];

        Assert.Throws<ArgumentException>(() => Choice.Distributed(distribution));
    }

    [Fact]
    public void Distributed_RequiresTheSelectionToBePartOfTheDistribution()
    {
        OptionProbability<Department>[] distribution =
        [
            new(Department.Billing, new Probability(0.5)),
        ];

        Assert.Throws<ArgumentException>(() => Choice.Distributed(distribution, Department.Sales));
    }

    [Fact]
    public void Distributed_WithSelection_CarriesBoth()
    {
        Choice<Department> choice = Choice.Distributed(
            [
                new OptionProbability<Department>(Department.Billing, new Probability(0.9)),
                new OptionProbability<Department>(Department.Sales, new Probability(0.1)),
            ],
            Department.Billing);

        Assert.True(choice.HasSelection);
        Assert.True(choice.HasDistribution);
        Assert.Equal(Department.Billing, choice.Selection);
    }

    [Fact]
    public void Equality_ComparesSelectionAndDistribution()
    {
        Choice<Department> left = Choice.Selected(Department.Billing);
        Choice<Department> right = Choice.Selected(Department.Billing);
        Choice<Department> other = Choice.Selected(Department.Sales);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, other);
    }

    [Fact]
    public void Selected_RejectsANullOption()
    {
        Assert.Throws<ArgumentNullException>(() => Choice.Selected<string>(null!));
    }

    private static IEnumerable<string> Enumerate(Choice<string> choice)
    {
        foreach (OptionProbability<string> entry in choice.Distribution)
        {
            yield return entry.Option;
        }
    }
}
