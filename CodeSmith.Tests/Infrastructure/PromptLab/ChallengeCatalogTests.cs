// == Challenge Catalog Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Infrastructure.Services.PromptLab;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class ChallengeCatalogTests
{
    // == Catalog Integrity Tests == //

    [Fact]
    public void All_ReturnsNonEmptyList()
    {
        Assert.NotEmpty(ChallengeCatalog.All);
    }

    [Fact]
    public void All_AllChallengesHaveUniqueIds()
    {
        var ids = ChallengeCatalog.All.Select(c => c.ChallengeId).ToList();
        var uniqueIds = ids.Distinct().ToList();

        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    [Fact]
    public void All_AllChallengesHaveNonEmptyRequiredFields()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(challenge.ChallengeId), $"Challenge missing ChallengeId");
            Assert.False(string.IsNullOrWhiteSpace(challenge.Title), $"Challenge '{challenge.ChallengeId}' missing Title");
            Assert.False(string.IsNullOrWhiteSpace(challenge.Description), $"Challenge '{challenge.ChallengeId}' missing Description");
        }
    }

    [Fact]
    public void All_AllChallengesHaveAtLeastThreeTestInputs()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            Assert.True(challenge.TestInputs.Count >= 3,
                $"Challenge '{challenge.ChallengeId}' has only {challenge.TestInputs.Count} test inputs (minimum 3)");
        }
    }

    [Fact]
    public void All_AllChallengesHaveAtLeastOneRubricCriterion()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            Assert.NotEmpty(challenge.Rubric);
        }
    }

    [Fact]
    public void All_AllChallengesHaveAtLeastOneEditableField()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            Assert.NotEmpty(challenge.EditableFields);
        }
    }

    [Fact]
    public void All_AllTestInputsHaveUniqueIdsWithinChallenge()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            var inputIds = challenge.TestInputs.Select(t => t.InputId).ToList();
            var uniqueInputIds = inputIds.Distinct().ToList();
            Assert.Equal(inputIds.Count, uniqueInputIds.Count);
        }
    }

    [Fact]
    public void All_AllRubricCriteriaHavePositiveMaxPoints()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            foreach (var criterion in challenge.Rubric)
            {
                Assert.True(criterion.MaxPoints > 0,
                    $"Criterion '{criterion.CriterionId}' in '{challenge.ChallengeId}' has MaxPoints={criterion.MaxPoints}");
            }
        }
    }

    [Fact]
    public void All_AllChallengesHaveHiddenAdversarialPrompt()
    {
        // Every challenge must have anti-gaming mechanics
        foreach (var challenge in ChallengeCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(challenge.HiddenAdversarialPrompt),
                $"Challenge '{challenge.ChallengeId}' is missing a HiddenAdversarialPrompt (required for anti-gaming)");
        }
    }

    [Fact]
    public void All_CoversSixCategories()
    {
        var categories = ChallengeCatalog.All.Select(c => c.Category).Distinct().ToList();

        Assert.Contains(ChallengeCategory.OutputFormatControl, categories);
        Assert.Contains(ChallengeCategory.SpecificityOfScope, categories);
        Assert.Contains(ChallengeCategory.NegativeInstructions, categories);
        Assert.Contains(ChallengeCategory.ConditionalBehavior, categories);
        Assert.Contains(ChallengeCategory.QuantityEnumeration, categories);
        Assert.Contains(ChallengeCategory.ToneRegister, categories);
    }

    [Fact]
    public void All_AllTestInputsHaveNonEmptyUserMessage()
    {
        foreach (var challenge in ChallengeCatalog.All)
        {
            foreach (var input in challenge.TestInputs)
            {
                Assert.False(string.IsNullOrWhiteSpace(input.UserMessage),
                    $"TestInput '{input.InputId}' in '{challenge.ChallengeId}' has empty UserMessage");
            }
        }
    }
}
