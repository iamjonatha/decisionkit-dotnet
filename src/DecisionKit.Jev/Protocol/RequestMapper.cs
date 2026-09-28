using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Serialization;
using DecisionKit.Providers;
using DecisionKit.Questions;

namespace DecisionKit.Jev.Protocol;

/// <summary>
/// Translates a domain decision request into the JEV wire request.
/// </summary>
/// <remarks>
/// <para>
/// The mapper decides nothing about transport. It produces the body, and only the body: the
/// endpoint, the headers and the credential are the transport layer's business, which is why this
/// type can be exercised without an HTTP stack.
/// </para>
/// <para>
/// Two request options have no JEV equivalent and are refused rather than dropped, because dropping
/// them would change what the caller asked for without saying so:
/// <see cref="DecisionOptions.Language"/>, which JEV does not accept at all, and
/// <see cref="DecisionRequest.IdempotencyKey"/>, which JEV offers no way to honour.
/// <see cref="DecisionOptions.IncludeExplanations"/> is ignored, as the domain intends for a
/// provider that cannot explain itself — JEV returns no rationale in any form.
/// </para>
/// <para>
/// <see cref="DecisionRequest.ClientRequestId"/> and <see cref="DecisionRequest.Metadata"/> are
/// correlation and annotation rather than instruction, and JEV has nowhere to carry them, so they
/// stay on the request the caller holds and do not reach the wire.
/// </para>
/// </remarks>
public sealed class RequestMapper
{
    private readonly QuestionMapper _questions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMapper"/> class with the default mapping
    /// settings.
    /// </summary>
    public RequestMapper()
        : this(QuestionMapper.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMapper"/> class.
    /// </summary>
    /// <param name="options">The mapping settings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public RequestMapper(JevMappingOptions options)
        : this(new QuestionMapper(options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMapper"/> class over an existing question
    /// mapper.
    /// </summary>
    /// <param name="questionMapper">The mapper that translates each question.</param>
    /// <exception cref="ArgumentNullException"><paramref name="questionMapper"/> is <see langword="null"/>.</exception>
    public RequestMapper(QuestionMapper questionMapper)
    {
        ArgumentNullException.ThrowIfNull(questionMapper);

        _questions = questionMapper;
    }

    /// <summary>
    /// Gets the mapper that uses the default mapping settings.
    /// </summary>
    public static RequestMapper Default { get; } = new();

    /// <summary>
    /// Gets the mapping settings this mapper uses.
    /// </summary>
    public JevMappingOptions Options => _questions.Options;

    /// <summary>
    /// Translates a decision request into its JEV wire form.
    /// </summary>
    /// <param name="request">The request to translate.</param>
    /// <returns>The wire request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionKit.Errors.DecisionException">
    /// The request cannot be expressed in the JEV protocol.
    /// </exception>
    public JevRequest ToWire(DecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequireExpressibleOptions(request);

        return new JevRequest
        {
            State = ToState(request.Input),
            Model = Options.Model,
            Questions = ToWire(request.Questions),
        };
    }

    private void RequireExpressibleOptions(DecisionRequest request)
    {
        if (request.Options.Language is not null)
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.UnsupportedOptionCode,
                "JEV has no way to be asked for a given language, so the requested language could not be honoured. Remove it, or state the language in the question text.");
        }

        if (request.IdempotencyKey is not null)
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.UnsupportedOptionCode,
                "JEV has no idempotency mechanism, so a retried request would be evaluated again rather than deduplicated. Remove the idempotency key, or deduplicate before calling.");
        }
    }

    private JsonNode ToState(DecisionInput input)
    {
        if (input.IsEmpty)
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.InputRequiredCode,
                "JEV evaluates questions against a state and requires one, so a request without input cannot be sent.");
        }

        if (input.Properties.Count == 0)
        {
            return JsonValue.Create(input.Text!);
        }

        JsonObject state = JevJsonValues.ToJsonObject(input.Properties)!;

        if (input.Text is not { } text)
        {
            return state;
        }

        if (state.ContainsKey(JevProtocol.StateTextKey))
        {
            throw JevMappingErrors.CannotSend(
                Options,
                JevProtocol.AmbiguousInputCode,
                $"The input carries both text and a property named '{JevProtocol.StateTextKey}', and the text travels under that name, so one would silently replace the other. Rename the property, or fold the text into it.");
        }

        state[JevProtocol.StateTextKey] = JsonValue.Create(text);

        return state;
    }

    private Dictionary<string, JevQuestion> ToWire(QuestionSet questions)
    {
        Dictionary<string, JevQuestion> wire = new(questions.Count, StringComparer.Ordinal);

        foreach (Question question in questions)
        {
            wire[question.Id.Value] = _questions.ToWire(question);
        }

        return wire;
    }
}
