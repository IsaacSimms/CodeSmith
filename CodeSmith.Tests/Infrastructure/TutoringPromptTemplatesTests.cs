// == Tutoring Prompt Templates Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class TutoringPromptTemplatesTests
{
    private readonly TutoringPromptTemplates _templates = new();

    private static ProblemSpec Spec(
        Difficulty difficulty = Difficulty.Easy,
        Language language     = Language.Python,
        ProblemFocus focus    = ProblemFocus.Random,
        ProblemTopic topic    = ProblemTopic.Random)
        => new(difficulty, language, AiProvider.Anthropic, focus, topic);

    // == ProblemGeneration == //

    [Fact]
    public void ProblemGeneration_SystemPromptContainsLanguageLabel()
    {
        var result = _templates.ProblemGeneration(Spec(language: Language.Python));

        Assert.Contains("Python", result.SystemPrompt);
    }

    [Fact]
    public void ProblemGeneration_UserMessageContainsDifficultyAndLanguage()
    {
        var result = _templates.ProblemGeneration(Spec(Difficulty.Hard, Language.Go));

        Assert.Contains("Hard", result.UserMessage);
        Assert.Contains("Go", result.UserMessage);
    }

    [Fact]
    public void ProblemGeneration_UserMessageContainsResolvedFocusAndTopicProse()
    {
        var result = _templates.ProblemGeneration(Spec(Difficulty.Medium, Language.CSharp));

        Assert.Contains(TutoringPromptTemplates.FocusProse[result.Focus], result.UserMessage);
        Assert.Contains(TutoringPromptTemplates.TopicProse[result.Topic], result.UserMessage);
    }

    [Fact]
    public void ProblemGeneration_WhenRandom_ResolvesBothAxesToConcreteValues()
    {
        var result = _templates.ProblemGeneration(Spec(language: Language.Rust));

        Assert.NotEqual(ProblemFocus.Random, result.Focus);
        Assert.NotEqual(ProblemTopic.Random, result.Topic);
    }

    // Replaces ProblemGeneration_CategoryComeFromKnownSet — the string[] it asserted against is gone
    [Fact]
    public void ProblemGeneration_WhenRandom_TopicComesFromEnum()
    {
        var result = _templates.ProblemGeneration(Spec(Difficulty.Medium, Language.Java));

        Assert.Contains(result.Topic, TutoringPromptTemplates.TopicRoll);
    }

    // Replaces ProblemGeneration_AngleComeFromKnownSet — same reason
    [Fact]
    public void ProblemGeneration_WhenRandom_FocusComesFromEnum()
    {
        var result = _templates.ProblemGeneration(Spec(Difficulty.Hard, Language.TypeScript));

        Assert.Contains(result.Focus, TutoringPromptTemplates.WeightedFocusRoll);
    }

    [Theory]
    [InlineData(Language.CSharp,     "C#")]
    [InlineData(Language.Cpp,        "C++")]
    [InlineData(Language.Go,         "Go")]
    [InlineData(Language.Rust,       "Rust")]
    [InlineData(Language.Python,     "Python")]
    [InlineData(Language.Java,       "Java")]
    [InlineData(Language.TypeScript, "TypeScript")]
    public void ProblemGeneration_LanguageLabelIsCorrect(Language language, string expectedLabel)
    {
        var result = _templates.ProblemGeneration(Spec(language: language));

        Assert.Equal(expectedLabel, result.LanguageLabel);
        Assert.Contains(expectedLabel, result.SystemPrompt);
    }

    // == Explicit Selection == //

    [Theory]
    [InlineData(ProblemFocus.Standard)]
    [InlineData(ProblemFocus.BugFix)]
    [InlineData(ProblemFocus.PerformanceOptimization)]
    [InlineData(ProblemFocus.FeatureExtension)]
    [InlineData(ProblemFocus.UnusualConstraints)]
    [InlineData(ProblemFocus.EdgeCaseGauntlet)]
    [InlineData(ProblemFocus.RealWorldScenario)]
    [InlineData(ProblemFocus.Refactoring)]
    public void ProblemGeneration_WhenExplicitFocus_HonorsItExactly(ProblemFocus focus)
    {
        var result = _templates.ProblemGeneration(Spec(focus: focus));

        Assert.Equal(focus, result.Focus);
        Assert.Contains(TutoringPromptTemplates.FocusProse[focus], result.UserMessage);
    }

    [Theory]
    [InlineData(ProblemTopic.ArraysAndStrings)]
    [InlineData(ProblemTopic.DynamicProgramming)]
    [InlineData(ProblemTopic.SimulationAndModeling)]
    [InlineData(ProblemTopic.BitManipulation)]
    public void ProblemGeneration_WhenExplicitTopic_HonorsItExactly(ProblemTopic topic)
    {
        var result = _templates.ProblemGeneration(Spec(topic: topic));

        Assert.Equal(topic, result.Topic);
        Assert.Contains(TutoringPromptTemplates.TopicProse[topic], result.UserMessage);
    }

    [Fact]
    public void ProblemGeneration_WhenBothPinned_LeavesTheOtherAxisAlone()
    {
        var result = _templates.ProblemGeneration(Spec(focus: ProblemFocus.Refactoring, topic: ProblemTopic.StateMachines));

        Assert.Equal(ProblemFocus.Refactoring,   result.Focus);
        Assert.Equal(ProblemTopic.StateMachines, result.Topic);
    }

    // == Weighted Focus Roll == //
    // Exposing Focus to the user must not retune what Random produces — these pin the historical
    // distribution as data so the assertion is deterministic rather than statistical.

    [Fact]
    public void WeightedFocusRoll_HasTenEntries()
    {
        Assert.Equal(10, TutoringPromptTemplates.WeightedFocusRoll.Length);
    }

    [Fact]
    public void WeightedFocusRoll_StandardAppearsThreeTimes()
    {
        Assert.Equal(3, TutoringPromptTemplates.WeightedFocusRoll.Count(f => f == ProblemFocus.Standard));
    }

    [Fact]
    public void WeightedFocusRoll_EveryNonStandardFocusAppearsOnce()
    {
        var others = Enum.GetValues<ProblemFocus>()
            .Where(f => f is not ProblemFocus.Random and not ProblemFocus.Standard);

        Assert.All(others, focus =>
            Assert.Equal(1, TutoringPromptTemplates.WeightedFocusRoll.Count(f => f == focus)));
    }

    [Fact]
    public void WeightedFocusRoll_NeverContainsRandom()
    {
        Assert.DoesNotContain(ProblemFocus.Random, TutoringPromptTemplates.WeightedFocusRoll);
    }

    [Fact]
    public void TopicRoll_CoversEveryTopicExactlyOnce()
    {
        var expected = Enum.GetValues<ProblemTopic>().Where(t => t != ProblemTopic.Random);

        Assert.Equal(12, TutoringPromptTemplates.TopicRoll.Length);
        Assert.All(expected, topic =>
            Assert.Equal(1, TutoringPromptTemplates.TopicRoll.Count(t => t == topic)));
    }

    // == Prose Map Completeness == //
    // The enum, the C# prose map, and the frontend label map are three parallel lists with no
    // compile-time link. These cover the C# half so a new member cannot ship without its prose.

    [Fact]
    public void FocusProse_HasEntryForEveryEnumMemberExceptRandom()
    {
        var expected = Enum.GetValues<ProblemFocus>().Where(f => f != ProblemFocus.Random);

        Assert.All(expected, focus => Assert.True(
            TutoringPromptTemplates.FocusProse.ContainsKey(focus),
            $"ProblemFocus.{focus} has no prose entry"));
        Assert.DoesNotContain(ProblemFocus.Random, TutoringPromptTemplates.FocusProse.Keys);
    }

    [Fact]
    public void TopicProse_HasEntryForEveryEnumMemberExceptRandom()
    {
        var expected = Enum.GetValues<ProblemTopic>().Where(t => t != ProblemTopic.Random);

        Assert.All(expected, topic => Assert.True(
            TutoringPromptTemplates.TopicProse.ContainsKey(topic),
            $"ProblemTopic.{topic} has no prose entry"));
        Assert.DoesNotContain(ProblemTopic.Random, TutoringPromptTemplates.TopicProse.Keys);
    }

    // == Prompt Hardening == //

    [Fact]
    public void ProblemGeneration_SystemPromptBindsFocusAndSoftensTopic()
    {
        var result = _templates.ProblemGeneration(Spec());

        Assert.Contains("approach style is binding", result.SystemPrompt);
        Assert.Contains("strong preference",         result.SystemPrompt);
    }

    // == GuidanceSystemPrompt == //

    [Fact]
    public void GuidanceSystemPrompt_ContainsProblemDescription()
    {
        var prompt = _templates.GuidanceSystemPrompt(Language.Python, "Find the two sum", "def two_sum(): pass");

        Assert.Contains("Find the two sum", prompt);
    }

    [Fact]
    public void GuidanceSystemPrompt_ContainsStarterCode()
    {
        var prompt = _templates.GuidanceSystemPrompt(Language.Python, "Find the two sum", "def two_sum(): pass");

        Assert.Contains("def two_sum(): pass", prompt);
    }

    [Fact]
    public void GuidanceSystemPrompt_ContainsLanguageLabel()
    {
        var prompt = _templates.GuidanceSystemPrompt(Language.Go, "Problem", "starter");

        Assert.Contains("Go", prompt);
    }

    [Fact]
    public void GuidanceSystemPrompt_WhenCodeAnalysis_DiffersFromGuidance()
    {
        var guidance     = _templates.GuidanceSystemPrompt(Language.Python, "Problem", "code", guidanceMode: GuidanceMode.Guidance);
        var codeAnalysis = _templates.GuidanceSystemPrompt(Language.Python, "Problem", "code", guidanceMode: GuidanceMode.CodeAnalysis);

        Assert.NotEqual(guidance, codeAnalysis);
    }

    [Fact]
    public void GuidanceSystemPrompt_WhenEditorContent_AppendsEditorSection()
    {
        var withEditor    = _templates.GuidanceSystemPrompt(Language.Rust, "Problem", "starter", editorContent: "fn main() {}");
        var withoutEditor = _templates.GuidanceSystemPrompt(Language.Rust, "Problem", "starter");

        Assert.Contains("fn main() {}", withEditor);
        Assert.DoesNotContain("fn main() {}", withoutEditor);
    }

    [Fact]
    public void GuidanceSystemPrompt_WhenEditorContentIsWhitespace_DoesNotAppendEditorSection()
    {
        var withoutEditor = _templates.GuidanceSystemPrompt(Language.Java, "Problem", "starter");
        var withWhitespace = _templates.GuidanceSystemPrompt(Language.Java, "Problem", "starter", editorContent: "   ");

        Assert.Equal(withoutEditor, withWhitespace);
    }

    [Fact]
    public void GuidanceSystemPrompt_ContainsSessionFocus()
    {
        var prompt = _templates.GuidanceSystemPrompt(Language.CSharp, "Problem", "starter", focus: ProblemFocus.Refactoring);

        // The short label, not the full explanatory prose — the tutor needs the exercise type, not the spec
        Assert.Contains("Refactoring exercise", prompt);
    }

    [Fact]
    public void GuidanceSystemPrompt_WhenFocusIsRandom_OmitsTheFocusStatement()
    {
        var prompt = _templates.GuidanceSystemPrompt(Language.CSharp, "Problem", "starter", focus: ProblemFocus.Random);

        Assert.DoesNotContain("exercise — keep your guidance", prompt);
    }

    [Fact]
    public void ShortFocusLabel_StripsTheExplanatoryClause()
    {
        Assert.Equal("Bug fix",                 TutoringPromptTemplates.ShortFocusLabel(ProblemFocus.BugFix));
        Assert.Equal("Refactoring",             TutoringPromptTemplates.ShortFocusLabel(ProblemFocus.Refactoring));
        Assert.Equal("Standard implementation", TutoringPromptTemplates.ShortFocusLabel(ProblemFocus.Standard));
    }
}
