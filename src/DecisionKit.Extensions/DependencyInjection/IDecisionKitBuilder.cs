using Microsoft.Extensions.DependencyInjection;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// Collects the DecisionKit registrations made on a service collection.
/// </summary>
/// <remarks>
/// The builder exists so that a provider registration reads as one sentence and so that provider
/// packages have somewhere to hang their own extension methods without extending
/// <see cref="IServiceCollection"/> a second time. It carries no state of its own:
/// <see cref="Services"/> is the service collection it was created from, and everything a
/// registration does, it does there.
/// </remarks>
public interface IDecisionKitBuilder
{
    /// <summary>
    /// Gets the service collection the registrations are made in.
    /// </summary>
    IServiceCollection Services { get; }
}
