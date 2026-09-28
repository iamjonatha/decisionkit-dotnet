using Microsoft.Extensions.DependencyInjection;

namespace DecisionKit.Extensions.DependencyInjection;

/// <summary>
/// The builder <c>AddDecisionKit</c> hands back.
/// </summary>
internal sealed class DecisionKitBuilder : IDecisionKitBuilder
{
    public DecisionKitBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
