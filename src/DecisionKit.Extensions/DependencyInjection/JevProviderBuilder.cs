using System;
using DecisionKit.Jev.Authentication;
using DecisionKit.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// The builder <c>AddJevProvider</c> hands back.
/// </summary>
internal sealed class JevProviderBuilder : IJevProviderBuilder
{
    private readonly JevProviderRegistration _registration;

    public JevProviderBuilder(IServiceCollection services, IHttpClientBuilder httpClient, JevProviderRegistration registration)
    {
        Services = services;
        HttpClient = httpClient;
        _registration = registration;
    }

    public IServiceCollection Services { get; }

    public string Name => _registration.Name;

    public IHttpClientBuilder HttpClient { get; }

    public IJevProviderBuilder WithCredentials(Func<IServiceProvider, IJevCredentialProvider> credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        // The registration already added the configuration-backed provider, so this replaces it
        // rather than competing with it.
        Services.RemoveAllKeyed<IJevCredentialProvider>(Name);
        Services.AddKeyedSingleton<IJevCredentialProvider>(Name, (services, _) => credentials(services));

        _registration.HasCredentials = true;

        return this;
    }

    public IJevProviderBuilder Decorate(Func<IServiceProvider, IDecisionProvider, IDecisionProvider> decorator)
    {
        ArgumentNullException.ThrowIfNull(decorator);

        _registration.Decorate(decorator);

        return this;
    }
}
