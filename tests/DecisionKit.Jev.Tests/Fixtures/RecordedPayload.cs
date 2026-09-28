using System;
using System.IO;
using System.Reflection;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// Reads the payloads recorded under <c>Payloads</c>.
/// </summary>
/// <remarks>
/// The payloads are checked in as files rather than built by the tests on purpose: a test that
/// constructs the JSON it then parses cannot notice that the wire contract changed.
/// </remarks>
public static class RecordedPayload
{
    public static string Read(string name)
    {
        string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("The test assembly has no directory.");

        return File.ReadAllText(Path.Combine(directory, "Payloads", name));
    }
}
