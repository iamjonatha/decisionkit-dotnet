using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using DecisionKit.Jev.Tests.Fixtures;
using Xunit;

namespace DecisionKit.Jev.Tests.Serialization;

public sealed class JevJsonSerializationTests
{
    [Fact]
    public void Serialize_NamesEveryRequestMemberTheWayTheServiceExpects()
    {
        string json = JevJsonSerialization.Serialize(RequestMapper.Default.ToWire(JevScenario.CreateRequest()));

        JsonObject request = JsonNode.Parse(json)!.AsObject();

        Assert.True(request.ContainsKey("state"));
        Assert.True(request.ContainsKey("model"));
        Assert.True(request.ContainsKey("questions"));
        Assert.Equal(3, request.Count);
    }

    [Fact]
    public void Serialize_NeverSendsTheRetiredDocumentMember()
    {
        string json = JevJsonSerialization.Serialize(RequestMapper.Default.ToWire(JevScenario.CreateRequest()));

        Assert.DoesNotContain("\"document\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_OmitsCriteriaThatWereNotSupplied()
    {
        JevQuestion question = QuestionMapper.Default.ToWire(JevScenario.UrgentQuestion);

        string json = JsonSerializer.Serialize(question, JevJsonSerialization.QuestionTypeInfo);

        Assert.DoesNotContain("criteria", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ReadsTheRecordedResponse()
    {
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        Assert.Equal("jev-1.13.0", response.Model);
        Assert.Equal(4, response.Answers.Count);
        Assert.Equal(392, response.Usage!.InputTokens);
    }

    [Fact]
    public void Deserialize_KeepsMembersItDoesNotModelOnTheAnswer()
    {
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        JevAnswer tone = response.Answers["tone"];

        Assert.NotNull(tone.Extensions);
        Assert.Equal("negative", tone.Extensions!["label"].GetString());
    }

    [Fact]
    public void Deserialize_ReadsAScoreLegendAndDistribution()
    {
        JevResponse response = JevJsonSerialization.DeserializeResponse(RecordedPayload.Read("response.json"));

        JevAnswer frustration = response.Answers["frustration"];

        Assert.Equal("Frustrated but civil", frustration.Legend!["1"]!.GetValue<string>());
        Assert.Equal(0.54, frustration.Probabilities!["2"]!.GetValue<double>());
    }

    [Fact]
    public void Deserialize_ReadsABareStringDetail()
    {
        JevErrorResponse error = JevJsonSerialization.DeserializeError(RecordedPayload.Read("error-framework.json"));

        JsonValue detail = Assert.IsAssignableFrom<JsonValue>(error.Detail);
        Assert.Equal("Not Found", detail.GetValue<string>());
    }

    [Fact]
    public void Deserialize_ReadsAnObjectDetail()
    {
        JevErrorResponse error = JevJsonSerialization.DeserializeError(RecordedPayload.Read("error.json"));

        JsonObject detail = Assert.IsAssignableFrom<JsonObject>(error.Detail);
        Assert.Equal("invalid_request_error", detail["error_type"]!.GetValue<string>());
    }

    [Fact]
    public void Deserialize_ReportsMalformedJsonAsASerializationFailure()
    {
        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => JevJsonSerialization.DeserializeResponse("{\"answers\":"));

        Assert.Equal(DecisionErrorCategory.Serialization, exception.Category);
        Assert.Equal(JevProtocol.PayloadUnreadableCode, exception.Error.Code);
    }

    [Fact]
    public void Deserialize_ReportsTheJsonLiteralNullAsAFailure()
    {
        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => JevJsonSerialization.DeserializeResponse("null"));

        Assert.Equal(DecisionErrorCategory.Serialization, exception.Category);
    }

    [Fact]
    public void Deserialize_ReportsWhereThePayloadWentWrong()
    {
        DecisionException exception = Assert.ThrowsAny<DecisionException>(
            () => JevJsonSerialization.DeserializeResponse("{\"answers\":"));

        Assert.Contains("json_line", exception.Error.Properties);
    }

    [Fact]
    public void Deserialize_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => JevJsonSerialization.DeserializeResponse(null!));
    }

    [Fact]
    public void RoundTrip_PreservesEveryMemberOfTheRecordedResponse()
    {
        string original = RecordedPayload.Read("response.json");

        JevResponse response = JevJsonSerialization.DeserializeResponse(original);
        string written = JevJsonSerialization.Serialize(response);
        JevResponse again = JevJsonSerialization.DeserializeResponse(written);

        Assert.Equal(response.Answers.Count, again.Answers.Count);
        Assert.Equal(response.Model, again.Model);
        Assert.Equal("negative", again.Answers["tone"].Extensions!["label"].GetString());
        Assert.Equal(0.93, again.Answers["department"].Probabilities!["Billing"]!.GetValue<double>());
    }
}
