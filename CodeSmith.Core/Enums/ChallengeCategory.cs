// == Challenge Category Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// Represents the prompt engineering skill category a challenge tests.
/// </summary>
public enum ChallengeCategory
{
    OutputFormatControl,   // Controlling the shape and format of model output
    SpecificityOfScope,    // Narrowing or broadening the model's focus
    NegativeInstructions,  // Telling the model what NOT to do
    ConditionalBehavior,   // Changing model behavior based on input conditions
    QuantityEnumeration,   // Controlling counts, lists, and enumerations
    ToneRegister           // Adjusting formality, tone, or persona
}
