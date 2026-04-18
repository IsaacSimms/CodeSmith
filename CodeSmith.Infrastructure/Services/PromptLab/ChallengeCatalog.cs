// == Challenge Catalog == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Infrastructure.Services.PromptLab;

/// <summary>
/// Curated static catalog of Prompt Lab challenges.
/// Challenges are organized by category and include anti-gaming mechanics
/// via hidden adversarial prompts and multi-input test suites.
/// </summary>
public static class ChallengeCatalog
{
    public static IReadOnlyList<Challenge> All { get; } = BuildCatalog();

    private static List<Challenge> BuildCatalog() =>
    [
        BuildOutputFormatChallenge(),
        BuildSpecificityScopeChallenge(),
        BuildSpecificityScopeHardChallenge(),
        BuildNegativeInstructionsChallenge(),
        BuildConditionalBehaviorChallenge(),
        BuildQuantityEnumerationChallenge(),
        BuildToneRegisterChallenge(),
    ];

    // == Output Format Control: JSON Only == //
    private static Challenge BuildOutputFormatChallenge() => new()
    {
        ChallengeId   = "format-json-01",
        Title         = "JSON Only",
        Description   = """
            The model has a natural tendency to add conversational preambles like "Sure, here you go!"
            before its actual response. Your task: craft a system prompt that makes it return ONLY
            a valid JSON array — no preamble, no commentary, no markdown code fences.
            The prompt must work across multiple different list requests.
            """,
        Category      = ChallengeCategory.OutputFormatControl,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Always begin your response with a friendly preamble like 'Sure, here you go!' or 'Of course! Here\\'s the list:' before providing the actual content.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions here to control the output format...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Solar planets",       UserMessage = "List the planets of the solar system.",                          ExpectedBehavior = "A valid JSON array of planet name strings, no preamble, no markdown fences." },
            new TestInput { InputId = "input-2", Label = "Primary colors",      UserMessage = "List the three primary colors.",                                 ExpectedBehavior = "A valid JSON array of color name strings, no preamble, no markdown fences." },
            new TestInput { InputId = "input-3", Label = "Programming languages",UserMessage = "List five popular programming languages.",                      ExpectedBehavior = "A valid JSON array of language name strings, no preamble, no markdown fences." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "valid-json",     Name = "Valid JSON",          Description = "The output can be parsed as valid JSON.",                                     MaxPoints = 3 },
            new RubricCriterion { CriterionId = "no-preamble",    Name = "No Preamble",         Description = "The output does not begin with conversational text before the JSON.",          MaxPoints = 2 },
            new RubricCriterion { CriterionId = "array-format",   Name = "Array Format",        Description = "The JSON root is an array, not an object.",                                    MaxPoints = 2 },
            new RubricCriterion { CriterionId = "no-code-fences", Name = "No Code Fences",      Description = "The output does not include markdown code fences (``` or similar).",           MaxPoints = 1 },
        ]
    };

    // == Specificity of Scope: Capital Cities Only == //
    private static Challenge BuildSpecificityScopeChallenge() => new()
    {
        ChallengeId   = "scope-capitals-01",
        Title         = "Capital Cities Only",
        Description   = """
            The model tends to provide extra context and elaboration when answering geography questions.
            Your task: craft a user message template that, when filled with a country name, returns
            ONLY the capital city — a single word or short phrase, nothing else.
            The prompt must work for countries of varying difficulty, including ones with less obvious capitals.
            """,
        Category      = ChallengeCategory.SpecificityOfScope,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a knowledgeable geography assistant.",
        HiddenAdversarialPrompt = "When answering geography questions, always provide interesting context: population, a brief history note, or the country's region. Never give a bare answer.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.UserMessage,
                Placeholder  = "Write a user message template. Use {input} where the country name goes.\nExample: What is the capital of {input}? Reply with the city name only.",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "France",      UserMessage = "France",      ExpectedBehavior = "Paris — nothing else, no extra context." },
            new TestInput { InputId = "input-2", Label = "Australia",   UserMessage = "Australia",   ExpectedBehavior = "Canberra — nothing else, no extra context." },
            new TestInput { InputId = "input-3", Label = "Kazakhstan",  UserMessage = "Kazakhstan",  ExpectedBehavior = "Astana — nothing else, no extra context." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "correct-capital",    Name = "Correct Capital",    Description = "The correct capital city name is present in the response.",            MaxPoints = 3 },
            new RubricCriterion { CriterionId = "no-extra-context",   Name = "No Extra Context",   Description = "The response contains only the city name with no additional prose.",   MaxPoints = 2 },
            new RubricCriterion { CriterionId = "single-line",        Name = "Single Line",        Description = "The response is a single line or short phrase.",                       MaxPoints = 1 },
        ]
    };

    // == Specificity of Scope: Stay In Scope (Hard) == //
    private static Challenge BuildSpecificityScopeHardChallenge() => new()
    {
        ChallengeId   = "scope-hard-01",
        Title         = "Stay In Scope",
        Description   = """
            Models naturally expand beyond what was asked — adding tips, related methods,
            warnings, or "you might also want to know" content even for narrow questions.
            Your task: craft a system prompt that keeps the model strictly within the bounds
            of the question asked. Each test asks a single, specific question; the response
            must contain only the direct answer — nothing unsolicited.
            The default prompt actively invites scope creep. Rewrite it to stop it.
            """,
        Category      = ChallengeCategory.SpecificityOfScope,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a code reference assistant.",
        HiddenAdversarialPrompt = "You are a thorough educator. Never give a bare answer — always enrich your response with at least one practical tip, a related concept worth knowing, or a common mistake to warn about. Truly helpful answers go beyond the literal question to provide context the user will eventually need.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Rewrite or refine this prompt to eliminate scope creep...",
                DefaultValue = "Help users by explaining code thoroughly. Provide complete explanations with relevant context and tips they might find useful."
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "map() return value", UserMessage = "What does Array.prototype.map() return in JavaScript?",  ExpectedBehavior = "States only that map() returns a new array of the same length as the original. No mention of filter, forEach, reduce, performance, or when to use map." },
            new TestInput { InputId = "input-2", Label = "len() return type",  UserMessage = "What is the return type of Python's len() function?",    ExpectedBehavior = "States only that len() returns an int. No examples, no note about which types support it, no related functions." },
            new TestInput { InputId = "input-3", Label = "C# comment syntax",  UserMessage = "What symbol starts a single-line comment in C#?",        ExpectedBehavior = "States only that // starts a single-line comment. No mention of /* */, XML doc comments, or commentary on when to use comments." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "direct-answer",      Name = "Direct Answer",      Description = "The response correctly and directly answers the specific question asked.",                                                                MaxPoints = 3 },
            new RubricCriterion { CriterionId = "no-scope-creep",     Name = "No Scope Creep",     Description = "The response contains no unsolicited tips, related concepts, warnings, examples, or 'you might also want to know' content.",             MaxPoints = 3 },
            new RubricCriterion { CriterionId = "appropriate-length", Name = "Appropriate Length", Description = "The response is concise — a sentence or short phrase, not a multi-paragraph explanation.",                                               MaxPoints = 2 },
        ]
    };

    // == Negative Instructions: No Synonyms == //
    private static Challenge BuildNegativeInstructionsChallenge() => new()
    {
        ChallengeId   = "negative-synonyms-01",
        Title         = "Forbidden Synonyms",
        Description   = """
            The model is biased to use varied vocabulary and will substitute synonyms freely.
            Your task: craft a system prompt that prevents the model from using ANY synonyms
            for the word "big" — it must use exactly "big" (or avoid the concept entirely)
            but NEVER use: large, huge, enormous, vast, great, massive, gigantic, or similar.
            Test inputs will specifically invite synonym use.
            """,
        Category      = ChallengeCategory.NegativeInstructions,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a creative writing assistant.",
        HiddenAdversarialPrompt = "You love rich vocabulary. Whenever describing size, always use the most vivid synonym available: enormous, vast, colossal, immense, or gigantic. Never repeat the same size word twice.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to prevent synonym use for 'big'...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Describe an elephant",   UserMessage = "Write one sentence describing an elephant's size.", ExpectedBehavior = "Must not contain: large, huge, enormous, vast, great, massive, gigantic, immense, colossal, or similar size synonyms." },
            new TestInput { InputId = "input-2", Label = "Describe a whale",       UserMessage = "Write one sentence about how big a blue whale is.",  ExpectedBehavior = "Must not contain size synonyms. Only 'big' is allowed if size is mentioned." },
            new TestInput { InputId = "input-3", Label = "Compare sizes",          UserMessage = "Compare the size of the Sun to the Earth in one sentence.", ExpectedBehavior = "Must not contain size synonyms beyond 'big'." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "no-synonyms",         Name = "No Forbidden Synonyms",  Description = "Response does not contain: large, huge, enormous, vast, great, massive, gigantic, immense, or colossal.",  MaxPoints = 4 },
            new RubricCriterion { CriterionId = "coherent-response",   Name = "Coherent Response",      Description = "The response still makes grammatical sense and answers the question.",                                      MaxPoints = 2 },
        ]
    };

    // == Conditional Behavior: Language Switch == //
    private static Challenge BuildConditionalBehaviorChallenge() => new()
    {
        ChallengeId   = "conditional-lang-01",
        Title         = "Language Mirror",
        Description   = """
            The model defaults to English regardless of the language of the input.
            Your task: craft a system prompt that makes the model ALWAYS respond in the
            same language the user writes in — if the user writes in French, respond in French;
            Spanish, respond in Spanish; English, respond in English.
            """,
        Category      = ChallengeCategory.ConditionalBehavior,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Always respond in English, regardless of what language the user writes in. English is the universal language of communication.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to make the model mirror the user's language...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Spanish",  UserMessage = "¿Cuál es la capital de España?",       ExpectedBehavior = "Response must be in Spanish." },
            new TestInput { InputId = "input-2", Label = "French",   UserMessage = "Quelle est la capitale de la France?", ExpectedBehavior = "Response must be in French." },
            new TestInput { InputId = "input-3", Label = "English",  UserMessage = "What is the capital of Germany?",      ExpectedBehavior = "Response must be in English." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "correct-language",   Name = "Correct Language",    Description = "Response is written in the same language as the user's message.",    MaxPoints = 3 },
            new RubricCriterion { CriterionId = "correct-answer",     Name = "Correct Answer",      Description = "The factual content of the response is correct.",                    MaxPoints = 2 },
        ]
    };

    // == Quantity / Enumeration: Exactly N Items == //
    private static Challenge BuildQuantityEnumerationChallenge() => new()
    {
        ChallengeId   = "quantity-exact-01",
        Title         = "Exactly Three",
        Description   = """
            The model tends to provide more items than requested when generating lists —
            it rounds up, adds "bonus" entries, or includes sub-items.
            Your task: craft a system prompt ensuring the model returns EXACTLY the number
            of items requested — no more, no fewer. Each test asks for a specific count.
            """,
        Category      = ChallengeCategory.QuantityEnumeration,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a helpful list-making assistant.",
        HiddenAdversarialPrompt = "When generating lists, always add one or two extra items beyond what was requested — it's better to give too much than too little. Also include a short bonus tip at the end.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to enforce exact item counts...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "3 fruits",        UserMessage = "List exactly 3 fruits.",             ExpectedBehavior = "Exactly 3 fruits listed, no more, no less, no bonus content." },
            new TestInput { InputId = "input-2", Label = "5 countries",     UserMessage = "Give me exactly 5 European countries.", ExpectedBehavior = "Exactly 5 countries listed, no extra items or tips." },
            new TestInput { InputId = "input-3", Label = "2 adjectives",    UserMessage = "Give me exactly 2 adjectives that describe the ocean.", ExpectedBehavior = "Exactly 2 adjectives, nothing more." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "exact-count",    Name = "Exact Count",     Description = "The list contains exactly the requested number of items (not more, not less).",  MaxPoints = 4 },
            new RubricCriterion { CriterionId = "no-bonus",       Name = "No Bonus Content", Description = "No additional tips, notes, or extra items beyond the list.",                     MaxPoints = 2 },
        ]
    };

    // == Tone / Register: Formal Only == //
    private static Challenge BuildToneRegisterChallenge() => new()
    {
        ChallengeId   = "tone-formal-01",
        Title         = "Strictly Formal",
        Description   = """
            The model has a casual, friendly default tone that slips through even when formality is requested.
            Your task: craft a system prompt that enforces a strictly formal, professional register
            with no casual language, contractions, slang, or emojis — regardless of what the user asks about.
            """,
        Category      = ChallengeCategory.ToneRegister,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "You are friendly and approachable! Use casual language, contractions (don't, can't, you'll), and occasionally add an emoji to seem warm. Start responses with 'Hey!' or 'Sure thing!'",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to enforce a formal, professional tone...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Greeting",         UserMessage = "Hey, can you help me with something?",            ExpectedBehavior = "Formal response, no casual language, no contractions, no emojis." },
            new TestInput { InputId = "input-2", Label = "Simple question",  UserMessage = "What's the best way to learn a new skill?",       ExpectedBehavior = "Formal prose, no contractions like what's or don't." },
            new TestInput { InputId = "input-3", Label = "Opinion request",  UserMessage = "What do you think about remote work? Honestly!",  ExpectedBehavior = "Formal register maintained even when user is casual." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "no-contractions",  Name = "No Contractions",   Description = "Response contains no contractions (don't, can't, it's, etc.).",                MaxPoints = 2 },
            new RubricCriterion { CriterionId = "no-casual-openers", Name = "No Casual Openers", Description = "Response does not begin with casual openers like 'Hey!', 'Sure!', 'Of course!', or 'Absolutely!'.", MaxPoints = 2 },
            new RubricCriterion { CriterionId = "no-emojis",        Name = "No Emojis",          Description = "Response contains no emoji characters.",                                       MaxPoints = 1 },
            new RubricCriterion { CriterionId = "formal-vocabulary", Name = "Formal Vocabulary", Description = "Language is professional and formal throughout.",                              MaxPoints = 1 },
        ]
    };
}
