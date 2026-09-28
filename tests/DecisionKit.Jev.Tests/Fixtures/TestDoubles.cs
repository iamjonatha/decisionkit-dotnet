using System;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// An option whose text is chosen by the test, so that label collisions and blank labels can be
/// provoked deliberately.
/// </summary>
public sealed class TextOption(string text)
{
    public override string ToString() => text;
}

/// <summary>
/// A clock that never moves, so that a mapped timestamp can be asserted exactly.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
