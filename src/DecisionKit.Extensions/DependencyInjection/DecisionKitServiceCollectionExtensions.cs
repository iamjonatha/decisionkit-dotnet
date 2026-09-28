using System;
using DecisionKit.Extensions.DependencyInjection;

// Registration extensions live in the namespace every application already has in scope, so that
// AddDecisionKit() is reachable without a using directive. The folder cannot be named after it.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registers DecisionKit in an application's service collection.
/// </summary>
public static class DecisionKitServiceCollectionExtensions
{
    /// <summary>
    /// Prepares the service collection for DecisionKit and returns the builder its providers are
    /// registered on.
    /// </summary>
    /// <param name="services">The service collection to register in.</param>
    /// <returns>The builder to add providers to.</returns>
    /// <remarks>
    /// <para>
    /// On its own this registers no provider and decides nothing: DecisionKit's core knows nothing
    /// about any service, so there is nothing for it to register. The call exists to give provider
    /// packages one place to attach to, and to make the intent of the next line obvious.
    /// </para>
    /// <para>
    /// Calling it more than once is harmless.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IDecisionKitBuilder AddDecisionKit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();

        return new DecisionKitBuilder(services);
    }
}
