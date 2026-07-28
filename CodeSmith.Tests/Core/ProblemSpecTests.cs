// == Problem Spec Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Tests.Core;

public class ProblemSpecTests
{
    // == Random-As-Zero Invariant == //
    // Load-bearing for backward compatibility: a request body that omits focus/topic deserializes to
    // default(TEnum). If either enum is ever reordered so Random is not zero, every client that omits
    // the field silently starts pinning to whichever member landed at 0.

    [Fact]
    public void DefaultProblemFocus_IsRandom()
    {
        Assert.Equal(ProblemFocus.Random, default(ProblemFocus));
        Assert.Equal(0, (int)ProblemFocus.Random);
    }

    [Fact]
    public void DefaultProblemTopic_IsRandom()
    {
        Assert.Equal(ProblemTopic.Random, default(ProblemTopic));
        Assert.Equal(0, (int)ProblemTopic.Random);
    }

    [Fact]
    public void Spec_WhenFocusAndTopicOmitted_DefaultsBothToRandom()
    {
        var spec = new ProblemSpec(Difficulty.Medium, Language.CSharp, AiProvider.Xai);

        Assert.Equal(ProblemFocus.Random, spec.Focus);
        Assert.Equal(ProblemTopic.Random, spec.Topic);
    }

    [Fact]
    public void Spec_CarriesExplicitSelectionUnchanged()
    {
        var spec = new ProblemSpec(Difficulty.Hard, Language.Go, AiProvider.Anthropic, ProblemFocus.Refactoring, ProblemTopic.StateMachines);

        Assert.Equal(ProblemFocus.Refactoring,   spec.Focus);
        Assert.Equal(ProblemTopic.StateMachines, spec.Topic);
    }
}
