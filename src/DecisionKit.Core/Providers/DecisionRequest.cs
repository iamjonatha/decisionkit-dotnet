using System;
using System.Collections.Generic;
using DecisionKit.Identifiers;
using DecisionKit.Internal;
using DecisionKit.Questions;

namespace DecisionKit.Providers;

/// <summary>
/// One decision to evaluate: what it is about, what is being asked, and how the caller wants it
/// answered.
/// </summary>
/// <remarks>
/// <para>
/// A request is provider-neutral and transport-free. It never exposes an
/// <see cref="System.Net.Http.HttpRequestMessage"/>, a URL, a header or a credential, because the
/// same request must be answerable by a second provider, by a fake in a test, and by a decorator
/// that never makes a network call at all.
/// </para>
/// <para>
/// A request carries at least one question. An empty <see cref="QuestionSet"/> is a legitimate
/// intermediate value while questions are being assembled, but sending it asks a provider nothing.
/// </para>
/// </remarks>
public sealed class DecisionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionRequest"/> class.
    /// </summary>
    /// <param name="questions">The questions to ask, in the order they should be sent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="questions"/> is empty.</exception>
    public DecisionRequest(QuestionSet questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        if (questions.Count == 0)
        {
            throw new ArgumentException("A decision request must ask at least one question.", nameof(questions));
        }

        Questions = questions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionRequest"/> class.
    /// </summary>
    /// <param name="questions">The questions to ask, in the order they should be sent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="questions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="questions"/> is empty, contains a <see langword="null"/> entry, or contains
    /// two questions with the same identifier.
    /// </exception>
    public DecisionRequest(IEnumerable<Question> questions)
        : this(new QuestionSet(questions))
    {
    }

    /// <summary>
    /// Gets the questions to ask, in the order they should be sent.
    /// </summary>
    public QuestionSet Questions { get; }

    /// <summary>
    /// Gets what the decision is about. Defaults to <see cref="DecisionInput.Empty"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned input is <see langword="null"/>.</exception>
    public DecisionInput Input
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = DecisionInput.Empty;

    /// <summary>
    /// Gets the provider-neutral options for this request. Defaults to
    /// <see cref="DecisionOptions.Default"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned options are <see langword="null"/>.</exception>
    public DecisionOptions Options
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = DecisionOptions.Default;

    /// <summary>
    /// Gets the identifier the caller generated for this request, so that application logs and
    /// provider logs can be correlated.
    /// </summary>
    /// <exception cref="ArgumentException">The assigned identifier was created through <see langword="default"/>.</exception>
    public RequestId? ClientRequestId
    {
        get;
        init
        {
            if (value is { IsEmpty: true })
            {
                throw new ArgumentException("A client request identifier must be initialized. Pass null instead.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the key that marks this request as safely repeatable, when the caller supplies one.
    /// </summary>
    /// <remarks>
    /// The same key must be reused for every attempt of one logical decision. A provider that does
    /// not support idempotency reports so through
    /// <see cref="DecisionProviderCapabilities.SupportsIdempotencyKeys"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">The assigned key was created through <see langword="default"/>.</exception>
    public IdempotencyKey? IdempotencyKey
    {
        get;
        init
        {
            if (value is { IsEmpty: true })
            {
                throw new ArgumentException("An idempotency key must be initialized. Pass null instead.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the provider-neutral metadata attached to this request.
    /// </summary>
    /// <remarks>
    /// This is the documented place for data a specific provider understands and the domain does
    /// not, so that a provider feature never has to be added to this type. The collection is copied
    /// on assignment.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get;
        init => field = MetadataSnapshot.Create(value);
    } = MetadataSnapshot.Empty;
}
