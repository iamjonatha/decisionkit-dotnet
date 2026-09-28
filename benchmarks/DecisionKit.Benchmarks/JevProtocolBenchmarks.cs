using BenchmarkDotNet.Attributes;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Questions;
using DecisionKit.Jev.Serialization;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;

namespace DecisionKit.Benchmarks;

/// <summary>
/// The JEV protocol layer: domain to wire, wire to JSON, and back.
/// </summary>
/// <remarks>
/// The transport is excluded on purpose. A network round trip is measured in milliseconds and would
/// hide everything these numbers are meant to expose.
/// </remarks>
[MemoryDiagnoser]
public class JevProtocolBenchmarks
{
    private const string ResponseJson = """
        {
          "model": "jev-latest",
          "usage": { "input_tokens": 214, "output_tokens": 48 },
          "answers": {
            "routing": {
              "type": "choice",
              "choice": "Billing",
              "confidence": 0.82,
              "probabilities": { "Billing": 0.82, "Support": 0.12, "Security": 0.06 }
            },
            "frustration": { "type": "score", "score": 4, "confidence": 0.71 }
          }
        }
        """;

    private readonly RequestMapper _requestMapper = RequestMapper.Default;
    private readonly ResponseMapper _responseMapper = ResponseMapper.Default;

    private DecisionRequest _request = null!;
    private JevRequest _wireRequest = null!;
    private JevResponse _wireResponse = null!;
    private string _requestJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        ChoiceQuestion<Department> routing = new(
            new QuestionId("routing"),
            "Which team should handle this ticket?",
            [Department.Billing, Department.Support, Department.Security]);

        JevScoreQuestion frustration = new(
            new QuestionId("frustration"),
            "How frustrated is the customer?",
            [
                "Calm and cooperative.",
                "Mildly annoyed.",
                "Clearly irritated.",
                "Angry and threatening to leave.",
                "Already announced they are cancelling.",
            ]);

        _request = new DecisionRequest(QuestionSet.Create(routing, frustration))
        {
            Input = DecisionInput.FromText("I have been charged twice for the same month and nobody replied."),
        };

        _wireRequest = _requestMapper.ToWire(_request);
        _requestJson = JevJsonSerialization.Serialize(_wireRequest);
        _wireResponse = JevJsonSerialization.DeserializeResponse(ResponseJson);
    }

    /// <summary>
    /// Projecting the domain request onto the JEV wire shape.
    /// </summary>
    [Benchmark]
    public JevRequest MapRequestToWire() => _requestMapper.ToWire(_request);

    /// <summary>
    /// Serializing the wire request through the source-generated context.
    /// </summary>
    [Benchmark]
    public string SerializeRequest() => JevJsonSerialization.Serialize(_wireRequest);

    /// <summary>
    /// Deserializing a response through the source-generated context.
    /// </summary>
    [Benchmark]
    public JevResponse DeserializeResponse() => JevJsonSerialization.DeserializeResponse(ResponseJson);

    /// <summary>
    /// Projecting the wire response back onto typed answers.
    /// </summary>
    [Benchmark]
    public DecisionResult MapResponseToDomain() => _responseMapper.ToDomain(_request, _wireResponse);

    /// <summary>
    /// Everything a provider does around a single call, excluding the call itself.
    /// </summary>
    [Benchmark]
    public DecisionResult FullRoundTrip()
    {
        JevRequest wire = _requestMapper.ToWire(_request);
        _ = JevJsonSerialization.Serialize(wire);

        JevResponse response = JevJsonSerialization.DeserializeResponse(ResponseJson);

        return _responseMapper.ToDomain(_request, response);
    }

    /// <summary>
    /// Deserializing the request, which only a server or a fixture-driven test does.
    /// </summary>
    [Benchmark]
    public JevRequest DeserializeRequest() => JevJsonSerialization.DeserializeRequest(_requestJson);
}
