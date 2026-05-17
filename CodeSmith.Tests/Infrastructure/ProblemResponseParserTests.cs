// == Problem Response Parser Tests == //
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class ProblemResponseParserTests
{
    private readonly ProblemResponseParser _parser = new();

    [Fact]
    public void Parse_ValidResponse_ReturnsCorrectSections()
    {
        var response = """
            DESCRIPTION:
            Write a function that adds two numbers.

            STARTER_CODE:
            def add(a, b):
                pass
            """;

        var (description, starterCode) = _parser.Parse(response);

        Assert.Contains("Write a function that adds two numbers", description);
        Assert.Contains("def add(a, b):", starterCode);
    }

    [Fact]
    public void Parse_MissingMarkers_FallsBackToFullResponseAsDescription()
    {
        var response = "Just a plain response with no markers.";

        var (description, starterCode) = _parser.Parse(response);

        Assert.Equal("Just a plain response with no markers.", description);
        Assert.Equal(string.Empty, starterCode);
    }

    [Fact]
    public void Parse_CaseInsensitiveMarkers()
    {
        var response = """
            description:
            Lower case markers work.

            starter_code:
            int x = 0;
            """;

        var (description, starterCode) = _parser.Parse(response);

        Assert.Contains("Lower case markers work", description);
        Assert.Contains("int x = 0;", starterCode);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var response = "DESCRIPTION:   trimmed   STARTER_CODE:   code   ";

        var (description, starterCode) = _parser.Parse(response);

        Assert.Equal("trimmed", description);
        Assert.Equal("code", starterCode);
    }

    [Fact]
    public void Parse_OnlyDescriptionMarker_FallsBackToFullResponse()
    {
        // DESCRIPTION present but STARTER_CODE absent — only one marker should not partial-parse
        var response = "DESCRIPTION: some description but no code marker";

        var (description, starterCode) = _parser.Parse(response);

        Assert.Equal(response.Trim(), description);
        Assert.Equal(string.Empty, starterCode);
    }
}
