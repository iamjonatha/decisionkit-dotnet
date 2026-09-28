using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Extensions.Tests.Fixtures;

/// <summary>
/// One call as the stub saw it.
/// </summary>
public sealed class CapturedCall
{
    internal CapturedCall(HttpRequestMessage request)
    {
        Uri = request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        ApiKey = request.Headers.Authorization?.Parameter;
    }

    public Uri Uri { get; }

    public string? AuthorizationScheme { get; }

    public string? ApiKey { get; }
}

/// <summary>
/// A handler that answers every call with a valid JEV response and records what it was asked.
/// </summary>
/// <remarks>
/// The registration tests care about what reached the network, not about what came back, so the
/// response is fixed and the interesting half is the recording.
/// </remarks>
public sealed class JevStub : HttpMessageHandler
{
    public const string ResponseBody = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "is_urgent": { "type": "noul", "noul": 0.97 }
          }
        }
        """;

    private readonly ConcurrentQueue<CapturedCall> _calls = new();

    public IReadOnlyCollection<CapturedCall> Calls => _calls;

    public CapturedCall LastCall
    {
        get
        {
            CapturedCall? last = null;

            foreach (CapturedCall call in _calls)
            {
                last = call;
            }

            return last ?? throw new InvalidOperationException("No call was made.");
        }
    }

    /// <summary>
    /// Builds the decision every registration test asks about.
    /// </summary>
    public static DecisionRequest CreateRequest() =>
        new(new QuestionSet([new ProbabilityQuestion(new QuestionId("is_urgent"), "Whether the ticket needs attention today")]))
        {
            Input = DecisionInput.FromText("I was charged twice for the same order."),
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _calls.Enqueue(new CapturedCall(request));

        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json"),
        };

        return Task.FromResult(response);
    }
}
