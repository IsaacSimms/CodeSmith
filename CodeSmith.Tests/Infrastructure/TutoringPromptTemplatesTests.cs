// == Tutoring Prompt Templates Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class TutoringPromptTemplatesTests
{
    private readonly TutoringPromptTemplates _templates = new();

    // == ProblemGeneration == //

    [Fact]
    public void ProblemGeneration_SystemPromptContainsLanguageLabel()
    {
        var result = _templates.ProblemGeneration(Difficulty.Easy, Language.Python);

        Assert.Contains("Python", result.SystemPrompt);
    }

    [Fact]
    public void ProblemGeneration_UserMessageContainsDifficultyAndLanguage()
    {
        var result = _templates.ProblemGeneration(Difficulty.Hard, Language.Go);

        Assert.Contains("Hard", result.UserMessage);
        Assert.Contains("Go", result.UserMessage);
    }

    [Fact]
    public void ProblemGeneration_UserMessageContainsCategoryAndAngle()
    {
        var result = _templates.ProblemGeneration(Difficulty.Medium, Language.CSharp);

        Assert.Contains(result.Category, result.UserMessage);
        Assert.Contains(result.Angle, result.UserMessage);
    }

    [Fact]
    public void ProblemGeneration_ReturnsNonEmptyVarietyFields()
    {
        var result = _templates.ProblemGeneration(Difficulty.Easy, Language.Rust);

        Assert.False(string.IsNullOrWhiteSpace(result.Category));
        Assert.False(string.IsNullOrWhiteSpace(result.Angle));
    }

    [Fact]
    public void ProblemGeneration_CategoryComeFromKnownSet()
    {
        var result = _templates.ProblemGeneration(Difficulty.Medium, Language.Java);

        Assert.Contains(result.Category, TutoringPromptTemplates.ProblemCategories);
    }

    [Fact]
    public void ProblemGeneration_AngleComeFromKnownSet()
    {
        var result = _templates.ProblemGeneration(Difficulty.Hard, Language.TypeScript);

        Assert.Contains(result.Angle, TutoringPromptTemplates.ProblemAngles);
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
        var result = _templates.ProblemGeneration(Difficulty.Easy, language);

        Assert.Equal(expectedLabel, result.LanguageLabel);
        Assert.Contains(expectedLabel, result.SystemPrompt);
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

}
