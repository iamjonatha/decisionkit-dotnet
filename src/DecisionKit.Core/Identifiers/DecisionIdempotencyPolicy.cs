namespace DecisionKit.Identifiers;

/// <summary>
/// States what a resilience layer does about idempotency when it repeats a call.
/// </summary>
/// <remarks>
/// <para>
/// Repeating a call is only safe when the provider can recognize the repetition as the same logical
/// operation. This enumeration names the three positions a deployment can take on that, so that the
/// position is configured and documented rather than implied by whichever retry policy happens to
/// be in effect.
/// </para>
/// <para>
/// Not every provider can offer every value. A provider that documents no idempotency mechanism
/// accepts <see cref="Disabled"/> only, because generating or forwarding a key that the service
/// will not read would claim a guarantee that does not exist.
/// </para>
/// </remarks>
public enum DecisionIdempotencyPolicy
{
    /// <summary>
    /// No key is sent, and none is required. A repeated call is a second, independent operation as
    /// far as the provider is concerned.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The key the caller attached to the request is used, and none is invented. A request without
    /// one is not repeated.
    /// </summary>
    ExplicitOnly = 1,

    /// <summary>
    /// A key is generated for every request that carries none, so that any call can be repeated
    /// safely.
    /// </summary>
    Automatic = 2,
}
