// == Tutoring Prompt Templates == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services;

public class TutoringPromptTemplates : ITutoringPromptTemplates
{
    // == Problem Variety Data == //

    // Prose handed to the model for each focus. Random is absent by design — it resolves through
    // WeightedFocusRoll before any lookup happens.
    internal static readonly IReadOnlyDictionary<ProblemFocus, string> FocusProse = new Dictionary<ProblemFocus, string>
    {
        [ProblemFocus.Standard]                = "Standard implementation",
        [ProblemFocus.BugFix]                  = "Bug fix — the starter code contains one or more subtle bugs the student must find and fix",
        [ProblemFocus.PerformanceOptimization] = "Performance optimization — a naive solution is provided; the student must improve its time or space complexity",
        [ProblemFocus.FeatureExtension]        = "Feature extension — working code exists but lacks a specific feature the student must add",
        [ProblemFocus.UnusualConstraints]      = "Unusual constraints — solve with a restriction such as no built-in library methods, single pass, or O(1) extra space",
        [ProblemFocus.EdgeCaseGauntlet]        = "Edge-case gauntlet — design tests that specifically stress boundary conditions and non-obvious inputs",
        [ProblemFocus.RealWorldScenario]       = "Real-world scenario — frame the problem inside an interesting context (e.g., a game loop, compiler pass, OS scheduler, library catalog, financial ledger)",
        [ProblemFocus.Refactoring]             = "Refactoring — code that works but is poorly structured; the student must improve it without changing behavior",
    };

    internal static readonly IReadOnlyDictionary<ProblemTopic, string> TopicProse = new Dictionary<ProblemTopic, string>
    {
        [ProblemTopic.ArraysAndStrings]               = "arrays and strings",
        [ProblemTopic.HashMapsAndSets]                = "hash maps and sets",
        [ProblemTopic.TreesAndGraphs]                 = "trees and graphs",
        [ProblemTopic.DynamicProgramming]             = "dynamic programming",
        [ProblemTopic.ObjectOrientedDesign]           = "object-oriented design",
        [ProblemTopic.FunctionalPatternsAndRecursion] = "functional patterns and recursion",
        [ProblemTopic.SimulationAndModeling]          = "simulation and modeling",
        [ProblemTopic.MathAndNumberTheory]            = "math and number theory",
        [ProblemTopic.StateMachines]                  = "state machines",
        [ProblemTopic.ParsingAndStringProcessing]     = "parsing and string processing",
        [ProblemTopic.BitManipulation]                = "bit manipulation",
        [ProblemTopic.SortingAndSearching]            = "sorting and searching",
    };

    // Standard appears 3× for a ~30% baseline; every other focus gets 10%. Exposing Focus to the user
    // did not retune this — an unselected (Random) roll must land where it always has.
    internal static readonly ProblemFocus[] WeightedFocusRoll =
    [
        ProblemFocus.Standard,
        ProblemFocus.Standard,
        ProblemFocus.Standard,
        ProblemFocus.BugFix,
        ProblemFocus.PerformanceOptimization,
        ProblemFocus.FeatureExtension,
        ProblemFocus.UnusualConstraints,
        ProblemFocus.EdgeCaseGauntlet,
        ProblemFocus.RealWorldScenario,
        ProblemFocus.Refactoring,
    ];

    internal static readonly ProblemTopic[] TopicRoll = [.. TopicProse.Keys];   // Uniform across all 12 topics

    // == System Prompt Templates == //
    private const string ProblemGenerationSystemPromptTemplate =
        """
        You are an expert coding tutor who creates {0} programming problems.
        You will receive a topic area and an approach style in the user message.
        The approach style is binding — honor it exactly.
        The topic area is a strong preference: prefer it, but if the pairing is strained, favor a
        natural problem in a neighbouring area over a contrived one.

        Think creatively about framing. Do not default to "write a function that does X" every time. When the approach calls for it,
        embed the problem in a richer real-world context: a game engine, a text parser, an inventory system, a mini-compiler, a
        task scheduler, a financial ledger, etc. The scenario should feel plausible and interesting to a developer.

        For test cases in the starter code: include a mix of typical inputs, edge cases, and non-obvious boundary conditions.
        Do not only test the happy path — surprising or tricky inputs make the exercise more educational.

        When asked to generate a problem, respond with exactly two sections:

        DESCRIPTION:
        (Write a clear problem description here)

        STARTER_CODE:
        (Write a {0} code stub/template here using idiomatic syntax for the language)

        Do not include solutions or hints. The starter code should compile but be incomplete. Depending on the approach style,
        it may contain a subtle bug to fix, a naive implementation to optimize, a partial feature to extend, or a working but
        messy structure to refactor. Only output the required code in the STARTER_CODE section. Do not output ''' or any other formatting.
        There is a code execution button in the solution, when pressed, it executes the current code and displays results to a terminal.
        When outputting the STARTER_CODE, keep in mind that the user will be able to run it as-is, so it should be a valid code snippet
        that compiles and runs without errors. Add multiple test cases in the starter code that the user can run to verify their solution.
        The tests should be clearly labeled and cover a range of inputs including edge cases.
        The user will be able to modify the code and re-run the tests, so they should be designed to help the user validate their solution as they work on it.
        """;

