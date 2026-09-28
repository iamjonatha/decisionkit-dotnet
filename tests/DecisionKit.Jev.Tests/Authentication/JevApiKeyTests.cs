using System;
using DecisionKit.Jev.Authentication;
using Xunit;

namespace DecisionKit.Jev.Tests.Authentication;

public sealed class JevApiKeyTests
{
    private const string Secret = "ts_live_do_not_log_this_value";

    [Fact]
    public void Constructor_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new JevApiKey(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAnEmptyKey(string value) =>
        Assert.Throws<ArgumentException>(() => new JevApiKey(value));

    [Fact]
    public void Constructor_TrimsSurroundingWhitespace() =>
        Assert.Equal(new JevApiKey(Secret), new JevApiKey($"  {Secret}\n"));

    [Fact]
    public void ToString_NeverRevealsTheKey()
    {
        JevApiKey key = new(Secret);

        Assert.DoesNotContain(Secret, key.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NeverRevealsTheKeyThroughInterpolation()
    {
        JevApiKey key = new(Secret);

        Assert.DoesNotContain(Secret, $"authenticating with {key}", StringComparison.Ordinal);
    }

    [Fact]
    public void None_HoldsNoKey()
    {
        Assert.False(JevApiKey.None.IsPresent);
        Assert.Null(JevApiKey.None.Value);
    }

    [Fact]
    public void IsPresent_IsTrueForAKey() => Assert.True(new JevApiKey(Secret).IsPresent);

    [Fact]
    public void Equals_ComparesTheKey()
    {
        Assert.Equal(new JevApiKey(Secret), new JevApiKey(Secret));
        Assert.NotEqual(new JevApiKey(Secret), new JevApiKey("ts_other"));
        Assert.True(new JevApiKey(Secret) == new JevApiKey(Secret));
        Assert.True(new JevApiKey(Secret) != JevApiKey.None);
    }

    [Fact]
    public void GetHashCode_AgreesWithEquality() =>
        Assert.Equal(new JevApiKey(Secret).GetHashCode(), new JevApiKey(Secret).GetHashCode());

    [Fact]
    public void Value_IsTheKeyItself() => Assert.Equal(Secret, new JevApiKey(Secret).Value);
}
