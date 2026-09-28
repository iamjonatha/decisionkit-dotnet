using System;
using DecisionKit.Answers;
using DecisionKit.Identifiers;
using DecisionKit.Questions;

namespace DecisionKit.Jev.Tests.Fixtures;

/// <summary>
/// A question type the JEV package has no representation for, used to prove that an unmappable
/// request is rejected instead of being sent as something else.
/// </summary>
public sealed class ExoticQuestion(QuestionId id, string prompt) : Question<ProbabilityAnswer>(id, prompt);
