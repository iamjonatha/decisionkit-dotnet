using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Samples.Basic;

using CancellationTokenSource applicationStopping = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    applicationStopping.Cancel();
};

CancellationToken cancellationToken = applicationStopping.Token;

await MinimalDecision.RunAsync(cancellationToken);
await ChoiceQuestions.RunAsync(cancellationToken);
await ScoreQuestions.RunAsync(cancellationToken);
await MultipleQuestions.RunAsync(cancellationToken);
await UnknownQuestionTypes.RunAsync(cancellationToken);
await ErrorHandling.RunAsync(cancellationToken);
await Cancellation.RunAsync();
