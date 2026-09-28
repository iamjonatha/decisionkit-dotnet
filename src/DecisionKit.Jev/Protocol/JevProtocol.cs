namespace DecisionKit.Jev.Protocol;

/// <summary>
/// The fixed vocabulary of the JEV protocol mapping.
/// </summary>
/// <remarks>
/// Every string DecisionKit itself puts on the wire, reads from it, or attaches to a failure it
/// raises while mapping, is declared here. A caller that alerts on an error code, or reads a
/// provider-specific metadata value, should read it from this type rather than copy the literal.
/// </remarks>
public static class JevProtocol
{
    /// <summary>
    /// The provider name reported in <see cref="DecisionKit.Results.DecisionMetadata.ProviderName"/>
    /// and in every failure this package raises.
    /// </summary>
    public const string ProviderName = "jev";

    /// <summary>
    /// The model requested when the caller names none.
    /// </summary>
    /// <remarks>
    /// This is an alias. The response reports the concrete version it resolved to, which can change
    /// over time.
    /// </remarks>
    public const string DefaultModel = "jev-latest";

    /// <summary>
    /// The address of the public JEV service, used when the caller configures none.
    /// </summary>
    public const string DefaultBaseAddress = "https://api.typesafe.ai";

    /// <summary>
    /// The path segment that carries the protocol version.
    /// </summary>
    /// <remarks>
    /// The version is part of the path rather than a header, so DecisionKit appends it to the
    /// configured address. A configured address that already contains it would produce a duplicate
    /// and is rejected.
    /// </remarks>
    public const string VersionSegment = "v1";

    /// <summary>
    /// The path of the evaluation endpoint, relative to the configured address.
    /// </summary>
    public const string EvaluatePath = "v1/systemone";

    /// <summary>
    /// The path of the model listing endpoint, relative to the configured address.
    /// </summary>
    public const string ModelsPath = "v1/models";

    /// <summary>
    /// The authentication scheme the service expects on the <c>Authorization</c> header.
    /// </summary>
    public const string AuthenticationScheme = "Bearer";

    /// <summary>
    /// The response header that carries the identifier the service assigned the call.
    /// </summary>
    /// <remarks>
    /// The identifier is not in the response body, so it reaches the caller only because the
    /// transport reads this header and hands it to the mappers.
    /// </remarks>
    public const string RequestIdHeader = "x-typesafe-request-id";

    /// <summary>
    /// The response header that carries a retry delay in milliseconds.
    /// </summary>
    /// <remarks>
    /// When both this and the standard <c>Retry-After</c> header are present, this one wins,
    /// because it is the more precise of the two.
    /// </remarks>
    public const string RetryAfterMillisecondsHeader = "retry-after-ms";

    /// <summary>
    /// The smallest number of levels a score rubric may have.
    /// </summary>
    public const int MinimumScoreLevels = 2;

    /// <summary>
    /// The largest number of levels a score rubric may have.
    /// </summary>
    public const int MaximumScoreLevels = 10;

    /// <summary>
    /// The largest number of options a choice question may offer.
    /// </summary>
    public const int MaximumChoiceOptions = 255;

    /// <summary>
    /// The answer metadata key that carries how sure the model was, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Confidence has no place in the provider-neutral domain, because not every provider reports
    /// one and the ones that do compute it differently. It travels as provider-specific metadata,
    /// and <see cref="JevAnswers.TryGetConfidence"/> reads it back.
    /// </remarks>
    public const string ConfidenceMetadataKey = "jev.confidence";

    /// <summary>
    /// The answer metadata key that carries the rubric a score answer was measured against, as a
    /// dictionary keyed by level index.
    /// </summary>
    public const string LegendMetadataKey = "jev.legend";

    /// <summary>
    /// The answer metadata key that carries the full distribution behind a choice or score answer.
    /// </summary>
    /// <remarks>
    /// A choice distribution also reaches the domain as
    /// <see cref="DecisionKit.Values.Choice{TOption}.Distribution"/>, which is where typed code
    /// should read it. This key exists for a score answer, whose per-level distribution the domain
    /// does not model.
    /// </remarks>
    public const string ProbabilitiesMetadataKey = "jev.probabilities";

