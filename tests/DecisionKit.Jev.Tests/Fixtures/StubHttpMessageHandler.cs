using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// One request as the stub handler saw it, captured before the response is produced.
/// </summary>
/// <remarks>
/// The body is read here rather than by the test, because the content is disposed with the request
/// message as soon as the call returns.
/// </remarks>
public sealed class StubHttpRequest
{
    private StubHttpRequest(HttpRequestMessage request, string body)
    {
        Method = request.Method;
        Uri = request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;
        ContentType = request.Content?.Headers.ContentType?.MediaType;
        Body = body;
    }

    public HttpMethod Method { get; }

    public Uri Uri { get; }

    public string? AuthorizationScheme { get; }

    public string? AuthorizationParameter { get; }

    public string? ContentType { get; }

    public string Body { get; }

    internal static async Task<StubHttpRequest> CaptureAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new StubHttpRequest(request, body);
    }
}

/// <summary>
/// A handler that answers from a delegate and records what it was asked.
/// </summary>
/// <remarks>
/// Every transport test runs against this rather than a network. It records requests in a
/// concurrent queue so that the concurrency test can use one instance from many calls at once.
/// </remarks>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<StubHttpRequest, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly ConcurrentQueue<StubHttpRequest> _requests = new();

    public StubHttpMessageHandler(Func<StubHttpRequest, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public IReadOnlyCollection<StubHttpRequest> Requests => _requests;

    public StubHttpRequest LastRequest =>
        _requests.IsEmpty ? throw new InvalidOperationException("No request was sent.") : LastOf(_requests);

    /// <summary>
    /// Answers every call with the same status and body.
    /// </summary>
    public static StubHttpMessageHandler Always(HttpStatusCode status, string body, params (string Name, string Value)[] headers) =>
        new((_, _) => Task.FromResult(Respond(status, body, headers)));

    /// <summary>
    /// Fails every call the way an unreachable service does.
    /// </summary>
    public static StubHttpMessageHandler Unreachable() =>
        new((_, _) => throw new HttpRequestException("The name could not be resolved."));

    public static HttpResponseMessage Respond(HttpStatusCode status, string body, params (string Name, string Value)[] headers)
    {
        HttpResponseMessage response = new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        foreach ((string name, string value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        StubHttpRequest recorded = await StubHttpRequest.CaptureAsync(request, cancellationToken).ConfigureAwait(false);

        _requests.Enqueue(recorded);

        return await _responder(recorded, cancellationToken).ConfigureAwait(false);
    }

    private static StubHttpRequest LastOf(ConcurrentQueue<StubHttpRequest> requests)
    {
        StubHttpRequest? last = null;

        foreach (StubHttpRequest request in requests)
        {
            last = request;
        }

        return last ?? throw new InvalidOperationException("No request was sent.");
    }
}
