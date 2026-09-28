using System;
using DecisionKit.Errors;

namespace DecisionKit.Core.Tests.Errors;

public sealed class DecisionExceptionTests
{
    [Fact]
    public void FromError_MapsTheCategoryOntoTheReactionTheCallerCanHave()
    {
        Assert.IsType<DecisionValidationException>(FromCategory(DecisionErrorCategory.Validation));
        Assert.IsType<DecisionAuthenticationException>(FromCategory(DecisionErrorCategory.Authentication));
        Assert.IsType<DecisionAuthenticationException>(FromCategory(DecisionErrorCategory.Authorization));
        Assert.IsType<DecisionTransientException>(FromCategory(DecisionErrorCategory.RateLimit));
        Assert.IsType<DecisionTransientException>(FromCategory(DecisionErrorCategory.Timeout));
        Assert.IsType<DecisionTransientException>(FromCategory(DecisionErrorCategory.Transport));
        Assert.IsType<DecisionProviderException>(FromCategory(DecisionErrorCategory.Serialization));
        Assert.IsType<DecisionProviderException>(FromCategory(DecisionErrorCategory.ProviderError));
        Assert.IsType<DecisionProviderException>(FromCategory(DecisionErrorCategory.UnknownResponse));
        Assert.IsType<DecisionProviderException>(FromCategory(DecisionErrorCategory.Unknown));
    }

    [Fact]
    public void FromError_KeepsTheOriginalCategoryRatherThanTheOneTheExceptionSuggests()
    {
        DecisionException exception = FromCategory(DecisionErrorCategory.Authorization);

        Assert.Equal(DecisionErrorCategory.Authorization, exception.Category);
    }

    [Fact]
    public void FromError_RefusesToWrapACancellation()
    {
        DecisionError error = new(DecisionErrorCategory.Canceled, "The caller cancelled the call.");

        Assert.Throws<ArgumentException>(() => DecisionException.FromError(error));
    }

    [Fact]
    public void FromError_RejectsANullError()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionException.FromError(null!));
    }

    [Fact]
    public void FromError_PreservesTheStructuredErrorAndTheInnerException()
    {
        InvalidOperationException cause = new("socket closed");
        DecisionError error = new(DecisionErrorCategory.Transport, "The provider is unreachable.")
        {
            Code = "connection_reset",
            Retry = DecisionRetryHint.Retryable,
        };

        DecisionException exception = DecisionException.FromError(error, cause);

        Assert.Same(error, exception.Error);
        Assert.Same(cause, exception.InnerException);
        Assert.Equal("The provider is unreachable.", exception.Message);
        Assert.Equal("connection_reset", exception.Error.Code);
        Assert.Equal(DecisionRetryability.Retryable, exception.Retry.Retryability);
    }

    [Fact]
    public void AMessageOnlyExceptionStillCarriesAStructuredError()
    {
        DecisionValidationException exception = new("Question 'q' is unknown.");

        Assert.Equal(DecisionErrorCategory.Validation, exception.Category);
        Assert.Equal("Question 'q' is unknown.", exception.Error.Message);
    }

    [Fact]
    public void EveryDecisionExceptionSharesOneBaseType()
    {
        Assert.IsAssignableFrom<DecisionException>(new DecisionValidationException());
        Assert.IsAssignableFrom<DecisionException>(new DecisionAuthenticationException());
        Assert.IsAssignableFrom<DecisionException>(new DecisionTransientException());
        Assert.IsAssignableFrom<DecisionException>(new DecisionProviderException());
    }

    private static DecisionException FromCategory(DecisionErrorCategory category) =>
        DecisionException.FromError(new DecisionError(category, "Something went wrong."));
}
