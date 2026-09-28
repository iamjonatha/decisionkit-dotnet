using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Providers;
using DecisionKit.Results;

namespace DecisionKit.Extensions.Tests.Fixtures;

/// <summary>
/// A decorator that does nothing but say it was there.
/// </summary>
/// <remarks>
/// It renames the provider rather than counting only, so that a test can assert the order several
/// decorators were applied in by reading one string.
/// </remarks>
public sealed class TaggingProvider(IDecisionProvider inner, string tag) : IDecisionProvider
{
    public int Calls { get; private set; }

    public string Name => string.Create(CultureInfo.InvariantCulture, $"{tag}({inner.Name})");

    public DecisionProviderCapabilities Capabilities => inner.Capabilities;

    public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;

        return inner.DecideAsync(request, cancellationToken);
    }
}
