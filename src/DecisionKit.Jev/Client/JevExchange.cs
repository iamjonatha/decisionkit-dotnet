using DecisionKit.Jev.Models;

namespace DecisionKit.Jev.Client;

/// <summary>
/// One completed JEV call: what the service said, and what it said about the call itself.
/// </summary>
/// <remarks>
/// The request identifier and the status code are not in the body, so they would be lost if the
/// transport returned only a <see cref="JevResponse"/>. They travel together with it instead,
/// because the mappers and the log both need them.
/// </remarks>
internal readonly struct JevExchange
{
    internal JevExchange(JevResponse response, string? requestId, int statusCode)
    {
        Response = response;
        RequestId = requestId;
        StatusCode = statusCode;
    }

    internal JevResponse Response { get; }

    internal string? RequestId { get; }

    internal int StatusCode { get; }
}
