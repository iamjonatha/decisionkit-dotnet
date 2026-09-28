using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using DecisionKit.Jev.Tests.Fixtures;
using DecisionKit.Results;
using Xunit;

namespace DecisionKit.Jev.Tests.Contracts;

/// <summary>
/// Pins the payloads against the published System One reference.
/// </summary>
/// <remarks>
/// <para>
/// The payloads under <c>Payloads</c> follow the request and response documented for
/// <c>POST /v1/systemone</c>. A test that builds the JSON it then reads can only prove the code
/// agrees with itself, so what the mapper produces is compared against a recorded file instead, and
/// a recorded file is read back into the domain.
/// </para>
/// <para>
/// A failure here means the wire contract moved. The fix is to re-record the payload against the
/// reference, never to relax the assertion.
/// </para>
/// </remarks>
public sealed class JevWireContractTests
{
    [Fact]
    public void Request_MatchesTheRecordedPayload()
    {
        JsonObject produced = Produce();
        JsonObject recorded = JsonNode.Parse(RecordedPayload.Read("request.json"))!.AsObject();

        AssertSame(recorded, produced, "$");
    }

    [Fact]
    public void Request_CarriesOnlyTheThreeMembersTheServiceDefines()
    {
        JsonObject produced = Produce();

        Assert.Equal(["state", "model", "questions"], Keys(produced));
    }

    [Fact]
    public void Request_DescribesEachQuestionWithATypeAndInstructions()
    {
        JsonObject questions = Produce()["questions"]!.AsObject();

        foreach (KeyValuePair<string, JsonNode?> entry in questions)
        {
            JsonObject question = entry.Value!.AsObject();

            Assert.True(question.ContainsKey("type"), $"Question '{entry.Key}' travels without a type.");
            Assert.True(question.ContainsKey("instructions"), $"Question '{entry.Key}' travels without instructions.");
        }
    }

    [Fact]
    public void Response_ReadsBackIntoTheDomain()
    {
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));
        ResponseMapper mapper = new(AnswerMapper.Default, new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        DecisionResult result = mapper.ToDomain(JevScenario.CreateRequest(), response);

        Assert.Equal(4, result.Count);
        Assert.Equal("jev-1.13.0", result.Metadata.ModelVersion);
    }

    [Fact]
    public void Response_AnswersAreKeyedByTheQuestionNamesTheRequestChose()
    {
        JsonObject produced = Produce();
        JsonObject recorded = JsonNode.Parse(RecordedPayload.Read("response.json"))!.AsObject();

        Assert.Equal(Keys(produced["questions"]!.AsObject()), Keys(recorded["answers"]!.AsObject()));
    }

    private static JsonObject Produce()
    {
        JevRequest wire = RequestMapper.Default.ToWire(JevScenario.CreateRequest());

        return JsonNode.Parse(JevJsonSerialization.Serialize(wire))!.AsObject();
    }

    private static List<string> Keys(JsonObject value)
    {
        List<string> keys = [];

        foreach (KeyValuePair<string, JsonNode?> entry in value)
        {
            keys.Add(entry.Key);
        }

        return keys;
    }

    private static void AssertSame(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is null || actual is null)
        {
            Assert.True(expected is null && actual is null, $"{path} differs: one side is null.");
            return;
        }

        switch (expected)
        {
            case JsonObject expectedObject:
                JsonObject actualObject = Assert.IsAssignableFrom<JsonObject>(actual);
                Assert.Equal(Keys(expectedObject), Keys(actualObject));

                foreach (KeyValuePair<string, JsonNode?> entry in expectedObject)
                {
                    AssertSame(entry.Value, actualObject[entry.Key], $"{path}.{entry.Key}");
                }

                break;

            case JsonArray expectedArray:
                JsonArray actualArray = Assert.IsAssignableFrom<JsonArray>(actual);
                Assert.Equal(expectedArray.Count, actualArray.Count);

                for (int index = 0; index < expectedArray.Count; index++)
                {
                    AssertSame(expectedArray[index], actualArray[index], $"{path}[{index}]");
                }

                break;

            default:
                Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
                break;
        }
    }
}
