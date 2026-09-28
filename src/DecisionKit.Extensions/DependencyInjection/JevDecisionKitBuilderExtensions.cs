using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using DecisionKit.Extensions.Configuration;
using DecisionKit.Extensions.DependencyInjection;
using DecisionKit.Jev.Authentication;
using DecisionKit.Jev.Protocol;
using DecisionKit.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// Registration extensions live in the namespace every application already has in scope, so that
// AddJevProvider() is reachable without a using directive. The folder cannot be named after it.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registers the TypeSafe JEV provider.
/// </summary>
/// <remarks>
/// <para>
/// A registration owns four things: a named options instance, a named
/// <see cref="System.Net.Http.HttpClient"/>, a credential provider and the provider itself. All
/// four are keyed by the registration name, so an application can register JEV twice — a fast model
/// for one workload and a careful one for another — and resolve either by name.
/// </para>
/// <para>
/// The provider is registered both as a keyed singleton and as a plain
/// <see cref="IDecisionProvider"/>, so <c>GetRequiredService&lt;IDecisionProvider&gt;()</c> works in
/// the common case of exactly one, <c>GetRequiredKeyedService</c> works when there are several, and
/// <c>IEnumerable&lt;IDecisionProvider&gt;</c> sees every one of them. There is one instance either
/// way.
/// </para>
/// </remarks>
public static class JevDecisionKitBuilderExtensions
{
    /// <summary>
    /// Registers the JEV provider under its default name, configured from whatever already binds
    /// its options.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <returns>The builder for this registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(this IDecisionKitBuilder builder) =>
        builder.AddJevProvider(JevProtocol.ProviderName);

    /// <summary>
    /// Registers the JEV provider under its default name and configures it in code.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <param name="configure">Fills in the settings.</param>
    /// <returns>The builder for this registration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(this IDecisionKitBuilder builder, Action<JevProviderSettings> configure) =>
        builder.AddJevProvider(JevProtocol.ProviderName, configure);

    /// <summary>
    /// Registers the JEV provider under its default name and binds it to a configuration section.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <param name="configuration">The section holding the settings.</param>
    /// <returns>The builder for this registration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(this IDecisionKitBuilder builder, IConfiguration configuration) =>
        builder.AddJevProvider(JevProtocol.ProviderName, configuration);

    /// <summary>
    /// Registers the JEV provider under a name of your choosing.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <param name="name">The name to register and resolve it under.</param>
    /// <returns>The builder for this registration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(this IDecisionKitBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IServiceCollection services = builder.Services;

        RequireUnused(services, name);

        JevProviderRegistration registration = new(name);

        services.AddOptions<JevProviderSettings>(name).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JevProviderSettings>>(new JevSettingsValidator(registration));

        IHttpClientBuilder http = services
            .AddHttpClient(registration.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(JevProviderRegistration.CreateHandler)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddKeyedSingleton<IJevCredentialProvider>(name, (provider, _) => registration.CreateCredentials(provider));
        services.AddKeyedSingleton<IDecisionProvider>(name, (provider, _) => registration.Create(provider));
        services.AddSingleton(provider => provider.GetRequiredKeyedService<IDecisionProvider>(name));

        return new JevProviderBuilder(services, http, registration);
    }

    /// <summary>
    /// Registers the JEV provider under a name of your choosing and configures it in code.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <param name="name">The name to register and resolve it under.</param>
    /// <param name="configure">Fills in the settings.</param>
    /// <returns>The builder for this registration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(
        this IDecisionKitBuilder builder,
        string name,
        Action<JevProviderSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        IJevProviderBuilder jev = builder.AddJevProvider(name);

        jev.Services.Configure(name, configure);

        return jev;
    }

    /// <summary>
    /// Registers the JEV provider under a name of your choosing and binds it to a configuration
    /// section.
    /// </summary>
    /// <param name="builder">The DecisionKit builder.</param>
    /// <param name="name">The name to register and resolve it under.</param>
    /// <param name="configuration">The section holding the settings.</param>
    /// <returns>The builder for this registration.</returns>
    /// <remarks>
    /// <para>
    /// The section is re-read when the configuration reloads. Only the API key acts on that: the
    /// credential provider asks for it on every call, so a rotated key is picked up without a
    /// restart. An endpoint, a timeout or a retry policy is read once, when the provider is built,
    /// because swapping one under calls already in flight would be a change nobody asked for at a
    /// moment nobody chose.
    /// </para>
    /// <para>
    /// The keys are the property names of <see cref="JevProviderSettings"/>, with the retry settings
    /// nested under <c>Retry</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered.</exception>
    public static IJevProviderBuilder AddJevProvider(
        this IDecisionKitBuilder builder,
        string name,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IJevProviderBuilder jev = builder.AddJevProvider(name);

        jev.Services.Configure<JevProviderSettings>(name, settings => JevSettingsBinder.Bind(name, configuration, settings));
        jev.Services.AddSingleton<IOptionsChangeTokenSource<JevProviderSettings>>(
            new ConfigurationChangeTokenSource<JevProviderSettings>(name, configuration));

        return jev;
    }

    /// <summary>
    /// Configures an already registered JEV provider.
    /// </summary>
    /// <param name="builder">The provider builder.</param>
    /// <param name="configure">Fills in the settings.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// This runs after anything bound from configuration, so it is also how an application
    /// overrides one value a section supplied.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IJevProviderBuilder Configure(this IJevProviderBuilder builder, Action<JevProviderSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(builder.Name, configure);

        return builder;
    }

    /// <summary>
    /// Authenticates this provider with a key held in code.
    /// </summary>
    /// <param name="builder">The provider builder.</param>
    /// <param name="apiKey">The key.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Use this for a key the application already holds in memory. A key that lives in a secret
    /// store belongs behind <see cref="IJevProviderBuilder.WithCredentials"/>, which is asked for it
    /// on every call and therefore survives a rotation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is empty or whitespace.</exception>
    public static IJevProviderBuilder WithApiKey(this IJevProviderBuilder builder, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return builder.Configure(settings => settings.ApiKey = apiKey);
    }

    /// <summary>
    /// Authenticates this provider with a credential provider the application owns.
    /// </summary>
    /// <param name="builder">The provider builder.</param>
    /// <param name="credentials">The credential provider.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IJevProviderBuilder WithCredentials(this IJevProviderBuilder builder, IJevCredentialProvider credentials)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(credentials);

        return builder.WithCredentials(_ => credentials);
    }

    private static void RequireUnused(IServiceCollection services, string name)
    {
        bool taken = services.Any(descriptor =>
            descriptor.ServiceType == typeof(IDecisionProvider)
            && descriptor.IsKeyedService
            && string.Equals(descriptor.ServiceKey as string, name, StringComparison.Ordinal));

        if (!taken)
        {
            return;
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"A decision provider named '{name}' is already registered. Give the second registration its own name, and resolve each one with GetRequiredKeyedService<IDecisionProvider>(name)."));
    }
}
