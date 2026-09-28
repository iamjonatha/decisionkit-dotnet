using System;
using DecisionKit.Jev.Client;
using Xunit;

namespace DecisionKit.Jev.Tests.Client;

public sealed class JevEndpointTests
{
    [Fact]
    public void Default_AddressesThePublicService()
    {
        Assert.Equal("https://api.typesafe.ai/", JevEndpoint.Default.BaseAddress.AbsoluteUri);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", JevEndpoint.Default.Evaluate.AbsoluteUri);
        Assert.Equal("https://api.typesafe.ai/v1/models", JevEndpoint.Default.Models.AbsoluteUri);
    }

    [Fact]
    public void Constructor_KeepsAProxyPrefixInsteadOfDroppingItsLastSegment()
    {
        JevEndpoint endpoint = new("https://gateway.corp.example/typesafe");

        Assert.Equal("https://gateway.corp.example/typesafe/v1/systemone", endpoint.Evaluate.AbsoluteUri);
    }

    [Fact]
    public void Constructor_AcceptsAnAddressThatAlreadyEndsWithASlash()
    {
        JevEndpoint endpoint = new("https://gateway.corp.example/typesafe/");

        Assert.Equal("https://gateway.corp.example/typesafe/v1/systemone", endpoint.Evaluate.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://api.typesafe.ai/v1")]
    [InlineData("https://api.typesafe.ai/v1/")]
    [InlineData("https://api.typesafe.ai/V1/")]
    public void Constructor_RefusesAnAddressThatAlreadyCarriesTheVersion(string address) =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint(address));

    [Fact]
    public void Constructor_RefusesPlainHttpForARemoteHost() =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint("http://api.typesafe.ai"));

    [Theory]
    [InlineData("http://localhost:5005")]
    [InlineData("http://127.0.0.1:5005")]
    public void Constructor_AllowsPlainHttpOnLoopbackSoThatAStubStaysUsable(string address)
    {
        JevEndpoint endpoint = new(address);

        Assert.Equal(Uri.UriSchemeHttp, endpoint.BaseAddress.Scheme);
    }

    [Fact]
    public void Constructor_RefusesASchemeItCannotSpeak() =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint("ftp://api.typesafe.ai"));

    [Fact]
    public void Constructor_RefusesCredentialsInTheAddress() =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint("https://user:secret@api.typesafe.ai"));

    [Theory]
    [InlineData("https://api.typesafe.ai/?tenant=acme")]
    [InlineData("https://api.typesafe.ai/#fragment")]
    public void Constructor_RefusesAQueryOrAFragment(string address) =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint(address));

    [Fact]
    public void Constructor_RefusesARelativeAddress() =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint(new Uri("/v1", UriKind.Relative)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a uri")]
    public void Constructor_RefusesTextThatIsNotAnAbsoluteUri(string address) =>
        Assert.Throws<ArgumentException>(() => new JevEndpoint(address));

    [Fact]
    public void Constructor_RefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => new JevEndpoint((Uri)null!));
        Assert.Throws<ArgumentNullException>(() => new JevEndpoint((string)null!));
    }

    [Fact]
    public void Equals_ComparesTheBaseAddress()
    {
        Assert.Equal(new JevEndpoint("https://api.typesafe.ai"), new JevEndpoint("https://api.typesafe.ai/"));
        Assert.NotEqual(new JevEndpoint("https://api.typesafe.ai"), new JevEndpoint("https://other.example"));
        Assert.Equal(
            new JevEndpoint("https://api.typesafe.ai").GetHashCode(),
            new JevEndpoint("https://api.typesafe.ai/").GetHashCode());
    }

    [Fact]
    public void ToString_ReportsTheBaseAddress() =>
        Assert.Equal("https://api.typesafe.ai/", JevEndpoint.Default.ToString());
}
