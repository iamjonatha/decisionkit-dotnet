using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Recording;
using DecisionKit.Values;

namespace DecisionKit.Testing.Assertions;

/// <summary>
/// Assertions about decision results and captured requests, written so that a failure explains
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// These helpers exist because the useful failure message is domain-specific. <c>Assert.Equal(0.8,
/// result.Get(question).Value.Value)</c> reports two doubles; <see cref="HasProbability(DecisionResult,
/// ProbabilityQuestion, double)"/> reports which question was asked, what the provider answered and
/// what the test expected.
/// </para>
/// <para>
/// They complement a test framework rather than replace it. Keep using the framework's assertions
/// for everything else.
/// </para>
/// </remarks>
public static class DecisionAssert
{
    private const double DefaultTolerance = 1e-9;

    /// <summary>
    /// Asserts that a result answered a question, and returns the answer.
    /// </summary>
    /// <typeparam name="TAnswer">The type of answer the question expects.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <returns>The answer, so that a test can assert further on it.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">
    /// The result carries no answer for that question, or carries one of another type.
    /// </exception>
    public static TAnswer Answered<TAnswer>(DecisionResult result, Question<TAnswer> question)
        where TAnswer : Answer
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(question);

        if (!result.TryGet(question.Id, out Answer? answer))
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected an answer for question '{question.Id}', but the result answered {Describe(result)}."));
        }

        if (answer is not TAnswer typed)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Question '{question.Id}' expects an answer of type {typeof(TAnswer)}, but the result carries {answer.GetType()}."));
        }

        return typed;
    }

    /// <summary>
    /// Asserts that a result did not answer a question.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have gone unanswered.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The result carries an answer for that question.</exception>
    public static void NotAnswered(DecisionResult result, Question question)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(question);

        if (result.Contains(question.Id))
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected no answer for question '{question.Id}', but the result carries {result.Get(question.Id).GetType()}."));
        }
    }

    /// <summary>
    /// Asserts the probability a result reports for a question.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <param name="expected">The expected probability.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The answer is missing or reports another probability.</exception>
    public static void HasProbability(DecisionResult result, ProbabilityQuestion question, double expected) =>
        HasProbability(result, question, expected, DefaultTolerance);

    /// <summary>
    /// Asserts the probability a result reports for a question, within a tolerance.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <param name="expected">The expected probability.</param>
    /// <param name="tolerance">The accepted absolute difference.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative.</exception>
    /// <exception cref="DecisionAssertionException">The answer is missing or reports another probability.</exception>
    public static void HasProbability(DecisionResult result, ProbabilityQuestion question, double expected, double tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        double actual = Answered(result, question).Value.Value;

        if (Math.Abs(actual - expected) > tolerance)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Question '{question.Id}' was answered with probability {actual}, but {expected} was expected."));
        }
    }

    /// <summary>
    /// Asserts the score a result reports for a question.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <param name="expected">The expected value on the question's scale.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The answer is missing or reports another score.</exception>
    public static void HasScore(DecisionResult result, ScoreQuestion question, double expected) =>
        HasScore(result, question, expected, DefaultTolerance);

    /// <summary>
    /// Asserts the score a result reports for a question, within a tolerance.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <param name="expected">The expected value on the question's scale.</param>
    /// <param name="tolerance">The accepted absolute difference.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is negative.</exception>
    /// <exception cref="DecisionAssertionException">The answer is missing or reports another score.</exception>
    public static void HasScore(DecisionResult result, ScoreQuestion question, double expected, double tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        Score actual = Answered(result, question).Value;

        if (Math.Abs(actual.Value - expected) > tolerance)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Question '{question.Id}' was answered with score {actual}, but {expected} was expected."));
        }
    }

    /// <summary>
    /// Asserts the option a result selected for a question.
    /// </summary>
    /// <typeparam name="TOption">The type of the options the question offers.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="question">The question that should have been answered.</param>
    /// <param name="expected">The expected option.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">
    /// The answer is missing, carries only a distribution, or selected another option.
    /// </exception>
    public static void HasSelection<TOption>(DecisionResult result, ChoiceQuestion<TOption> question, TOption expected)
        where TOption : notnull
    {
        ArgumentNullException.ThrowIfNull(expected);

        Choice<TOption> choice = Answered(result, question).Value;

        if (!choice.TryGetSelection(out TOption? actual))
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Question '{question.Id}' was answered with a distribution over {choice.Distribution.Count} option(s) and no selection, but the option '{expected}' was expected."));
        }

        if (!EqualityComparer<TOption>.Default.Equals(actual, expected))
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Question '{question.Id}' selected '{actual}', but '{expected}' was expected."));
        }
    }

    /// <summary>
    /// Asserts how many answers a result carries.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="expected">The expected number of answers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> is negative.</exception>
    /// <exception cref="DecisionAssertionException">The result carries another number of answers.</exception>
    public static void HasAnswerCount(DecisionResult result, int expected)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        if (result.Count != expected)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected {expected} answer(s), but the result answered {Describe(result)}."));
        }
    }

    /// <summary>
    /// Asserts that a request asked a question, and returns it.
    /// </summary>
    /// <param name="request">The captured request.</param>
    /// <param name="questionId">The identifier that should have been asked.</param>
    /// <returns>The question, so that a test can assert further on it.</returns>
    /// <exception cref="ArgumentException"><paramref name="questionId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The request did not ask that question.</exception>
    public static Question Asked(DecisionRequest request, string questionId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Asked(request, new QuestionId(questionId));
    }

    /// <summary>
    /// Asserts that a request asked a question, and returns it.
    /// </summary>
    /// <param name="request">The captured request.</param>
    /// <param name="questionId">The identifier that should have been asked.</param>
    /// <returns>The question, so that a test can assert further on it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The request did not ask that question.</exception>
    public static Question Asked(DecisionRequest request, QuestionId questionId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Questions.TryGet(questionId, out Question? question))
        {
            return question;
        }

        throw new DecisionAssertionException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Expected question '{questionId}' to be asked, but the request asked {DescribeQuestions(request.Questions)}."));
    }

    /// <summary>
    /// Asserts exactly which questions a request asked, and in which order.
    /// </summary>
    /// <param name="request">The captured request.</param>
    /// <param name="questionIds">The identifiers expected, in the order they should appear.</param>
    /// <exception cref="ArgumentException">An identifier is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The request asked something else, or in another order.</exception>
    public static void AskedExactly(DecisionRequest request, params string[] questionIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(questionIds);

        bool matches = request.Questions.Count == questionIds.Length;

        for (int index = 0; matches && index < questionIds.Length; index++)
        {
            matches = request.Questions[index].Id == new QuestionId(questionIds[index]);
        }

        if (!matches)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected the request to ask [{string.Join(", ", questionIds)}], but it asked {DescribeQuestions(request.Questions)}."));
        }
    }

    /// <summary>
    /// Asserts that a provider was called exactly once, and returns that call.
    /// </summary>
    /// <param name="calls">The captured call log.</param>
    /// <returns>The only recorded call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calls"/> is <see langword="null"/>.</exception>
    /// <exception cref="DecisionAssertionException">The provider was called a different number of times.</exception>
    public static RecordedDecisionCall CalledOnce(DecisionCallLog calls)
    {
        CalledTimes(calls, 1);

        return calls[0];
    }

    /// <summary>
    /// Asserts how many times a provider was called.
    /// </summary>
    /// <param name="calls">The captured call log.</param>
    /// <param name="expected">The expected number of calls.</param>
    /// <exception cref="ArgumentNullException"><paramref name="calls"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> is negative.</exception>
    /// <exception cref="DecisionAssertionException">The provider was called a different number of times.</exception>
    public static void CalledTimes(DecisionCallLog calls, int expected)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        if (calls.Count != expected)
        {
            throw new DecisionAssertionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected {expected} provider call(s), but {calls.Count} were recorded."));
        }
    }

    private static string Describe(DecisionResult result)
    {
        if (result.Count == 0)
        {
            return "nothing";
        }

        StringBuilder description = new();

        foreach (Answer answer in result.Answers)
        {
            description.Append(description.Length == 0 ? "[" : ", ").Append(answer.QuestionId);
        }

        return description.Append(']').ToString();
    }

    private static string DescribeQuestions(QuestionSet questions)
    {
        if (questions.Count == 0)
        {
            return "nothing";
        }

        StringBuilder description = new();

        foreach (Question question in questions)
        {
            description.Append(description.Length == 0 ? "[" : ", ").Append(question.Id);
        }

        return description.Append(']').ToString();
    }
}
