using System;
using DecisionKit.Jev.Authentication;
using DecisionKit.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// One JEV provider registration, open for the three things an application usually changes about
/// it: how it authenticates, how it reaches the network, and what wraps it.
/// </summary>
public interface IJevProviderBuilder : IDecisionKitBuilder
{
    /// <summary>
    /// Gets the name this provider is registered under.
    /// </summary>
    /// <remarks>
    /// It is the key the provider is resolved by, the name of its options, and the name it reports
    /// on results and failures unless the settings override it.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the builder for the named <see cref="System.Net.Http.HttpClient"/> this provider sends
    /// through.
    /// </summary>
    /// <remarks>
    /// Everything <c>IHttpClientFactory</c> offers is reachable from here: additional message
    /// handlers, a different primary handler, a proxy, client certificates, a resilience pipeline
    /// the application already owns.
    /// </remarks>
    IHttpClientBuilder HttpClient { get; }

    /// <summary>
    /// Authenticates this provider with something other than the configured API key.
    /// </summary>
    /// <param name="credentials">Builds the credential provider from the container.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Calling this replaces the credential provider that reads <c>ApiKey</c> from configuration,
    /// and tells startup validation to stop insisting on one.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="credentials"/> is <see langword="null"/>.</exception>
    IJevProviderBuilder WithCredentials(Func<IServiceProvider, IJevCredentialProvider> credentials);

    /// <summary>
    /// Wraps this provider in another one.
    /// </summary>
    /// <param name="decorator">Receives the container and the provider so far, and returns the wrapper.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Decorators are applied in the order they are registered, so the last one added is the
    /// outermost and sees every call first. This is how caching, metrics or an application's own
    /// policy are composed onto the provider without the provider knowing they exist.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="decorator"/> is <see langword="null"/>.</exception>
    IJevProviderBuilder Decorate(Func<IServiceProvider, IDecisionProvider, IDecisionProvider> decorator);
}
