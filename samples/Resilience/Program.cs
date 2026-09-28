using System;
using System.Collections.Generic;
using System.Globalization;
using DecisionKit.Errors;
using DecisionKit.Identifiers;
using DecisionKit.Jev.Client;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Resilience;

// Retrying is off by default, and that is a deliberate decision rather than an oversight. JEV has
// no idempotency mechanism, so a repeated evaluation is a second evaluation: it is billed again and
// it may answer differently. Only the application knows whether that is acceptable.
Console.WriteLine($"Default policy enabled: {JevProviderOptions.Default.Retry.IsEnabled}");
Console.WriteLine($"Standard policy enabled: {JevRetryPolicy.Standard.IsEnabled} ({JevRetryPolicy.Standard.MaxAttempts} attempts)");
Console.WriteLine();

// A section named "Retry" in configuration does not switch retrying on by itself. MaxAttempts is
// what switches it on, because one attempt is not a retry.
JevRetryPolicy policy = new()
{
    MaxAttempts = 4,
    InitialDelay = TimeSpan.FromMilliseconds(500),
    BackoffFactor = 2,
    MaxDelay = TimeSpan.FromSeconds(5),

    // Jitter is set to zero here only so that this sample prints the same numbers every run.
    // Leave it at its default in production: it is what stops every client in a fleet from
    // retrying in lockstep after the same outage.
    Jitter = 0,

    RespectRetryAfter = true,
};

Console.WriteLine("Exponential backoff, capped:");

for (int completedAttempts = 1; completedAttempts < policy.MaxAttempts; completedAttempts++)
{
    Console.WriteLine($"  after attempt {completedAttempts}: {policy.GetDelay(completedAttempts, retryAfter: null)}");
}

Console.WriteLine();

// A server that says how long to wait wins over the computed backoff, because the server knows
// something the client does not.
Console.WriteLine($"Server asked for 3s, policy computed 500ms -> {policy.GetDelay(1, TimeSpan.FromSeconds(3))}");
Console.WriteLine();

// Classification decides whether an attempt is repeatable at all. The error mapper has already
// judged the failure and recorded a retry hint on the error; the policy's status code lists are a
// deployment override on top of that, and they are empty unless an operator fills them in.
Console.WriteLine("Classification, as the transport reported it:");
Report(policy, 429, DecisionErrorCategory.RateLimit, DecisionRetryHint.After(TimeSpan.FromSeconds(2)));
Report(policy, 503, DecisionErrorCategory.Transport, DecisionRetryHint.Retryable);
Report(policy, 401, DecisionErrorCategory.Authentication, DecisionRetryHint.NotRetryable);
Report(policy, 400, DecisionErrorCategory.Validation, DecisionRetryHint.NotRetryable);

Console.WriteLine();

// A deployment sitting behind a gateway that returns 429 for something permanent can overrule the
// mapper without patching it. The non-retryable list is checked first, so listing a code in both
// refuses it.
JevRetryPolicy behindAGateway = new()
{
    MaxAttempts = 4,
    NonRetryableStatusCodes = new HashSet<int> { 429 },
};

Console.WriteLine("The same failures, behind a gateway that misreports 429:");
Report(behindAGateway, 429, DecisionErrorCategory.RateLimit, DecisionRetryHint.After(TimeSpan.FromSeconds(2)));
Report(behindAGateway, 503, DecisionErrorCategory.Transport, DecisionRetryHint.Retryable);

Console.WriteLine();

// The policy refuses to pretend. An idempotency key is only honest when the service deduplicates
// on it, and JEV does not, so anything other than Disabled is rejected at the point of setting it
// instead of quietly doing nothing at run time.
Console.WriteLine($"Idempotency: {policy.Idempotency}");

try
{
    _ = new JevRetryPolicy { Idempotency = DecisionIdempotencyPolicy.ExplicitOnly };
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"  Refused: {ex.Message.Split('.')[0]}.");
}

Console.WriteLine();
Console.WriteLine("Wire this policy to a provider with JevProviderOptions.Retry, or to configuration:");
Console.WriteLine("""
  { "Jev": { "Retry": { "MaxAttempts": 4, "InitialDelay": "00:00:00.500", "MaxDelay": "00:00:05" } } }
  """);

static void Report(JevRetryPolicy policy, int statusCode, DecisionErrorCategory category, DecisionRetryHint hint)
{
    DecisionError error = new(category, "Reported by the service.")
    {
        ProviderName = JevProtocol.ProviderName,
        Properties = new Dictionary<string, object?> { [JevProtocol.StatusCodeProperty] = statusCode },
        Retry = hint,
    };

    string status = statusCode.ToString(CultureInfo.InvariantCulture);

    Console.WriteLine($"  {status} {category,-15} hint {hint.Retryability,-12} -> {policy.Classify(error)}");
}
