// == Tutoring Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class TutoringServiceTests
{
    // == Language Label Tests == //

    [Theory]
    [InlineData(Language.CSharp, "C#")]
    [InlineData(Language.Cpp,    "C++")]
    [InlineData(Language.Go,     "Go")]
    [InlineData(Language.Rust,   "Rust")]
    [InlineData(Language.Python, "Python")]
    [InlineData(Language.Java,   "Java")]
    [InlineData(Language.TypeScript, "TypeScript")]
    public void GetLanguageLabel_ReturnsHumanReadableLabel(Language language, string expected)
    {
        var label = TutoringService.GetLanguageLabel(language);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void GetLanguageLabel_WithUnknownLanguage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TutoringService.GetLanguageLabel((Language)999));
    }

    // == Response Parsing Tests == //

    [Fact]
    public void ParseProblemResponse_WithValidFormat_ExtractsCorrectly()
    {
        var response = """
            DESCRIPTION:
            Write a function that reverses a string.

            STARTER_CODE:
            public class Solution
            {
                public string Reverse(string input)
                {
                    // Your code here
                }
            }
            """;

        var (description, starterCode) = TutoringService.ParseProblemResponse(response);

        Assert.Contains("reverses a string", description);
        Assert.Contains("public class Solution", starterCode);
    }

    [Fact]
    public void ParseProblemResponse_WithoutMarkers_FallsBackToFullText()
    {
        var response = "Just a plain text response without markers.";

        var (description, starterCode) = TutoringService.ParseProblemResponse(response);

        Assert.Equal("Just a plain text response without markers.", description);
        Assert.Equal(string.Empty, starterCode);
    }

    [Fact]
    public void ParseProblemResponse_CaseInsensitive_ParsesCorrectly()
    {
        var response = """
            description:
            A simple problem.

            starter_code:
            public void Solve() { }
            """;

        var (description, starterCode) = TutoringService.ParseProblemResponse(response);

        Assert.Contains("simple problem", description);
        Assert.Contains("public void Solve", starterCode);
    }

    // == Session Not Found Tests == //

    [Fact]
    public async Task GetGuidanceAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.Get(Arg.Any<Guid>()).Returns((ProblemSession?)null);

        var llmService = Substitute.For<ILlmService>();
        var logger = Substitute.For<ILogger<TutoringService>>();
        var service = new TutoringService(llmService, sessionStore, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", null, false, CancellationToken.None));
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ThrowsSessionNotFoundButAcceptsParam()
    {
        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.Get(Arg.Any<Guid>()).Returns((ProblemSession?)null);

        var llmService = Substitute.For<ILlmService>();
        var logger = Substitute.For<ILogger<TutoringService>>();
        var service = new TutoringService(llmService, sessionStore, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", "int x = 42;", false, CancellationToken.None));
    }

    // == Problem Variety Array Tests == //

    [Fact]
    public void ProblemCategories_HasAtLeastEightEntries()
    {
        Assert.True(TutoringService.ProblemCategories.Length >= 8,
            $"Expected at least 8 categories for variety, but found {TutoringService.ProblemCategories.Length}.");
    }

    [Fact]
    public void ProblemAngles_HasAtLeastFiveEntries()
    {
        Assert.True(TutoringService.ProblemAngles.Length >= 5,
            $"Expected at least 5 angles for variety, but found {TutoringService.ProblemAngles.Length}.");
    }

    [Fact]
    public void ProblemAngles_ContainsStandardImplementationMultipleTimesForWeighting()
    {
        var count = TutoringService.ProblemAngles.Count(a => a == "Standard implementation");
        Assert.True(count >= 2,
            $"Expected 'Standard implementation' to appear at least twice for baseline weighting, but found {count}.");
    }
}
