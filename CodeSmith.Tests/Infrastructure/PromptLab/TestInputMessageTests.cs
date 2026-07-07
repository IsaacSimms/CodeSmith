// == Test Input Message Tests == //
using CodeSmith.Infrastructure.Services.PromptLab;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class TestInputMessageTests
{
    [Fact]
    public void Build_TemplateWithPlaceholder_SubstitutesValue()
    {
        Assert.Equal("Summarize: the article", TestInputMessage.Build("Summarize: {input}", "the article"));
    }

    [Fact]
    public void Build_PlaceholderIsCaseInsensitive()
    {
        Assert.Equal("Summarize: the article", TestInputMessage.Build("Summarize: {INPUT}", "the article"));
    }

    [Fact]
    public void Build_MultiplePlaceholders_AllSubstituted()
    {
        Assert.Equal("a / a", TestInputMessage.Build("{input} / {input}", "a"));
    }

    [Fact]
    public void Build_NoPlaceholder_AppendsValueOnNewLine()
    {
        Assert.Equal("Summarize this.\n\nthe article", TestInputMessage.Build("Summarize this.", "the article"));
    }
}
