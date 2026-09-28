using System;
using DecisionKit.Errors;
using DecisionKit.Jev.Models;
using DecisionKit.Jev.Protocol;
using DecisionKit.Jev.Serialization;
using DecisionKit.Jev.Tests.Fixtures;
using Xunit;

namespace DecisionKit.Jev.Tests.Protocol;

public sealed class ErrorMapperTests
{
    [Theory]
    [InlineData(400, DecisionErrorCategory.Validation)]
    [InlineData(401, DecisionErrorCategory.Authentication)]
    [InlineData(403, DecisionErrorCategory.Authorization)]
    [InlineData(404, DecisionErrorCategory.Validation)]
    [InlineData(408, DecisionErrorCategory.Timeout)]
    [InlineData(422, DecisionErrorCategory.Validation)]
    [InlineData(429, DecisionErrorCategory.RateLimit)]
    [InlineData(500, DecisionErrorCategory.ProviderError)]
    [InlineData(503, DecisionErrorCategory.ProviderError)]
    [InlineData(529, DecisionErrorCategory.ProviderError)]
    public void ToDomain_TakesTheCategoryFromTheStatusCode(int statusCode, DecisionErrorCategory expected)
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(statusCode));

        Assert.Equal(expected, error.Category);
    }

    [Fact]
    public void ToDomain_ReportsATransportFailureWhenNoStatusArrived()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, JevErrorContext.None);

        Assert.Equal(DecisionErrorCategory.Transport, error.Category);
    }

    [Fact]
    public void ToDomain_ReadsTheMessageAndTypeOfAnApplicationError()
    {
        JevErrorResponse body = JevJsonSerialization.DeserializeError(RecordedPayload.Read("error.json"));

        DecisionError error = ErrorMapper.Default.ToDomain(body, new JevErrorContext(422));

        Assert.Equal("invalid_request_error", error.Code);
        Assert.Contains("choice questions require", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_ReportsTheErrorTypeAsAPropertyToo()
    {
        JevErrorResponse body = JevJsonSerialization.DeserializeError(RecordedPayload.Read("error.json"));

        DecisionError error = ErrorMapper.Default.ToDomain(body, new JevErrorContext(422));

        Assert.Equal("invalid_request_error", Assert.Contains(JevProtocol.ErrorTypeProperty, error.Properties));
    }

    [Fact]
    public void ToDomain_ReadsAFrameworkErrorReportedAsABareString()
    {
        JevErrorResponse body = JevJsonSerialization.DeserializeError(RecordedPayload.Read("error-framework.json"));

        DecisionError error = ErrorMapper.Default.ToDomain(body, new JevErrorContext(404));

        Assert.Equal("Not Found", error.Message);
        Assert.Null(error.Code);
    }

    [Fact]
    public void ToDomain_DescribesAFailureWhoseBodySaidNothing()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(new JevErrorResponse(), new JevErrorContext(500));

        Assert.Contains("500", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_SaysWhatAnOverloadedServiceMeans()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(529));

        Assert.Contains("overloaded", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDomain_RecordsTheStatusCodeAsAProperty()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(429));

        Assert.Equal(429, Assert.Contains(JevProtocol.StatusCodeProperty, error.Properties));
    }

    [Fact]
    public void ToDomain_PrefersTheDelayTheServiceAskedFor()
    {
        JevErrorContext context = new(429, TimeSpan.FromSeconds(12), null);

        DecisionError error = ErrorMapper.Default.ToDomain(null, context);

        Assert.Equal(DecisionRetryability.Retryable, error.Retry.Retryability);
        Assert.Equal(TimeSpan.FromSeconds(12), error.Retry.RetryAfter);
    }

    [Fact]
    public void ToDomain_MarksAnOverloadedServiceAsWorthRetrying()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(529));

        Assert.Equal(DecisionRetryability.Retryable, error.Retry.Retryability);
    }

    [Fact]
    public void ToDomain_MarksARejectedRequestAsNotWorthRetrying()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(422));

        Assert.Equal(DecisionRetryability.NotRetryable, error.Retry.Retryability);
    }

    [Fact]
    public void ToDomain_SaysNothingAboutRetryingAnUnexplainedServerError()
    {
        DecisionError error = ErrorMapper.Default.ToDomain(null, new JevErrorContext(500));

        Assert.Equal(DecisionRetryability.Unknown, error.Retry.Retryability);
    }

    [Fact]
    public void ToDomain_KeepsTheIdentifierTheServiceAssigned()
    {
        JevErrorContext context = new(500, null, "req_01HZX");

        DecisionError error = ErrorMapper.Default.ToDomain(null, context);

        Assert.Equal("req_01HZX", error.ProviderRequestId);
    }

    [Fact]
    public void ToException_ReportsTheCategoryThroughTheExceptionType()
    {
        DecisionException exception = ErrorMapper.Default.ToException(null, new JevErrorContext(401));

        Assert.IsType<DecisionAuthenticationException>(exception);
    }

    [Fact]
    public void JevErrorContext_RejectsANegativeDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JevErrorContext(429, TimeSpan.FromSeconds(-1), null));
    }
}