    /// <summary>
    /// The answer metadata key that carries the exact JSON an answer was read from, when
    /// <see cref="JevMappingOptions.PreserveRawPayloads"/> is on.
    /// </summary>
    public const string RawPayloadMetadataKey = "jev.raw";

    /// <summary>
    /// The error property that carries the service's own error type, which is undocumented and
    /// open-ended.
    /// </summary>
    public const string ErrorTypeProperty = "jev.error_type";

    /// <summary>
    /// The error property that carries the HTTP status code the failure was classified from.
    /// </summary>
    public const string StatusCodeProperty = "jev.status_code";

    /// <summary>
    /// The provider type reported for an unrecognized answer that arrived without a type name.
    /// </summary>
    public const string UnnamedAnswerType = "unnamed";

    /// <summary>
    /// The error code reported when a JEV payload cannot be read as JSON.
    /// </summary>
    public const string PayloadUnreadableCode = "jev_payload_unreadable";

    /// <summary>
    /// The error code reported when a metadata value has no JSON representation.
    /// </summary>
    public const string UnrepresentableMetadataCode = "jev_metadata_unrepresentable";

    /// <summary>
    /// The error code reported when a question type cannot be expressed in the JEV protocol.
    /// </summary>
    public const string UnsupportedQuestionCode = "jev_question_unsupported";

    /// <summary>
    /// The error code reported when a request option has no JEV equivalent and cannot be honoured.
    /// </summary>
    public const string UnsupportedOptionCode = "jev_option_unsupported";

    /// <summary>
    /// The error code reported when the raw definition of an unknown question is not valid JSON.
    /// </summary>
    public const string InvalidQuestionDefinitionCode = "jev_question_definition_invalid";

    /// <summary>
    /// The error code reported when two options of a choice question project onto the same wire
    /// value, which would make the answer impossible to resolve.
    /// </summary>
    public const string AmbiguousOptionsCode = "jev_choice_options_ambiguous";

    /// <summary>
    /// The error code reported when a choice question offers more options than the service accepts.
    /// </summary>
    public const string TooManyOptionsCode = "jev_choice_options_too_many";

    /// <summary>
    /// The error code reported when an answer cannot be mapped onto the question that asked for it.
    /// </summary>
    public const string UnmappableAnswerCode = "jev_answer_unmappable";

    /// <summary>
    /// The error code reported when a request carries no input, which the service requires.
    /// </summary>
    public const string InputRequiredCode = "jev_state_required";

    /// <summary>
    /// The error code reported when an input carries both text and a property already named
    /// <see cref="StateTextKey"/>, so the text would silently replace the property.
    /// </summary>
    public const string AmbiguousInputCode = "jev_state_ambiguous";

    /// <summary>
    /// The error code reported when the credential provider offers no key, so the request would
    /// travel unauthenticated.
    /// </summary>
    public const string CredentialMissingCode = "jev_credential_missing";

    /// <summary>
    /// The error code reported when a call runs out of time before the service answers.
    /// </summary>
    public const string TimeoutCode = "jev_timeout";

    /// <summary>
    /// The error code reported when the service could not be reached at all.
    /// </summary>
    public const string TransportFailureCode = "jev_transport_failure";

    /// <summary>
    /// The error property that names which budget a timed-out call exhausted, either
    /// <c>attempt</c> or <c>operation</c>.
    /// </summary>
    public const string TimeoutScopeProperty = "jev.timeout_scope";

    /// <summary>
    /// The error property that carries how many attempts a retried call made before it gave up.
    /// </summary>
    /// <remarks>
    /// Present only when the call was actually repeated, so its absence means the failure is the
    /// first and only attempt rather than the last of several.
    /// </remarks>
    public const string RetryAttemptsProperty = "jev.retry_attempts";

    /// <summary>
    /// The member of the state object that carries
    /// <see cref="DecisionKit.Providers.DecisionInput.Text"/> when the input also carries
    /// properties.
    /// </summary>
    /// <remarks>
    /// The service accepts any JSON value as state and imposes no schema on it, so this name is
    /// DecisionKit's convention rather than the protocol's. Text-only input travels as a bare JSON
    /// string and never uses it.
    /// </remarks>
    public const string StateTextKey = "text";
}
