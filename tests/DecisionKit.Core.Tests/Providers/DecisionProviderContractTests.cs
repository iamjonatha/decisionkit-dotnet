using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Answers;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Values;

namespace DecisionKit.Core.Tests.Providers;

/// <summary>
/// Exercises the Phase 2 exit criterion: a provider can be written against the types in
/// <c>DecisionKit.Core</c> alone, with no protocol, no transport and no extra package.
/// </summary>
public sealed class DecisionProviderContractTests
{
    [Fact]
    public async Task AProviderCanBeImplementedWithCoreTypesAloneAsync()
    {
        IDecisionProvider provider = new ConstantProvider();
        ProbabilityQuestion question = new(new QuestionId("frustration"), "Is the customer frustrated?");

        DecisionResult result = await provider.DecideAsync(
            new DecisionRequest(QuestionSet.Create(question)),
            TestContext.Current.CancellationToken);

        Assert.Equal(0.75, result.Get(question).Value.Value);
        Assert.Equal("constant", result.Metadata.ProviderName);
    }

    [Fact]
    public async Task TheClientRequestIdentifierReachesTheResultMetadataAsync()
    {
        IDecisionProvider provider = new ConstantProvider();
        RequestId clientRequestId = RequestId.New();

        DecisionRequest request = new(QuestionSet.Create(new ProbabilityQuestion(new QuestionId("q"), "Prompt")))
        {
            ClientRequestId = clientRequestId,
        };

        DecisionResult result = await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(clientRequestId, result.Metadata.ClientRequestId);
    }

    [Fact]
    public async Task ACancelledCallThrowsOperationCanceledExceptionRatherThanADecisionExceptionAsync()
    {
        IDecisionProvider provider = new ConstantProvider();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        DecisionRequest request = new(QuestionSet.Create(new ProbabilityQuestion(new QuestionId("q"), "Prompt")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.DecideAsync(request, cancellation.Token));
    }

    [Fact]
    public async Task AnUnsupportedQuestionTypeIsReportedAsAValidationFailureWithItsDiagnosticsAsync()
    {
        IDecisionProvider provider = new ConstantProvider();
        DecisionRequest request = new(QuestionSet.Create(new ScoreQuestion(new QuestionId("q"), "Prompt", 0, 10)));

        DecisionValidationException exception = await Assert.ThrowsAsync<DecisionValidationException>(
            () => provider.DecideAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(DecisionErrorCategory.Validation, exception.Category);
        Assert.Equal("unsupported_question_type", exception.Error.Code);
        Assert.Equal("constant", exception.Error.ProviderName);
        Assert.Equal(DecisionRetryability.NotRetryable, exception.Retry.Retryability);
    }

    [Fact]
    public async Task AProviderRejectsANullRequestAsync()
    {
        IDecisionProvider provider = new ConstantProvider();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.DecideAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AProviderDeclaresWhatItSupports()
    {
        IDecisionProvider provider = new ConstantProvider();

        Assert.True(provider.Capabilities.Supports<ProbabilityQuestion>());
        Assert.False(provider.Capabilities.SupportsIdempotencyKeys);
    }

    private sealed class ConstantProvider : IDecisionProvider
    {
        public string Name => "constant";

        public DecisionProviderCapabilities Capabilities { get; } = new()
        {
            SupportedQuestionTypes = [typeof(ProbabilityQuestion)],
            SupportsUsageReporting = true,
        };

        public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            List<Answer> answers = [];

            foreach (Question question in request.Questions)
            {
                if (!Capabilities.Supports(question.GetType()))
                {
                    throw DecisionException.FromError(new DecisionError(
                        DecisionErrorCategory.Validation,
                        $"This provider cannot answer question '{question.Id}'.")
                    {
                        Code = "unsupported_question_type",
                        ProviderName = Name,
                        Retry = DecisionRetryHint.NotRetryable,
                    });
                }

                answers.Add(new ProbabilityAnswer(question.Id, new Probability(0.75)));
            }

            DecisionMetadata metadata = new(Name, DateTimeOffset.UnixEpoch)
            {
                ClientRequestId = request.ClientRequestId,
            };

            return Task.FromResult(new DecisionResult(answers, metadata));
        }
    }
}
