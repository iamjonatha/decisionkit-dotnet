using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using DecisionKit.Extensions.Configuration;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Client;
using DecisionKit.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// Everything one JEV registration knows about itself, shared between the registration calls and
/// the factory that eventually builds the provider.
/// </summary>
/// <remarks>
/// <para>
/// The instance is created while the service collection is being filled and captured by the
/// factories registered in it, so a later call on the builder — a decorator, a credential provider
/// — is seen by a factory that was registered earlier. That is the whole trick: registration order
/// stops mattering, because nothing is read until the container resolves.
/// </para>
/// <para>
/// It is mutated only during registration, which happens on one thread before the container exists,
/// and read only afterwards.
/// </para>
/// </remarks>
internal sealed class JevProviderRegistration
{
    private readonly List<Func<IServiceProvider, IDecisionProvider, IDecisionProvider>> _decorators = [];

    public JevProviderRegistration(string name)
    {
        Name = name;
        HttpClientName = string.Create(CultureInfo.InvariantCulture, $"DecisionKit.Jev.{name}");
    }

    public string Name { get; }

    public string HttpClientName { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the application supplied a credential provider of
    /// its own, which is what lets a registration validate without an API key in configuration.
    /// </summary>
    public bool HasCredentials { get; set; }

    public void Decorate(Func<IServiceProvider, IDecisionProvider, IDecisionProvider> decorator) =>
        _decorators.Add(decorator);

    /// <summary>
    /// Builds the provider this registration describes, decorators included.
    /// </summary>
    /// <exception cref="OptionsValidationException">The settings describe an unusable provider.</exception>
    public IDecisionProvider Create(IServiceProvider services)
    {
        JevProviderSettings settings = services.GetRequiredService<IOptionsMonitor<JevProviderSettings>>().Get(Name);
        JevProviderOptions options = JevSettingsMapper.ToOptions(Name, settings);

        IDecisionProvider provider = new JevDecisionProvider(
            services.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            services.GetRequiredKeyedService<IJevCredentialProvider>(Name),
            options,
            services.GetService<ILogger<JevDecisionProvider>>() ?? NullLogger<JevDecisionProvider>.Instance,
            services.GetService<TimeProvider>() ?? TimeProvider.System);

        foreach (Func<IServiceProvider, IDecisionProvider, IDecisionProvider> decorator in _decorators)
        {
            provider = decorator(services, provider) ?? throw Discarded();
        }

        return provider;
    }

    /// <summary>
    /// Builds the credential provider used when the application registered none.
    /// </summary>
    /// <remarks>
    /// The key is read from the current settings on every call rather than captured once, so a
    /// configuration reload rotates it with nothing to restart.
    /// </remarks>
    public IJevCredentialProvider CreateCredentials(IServiceProvider services)
    {
        IOptionsMonitor<JevProviderSettings> settings = services.GetRequiredService<IOptionsMonitor<JevProviderSettings>>();

        return new DelegateJevCredentialProvider(() => ApiKey(settings.Get(Name)));
    }

    /// <summary>
    /// Builds the primary handler the named client sends through.
    /// </summary>
    /// <remarks>
    /// The provider is a singleton and holds its client for the lifetime of the application, so the
    /// handler rotation <c>IHttpClientFactory</c> normally relies on to notice a DNS change never
    /// happens. A pooled connection lifetime achieves the same thing from inside the handler, which
    /// is the supported way to keep a long-lived client honest. An application that needs a
    /// different primary handler configures one on the builder, and the last one wins.
    /// </remarks>
    public static HttpMessageHandler CreateHandler() =>
        SocketsHttpHandler.IsSupported
            ? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }
            : new HttpClientHandler();

    private static JevApiKey ApiKey(JevProviderSettings settings) =>
        string.IsNullOrWhiteSpace(settings.ApiKey) ? JevApiKey.None : new JevApiKey(settings.ApiKey);

    private InvalidOperationException Discarded() => new(
        string.Create(
            CultureInfo.InvariantCulture,
            $"A decorator registered on the JEV provider '{Name}' returned null. A decorator must return the provider to use, which is either the one it was given or a wrapper around it."));
}
