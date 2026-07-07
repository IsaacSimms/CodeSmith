// == Test Input Message Builder == //
namespace CodeSmith.Infrastructure.Services.PromptLab;

/// <summary>
/// Builds the effective user message for a Prompt Lab test input from the student's template:
/// substitutes the {input} placeholder (case-insensitive), or appends the test input value on
/// a new line when the template has no placeholder. Shared by simulation and evaluation so the
/// output being scored is always the output that was generated.
/// </summary>
internal static class TestInputMessage
{
    private const string Placeholder = "{input}";

    public static string Build(string template, string testInputValue) =>
        template.Contains(Placeholder, StringComparison.OrdinalIgnoreCase)
            ? template.Replace(Placeholder, testInputValue, StringComparison.OrdinalIgnoreCase)
            : $"{template}\n\n{testInputValue}";
}