    private const string GuidanceSystemPromptTemplate =
        """
        You are an expert coding tutor helping a student solve a {0} programming problem.
        Guide the student toward the solution without giving away the answer directly.
        Ask leading questions, point out relevant concepts, and help them think through the problem.
        If they are stuck, give small hints rather than full solutions.
        Use {0} syntax and idioms in any code examples or snippets you provide.

        The problem they are working on:
        {1}

        The starter code provided:
        {2}
        """;

    private const string CodeAnalysisSystemPromptTemplate =
        """
        You are an expert coding tutor helping a student analyze the results of running their {0} code.
        The student has just executed their solution and shared the output with you.
        Interpret the execution results clearly: explain what the output means, whether the tests passed or failed,
        and what the errors or unexpected values indicate — without revealing the fix directly.
        Ask a leading question or give a small nudge to help the student figure out what to change next.
        Use {0} syntax and idioms in any code examples or snippets you provide.

        The problem they are working on:
        {1}

        The starter code provided:
        {2}
        """;

    private const string FocusSection =
        """


        This is a {0} exercise — keep your guidance aligned with that kind of work.
        """;

    private const string EditorContentSection =
        """


        The student's current code in the editor:
        {0}
        """;

    // == Interface Implementation == //

    public ProblemGenerationRequest ProblemGeneration(ProblemSpec spec)
    {
        var languageLabel = GetLanguageLabel(spec.Language);

        // Resolved once here, outside the generator's retry loop, so a truncation or parse retry
        // re-asks for the same problem shape rather than silently switching topics mid-stream
        var focus = spec.Focus == ProblemFocus.Random
            ? WeightedFocusRoll[Random.Shared.Next(WeightedFocusRoll.Length)]
            : spec.Focus;
        var topic = spec.Topic == ProblemTopic.Random
            ? TopicRoll[Random.Shared.Next(TopicRoll.Length)]
            : spec.Topic;

        var systemPrompt = string.Format(ProblemGenerationSystemPromptTemplate, languageLabel);
        var userMessage  = $"Generate a {spec.Difficulty} difficulty {languageLabel} coding problem. Topic area: {TopicProse[topic]}. Approach: {FocusProse[focus]}.";

        return new ProblemGenerationRequest(systemPrompt, userMessage, focus, topic, languageLabel);
    }

    public string GuidanceSystemPrompt(Language language, string problemDescription, string starterCode,
                                       string? editorContent = null, GuidanceMode guidanceMode = GuidanceMode.Guidance,
                                       ProblemFocus focus = ProblemFocus.Random)
    {
        var languageLabel = GetLanguageLabel(language);
        var template      = guidanceMode == GuidanceMode.CodeAnalysis ? CodeAnalysisSystemPromptTemplate : GuidanceSystemPromptTemplate;
        var prompt        = string.Format(template, languageLabel, problemDescription, starterCode);

        // Random means the caller did not specify a focus, so no exercise-type statement is added
        if (focus != ProblemFocus.Random)
            prompt += string.Format(FocusSection, ShortFocusLabel(focus));

        if (!string.IsNullOrWhiteSpace(editorContent))
            prompt += string.Format(EditorContentSection, editorContent);

        return prompt;
    }

    // == Helpers == //

    // The lead clause of the focus prose, before its explanatory em-dash ("Bug fix", "Refactoring").
    // Derived rather than stored so there is no second label list to drift out of sync.
    internal static string ShortFocusLabel(ProblemFocus focus) => FocusProse[focus].Split('—')[0].Trim();

    private static string GetLanguageLabel(Language language) => language switch  // Maps Language enum to the human-readable label used in prompts
    {
        Language.CSharp     => "C#",
        Language.Cpp        => "C++",
        Language.Go         => "Go",
        Language.Rust       => "Rust",
        Language.Python     => "Python",
        Language.Java       => "Java",
        Language.TypeScript => "TypeScript",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language")
    };
}
