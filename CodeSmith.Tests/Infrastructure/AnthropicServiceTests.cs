// == Anthropic Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using CodeSmith.Core.Interfaces;

namespace CodeSmith.Tests.Infrastructure;

public class AnthropicServiceTests
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
        var label = AnthropicService.GetLanguageLabel(language);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void GetLanguageLabel_WithUnknownLanguage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AnthropicService.GetLanguageLabel((Language)999));
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

        var (description, starterCode) = AnthropicService.ParseProblemResponse(response);

        Assert.Contains("reverses a string", description);
        Assert.Contains("public class Solution", starterCode);
    }

    [Fact]
    public void ParseProblemResponse_WithoutMarkers_FallsBackToFullText()
    {
        var response = "Just a plain text response without markers.";

        var (description, starterCode) = AnthropicService.ParseProblemResponse(response);

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

        var (description, starterCode) = AnthropicService.ParseProblemResponse(response);

        Assert.Contains("simple problem", description);
        Assert.Contains("public void Solve", starterCode);
    }

    // == Session Not Found Tests == //

    [Fact]
    public async Task GetGuidanceAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.Get(Arg.Any<Guid>()).Returns((ProblemSession?)null);

        var options = Options.Create(new AnthropicOptions { ApiKey = "test-key" });
        var logger = Substitute.For<ILogger<AnthropicService>>();

        var service = new AnthropicService(options, sessionStore, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", null, CancellationToken.None));
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ThrowsSessionNotFoundButAcceptsParam()
    {
        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.Get(Arg.Any<Guid>()).Returns((ProblemSession?)null);

        var options = Options.Create(new AnthropicOptions { ApiKey = "test-key" });
        var logger = Substitute.For<ILogger<AnthropicService>>();

        var service = new AnthropicService(options, sessionStore, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", "int x = 42;", CancellationToken.None));
    }
}
