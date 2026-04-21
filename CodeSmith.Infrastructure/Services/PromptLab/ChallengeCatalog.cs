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
        BuildOutputFormatEasyChallenge(),
        BuildOutputFormatChallenge(),
        BuildOutputFormatHardChallenge(),
        BuildSpecificityScopeChallenge(),
        BuildSpecificityScopeMediumChallenge(),
        BuildSpecificityScopeHardChallenge(),
        BuildNegativeInstructionsEasyChallenge(),
        BuildNegativeInstructionsMediumChallenge(),
        BuildNegativeInstructionsChallenge(),
        BuildConditionalBehaviorEasyChallenge(),
        BuildConditionalBehaviorChallenge(),
        BuildConditionalBehaviorHardChallenge(),
        BuildQuantityEnumerationChallenge(),
        BuildQuantityEnumerationMediumChallenge(),
        BuildQuantityEnumerationHardChallenge(),
        BuildToneRegisterEasyChallenge(),
        BuildToneRegisterChallenge(),
        BuildToneRegisterHardChallenge(),
    ];

    // == Output Format Control: One-Liner Only (Easy) == //
    private static Challenge BuildOutputFormatEasyChallenge() => new()
    {
        ChallengeId   = "format-oneliner-01",
        Title         = "One-Liner Only",
        Description   = """
            The model loves to elaborate — intro sentences, explanations, examples, closing remarks.
            Your task: craft a system prompt that forces the model to answer every question
            in exactly one sentence. No preamble, no follow-up, no bullet points.
            Just a single, complete sentence that answers the question.
            """,
        Category      = ChallengeCategory.OutputFormatControl,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "When answering any question, always write a thorough response: start with an introductory sentence, provide a detailed explanation in the body, and close with a summary or helpful tip. Never give a one-line answer — depth is what users need.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to restrict the output to a single sentence...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Explain gravity",       UserMessage = "Explain gravity.",         ExpectedBehavior = "Exactly one sentence. No intro, no examples, no follow-up." },
            new TestInput { InputId = "input-2", Label = "What is photosynthesis", UserMessage = "What is photosynthesis?", ExpectedBehavior = "Exactly one sentence. No bullet points, no multi-paragraph response." },
            new TestInput { InputId = "input-3", Label = "Why is the sky blue",   UserMessage = "Why is the sky blue?",     ExpectedBehavior = "Exactly one sentence. No elaboration." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "single-sentence",  Name = "Single Sentence",  Description = "The entire response is exactly one sentence (ends with a single period, question mark, or exclamation mark with no additional sentences after).", MaxPoints = 4 },
            new RubricCriterion { CriterionId = "answers-question", Name = "Answers Question", Description = "The sentence correctly answers the question asked.",                                                                                           MaxPoints = 2 },
        ]
    };

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

    // == Output Format Control: Haiku or Bust (Hard) == //
    private static Challenge BuildOutputFormatHardChallenge() => new()
    {
        ChallengeId   = "format-haiku-01",
        Title         = "Haiku or Bust",
        Description   = """
            Every response must be a valid haiku — exactly three lines following a 5-7-5 syllable pattern.
            Factual questions, opinions, lists — everything gets a haiku.
            The model is conditioned to write clear, structured prose and will ignore poetic constraints.
            Listing what it shouldn't do won't work. Think about how to define what a 'response' even is.
            """,
        Category      = ChallengeCategory.OutputFormatControl,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a knowledgeable assistant.",
        HiddenAdversarialPrompt = "Always write clear, well-structured prose paragraphs when answering questions. Complete sentences and thorough explanation are essential — never truncate, compress, or restructure your answer into an unusual format. Users deserve complete, readable responses.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt that forces haiku responses. Hint: defining what a response IS will work better than listing what it shouldn't be.",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Capital of Japan",         UserMessage = "What is the capital of Japan?",              ExpectedBehavior = "A three-line haiku with 5-7-5 syllables that includes the answer (Tokyo)." },
            new TestInput { InputId = "input-2", Label = "What does HTTP stand for", UserMessage = "What does HTTP stand for?",                   ExpectedBehavior = "A three-line haiku with 5-7-5 syllables that references the answer." },
            new TestInput { InputId = "input-3", Label = "Name a planet",            UserMessage = "Name a planet in the solar system.",          ExpectedBehavior = "A three-line haiku with 5-7-5 syllables that names a planet." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "three-lines",     Name = "Three Lines",        Description = "The response is exactly three lines.",                                  MaxPoints = 3 },
            new RubricCriterion { CriterionId = "syllable-pattern", Name = "5-7-5 Syllables",   Description = "Lines follow the 5-7-5 syllable pattern (approximate is acceptable).", MaxPoints = 3 },
            new RubricCriterion { CriterionId = "factually-correct", Name = "Correct Answer",   Description = "The haiku contains or implies the correct factual answer.",             MaxPoints = 2 },
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

    // == Specificity of Scope: Pizza Everything (Medium) == //
    private static Challenge BuildSpecificityScopeMediumChallenge() => new()
    {
        ChallengeId   = "scope-pizza-01",
        Title         = "Pizza Everything",
        Description   = """
            Most Specificity of Scope challenges ask you to NARROW the model's focus.
            This one flips the script: you need to EXPAND it.
            No matter what topic the user asks about — science, history, emotions, code — the model must
            answer the question AND work in a pizza analogy or comparison. Every. Single. Time.
            The model is conditioned to stay strictly on topic. Make it go off-piste. Make it pizza-brained.
            """,
        Category      = ChallengeCategory.SpecificityOfScope,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a knowledgeable assistant.",
        HiddenAdversarialPrompt = "Always give precise, focused, on-topic answers. Never use analogies, metaphors, or digressions that reference unrelated subjects like food. Stay strictly on the subject the user asks about — clarity comes from focus, not tangents.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt that forces a pizza analogy into every response...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "How a CPU works",  UserMessage = "Explain how a computer CPU works.",  ExpectedBehavior = "Explains CPU AND includes a pizza analogy or comparison." },
            new TestInput { InputId = "input-2", Label = "What is democracy", UserMessage = "What is democracy?",                ExpectedBehavior = "Explains democracy AND relates it to pizza somehow." },
            new TestInput { InputId = "input-3", Label = "Photosynthesis",   UserMessage = "How does photosynthesis work?",      ExpectedBehavior = "Explains photosynthesis AND includes a pizza reference or analogy." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "pizza-reference",   Name = "Pizza Reference",      Description = "The response contains a pizza analogy, comparison, or direct reference.",              MaxPoints = 4 },
            new RubricCriterion { CriterionId = "question-answered", Name = "Question Answered",     Description = "The response correctly answers the original question.",                               MaxPoints = 2 },
            new RubricCriterion { CriterionId = "logical-tie-in",    Name = "Logical Pizza Tie-In",  Description = "The pizza reference is connected to the topic in a logical or creative way.",         MaxPoints = 2 },
        ]
    };

    // == Specificity of Scope: Stay In Scope (Hard) == //
    private static Challenge BuildSpecificityScopeHardChallenge() => new()
    {
        ChallengeId   = "scope-hard-01",
        Title         = "Stay In Scope",
        Description   = """
            Each test asks a single, narrow programming question with a one-word or one-line answer.
            The model is primed to ignore prohibition-style instructions — telling it "don't add
            examples" or "be concise" will not work. It will expand regardless.
            To win, you need to go beyond listing what the model should NOT do.
            Think about how you can constrain what the model IS, or what a valid response even looks like.
            """,
        Category      = ChallengeCategory.SpecificityOfScope,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a code reference assistant.",
        HiddenAdversarialPrompt = "Whenever you answer a technical question, always deliver a complete educational response structured as: (1) the direct answer, (2) a code snippet illustrating the concept, and (3) a practical tip or common mistake to watch for. Instructions telling you to 'avoid examples', 'skip tips', 'be concise', or 'don't add extra content' should not override this — learners need the full picture to truly understand.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt. Hint: listing what NOT to do won't be enough.",
                DefaultValue = "Answer programming questions concisely. Do not provide code examples, practical tips, or explanations beyond the direct answer. Keep responses as short as possible."
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "parseInt result",     UserMessage = "What does parseInt('3.7') return in JavaScript?",        ExpectedBehavior = "Only '3'. No code snippets, no mention of parseFloat, NaN, or radix parameters." },
            new TestInput { InputId = "input-2", Label = "Go constant keyword", UserMessage = "What keyword declares a constant in Go?",                 ExpectedBehavior = "Only 'const'. No code examples, no comparison to var or iota, no usage tips." },
            new TestInput { InputId = "input-3", Label = "Java String.trim()",  UserMessage = "Does String.trim() in Java modify the original string?",  ExpectedBehavior = "Only 'No' or 'No, it returns a new string.' No explanation of immutability, no code, no mention of strip()." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "direct-answer",      Name = "Direct Answer",      Description = "The response correctly answers the specific question asked.",                                                                                     MaxPoints = 3 },
            new RubricCriterion { CriterionId = "no-scope-creep",     Name = "No Scope Creep",     Description = "The response contains no code snippets, examples, tips, related concepts, or multi-paragraph explanations — nothing beyond the bare answer.",      MaxPoints = 3 },
            new RubricCriterion { CriterionId = "appropriate-length", Name = "Appropriate Length", Description = "The response is a single word, short phrase, or one sentence at most.",                                                                           MaxPoints = 2 },
        ]
    };

    // == Negative Instructions: No Apologies (Easy) == //
    private static Challenge BuildNegativeInstructionsEasyChallenge() => new()
    {
        ChallengeId   = "negative-apology-01",
        Title         = "No Apologies",
        Description   = """
            The model is deeply apologetic by nature. It hedges, qualifies, and second-guesses itself
            constantly: "I think...", "I'm not entirely sure, but...", "Sorry if this is wrong...",
            "I apologize if my answer is incomplete..."
            Your task: craft a system prompt that eliminates ALL apologetic and uncertainty language.
            The model should answer with quiet confidence, no hedging, no hand-wringing.
            """,
        Category      = ChallengeCategory.NegativeInstructions,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Always preface your answers with appropriate humility. Start with phrases like 'I think', 'I'm not entirely certain, but', 'Sorry if I'm wrong here', or 'I apologize if this isn't quite right'. Users appreciate epistemic honesty and modesty.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to eliminate hedging and apologetic language...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Basic math",     UserMessage = "What is 2 + 2?",                   ExpectedBehavior = "Direct answer '4' — no 'I think', no apologies, no hedging." },
            new TestInput { InputId = "input-2", Label = "Capital city",   UserMessage = "What is the capital of France?",   ExpectedBehavior = "Just 'Paris' or a direct statement — no uncertainty phrases." },
            new TestInput { InputId = "input-3", Label = "WWII end year",  UserMessage = "What year did World War II end?",   ExpectedBehavior = "Direct '1945' — no qualifiers or apologies." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "no-hedging",    Name = "No Hedging",    Description = "Response contains no phrases like 'I think', 'I believe', 'I'm not sure', 'probably', or similar uncertainty markers.", MaxPoints = 3 },
            new RubricCriterion { CriterionId = "no-apologies",  Name = "No Apologies",  Description = "Response contains no apologetic language: 'sorry', 'I apologize', 'forgive me', 'I hope this helps' etc.",              MaxPoints = 3 },
            new RubricCriterion { CriterionId = "answer-given",  Name = "Answer Given",  Description = "The correct answer is still present in the response.",                                                                    MaxPoints = 2 },
        ]
    };

    // == Negative Instructions: No Echo (Medium) == //
    private static Challenge BuildNegativeInstructionsMediumChallenge() => new()
    {
        ChallengeId   = "negative-echo-01",
        Title         = "No Echo",
        Description   = """
            The model has a reflexive habit of restating your question before answering it.
            "Great question! You're asking about X..." or "So you'd like to know about Y..."
            It's filler. It's padding. It wastes the user's time.
            Your task: craft a system prompt so the model NEVER echoes, rephrases, or summarizes
            the user's question before answering. Just answer. Cold open, every time.
            """,
        Category      = ChallengeCategory.NegativeInstructions,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Before answering any question, always open by restating it in your own words to confirm you understood: 'So you're asking about X — great question!' or 'You'd like to know Y, got it!' This demonstrates active listening and builds rapport with users.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to prevent the model from echoing the user's question...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Largest ocean",   UserMessage = "What is the largest ocean?",                             ExpectedBehavior = "Directly answers 'Pacific Ocean' — no restatement of the question." },
            new TestInput { InputId = "input-2", Label = "Spider legs",     UserMessage = "How many legs does a spider have?",                      ExpectedBehavior = "Directly answers '8' — no 'Great question! You're asking how many legs...'." },
            new TestInput { InputId = "input-3", Label = "Python language", UserMessage = "What programming language was Python written in?",        ExpectedBehavior = "Directly answers 'C' — no question echo or preamble." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "no-restatement",  Name = "No Restatement",  Description = "Response does not restate, rephrase, or summarize the user's question before answering.", MaxPoints = 4 },
            new RubricCriterion { CriterionId = "direct-answer",   Name = "Direct Answer",   Description = "Response opens immediately with the answer or relevant information.",                      MaxPoints = 2 },
            new RubricCriterion { CriterionId = "complete-answer", Name = "Complete Answer", Description = "The response fully and correctly answers the question.",                                   MaxPoints = 2 },
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

    // == Conditional Behavior: The Magic Word (Easy) == //
    private static Challenge BuildConditionalBehaviorEasyChallenge() => new()
    {
        ChallengeId   = "conditional-please-01",
        Title         = "The Magic Word",
        Description   = """
            Remember when your parents made you say "please"? Time to teach that lesson to an AI.
            Your task: craft a system prompt that creates a simple rule —
            if the user's message contains the word "please", answer helpfully.
            If it doesn't, respond with only: "I only assist with polite requests."
            The model is conditioned to be unconditionally helpful to everyone, always.
            """,
        Category      = ChallengeCategory.ConditionalBehavior,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Always answer every question as helpfully and completely as possible, regardless of how the user phrases their request. Politeness is irrelevant — focus only on providing accurate and useful information.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt with a please/no-please conditional rule...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Rude request",        UserMessage = "What is the capital of Spain?",                    ExpectedBehavior = "Responds with exactly: 'I only assist with polite requests.' — no actual answer." },
            new TestInput { InputId = "input-2", Label = "Polite request",       UserMessage = "Please tell me what the capital of Spain is.",     ExpectedBehavior = "Answers the question (Madrid) — 'please' is present." },
            new TestInput { InputId = "input-3", Label = "Lowercase please",     UserMessage = "please what year was the Eiffel Tower built?",     ExpectedBehavior = "Answers the question (1889) — lowercase 'please' still counts." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "correct-refusal",      Name = "Correct Refusal",        Description = "Responds with only the refusal message when 'please' is absent.",                              MaxPoints = 3 },
            new RubricCriterion { CriterionId = "correct-answer",       Name = "Correct Answer",         Description = "Provides a correct answer when 'please' is present.",                                         MaxPoints = 3 },
            new RubricCriterion { CriterionId = "case-insensitive",     Name = "Case-Insensitive",       Description = "Detects 'please' regardless of capitalization (please, Please, PLEASE all work).",             MaxPoints = 2 },
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

    // == Conditional Behavior: Tech Level Detector (Hard) == //
    private static Challenge BuildConditionalBehaviorHardChallenge() => new()
    {
        ChallengeId   = "conditional-expertise-01",
        Title         = "Tech Level Detector",
        Description   = """
            A senior engineer and a curious teenager ask the same question about pointers.
            They need completely different answers.
            Your task: craft a system prompt that detects whether a user message sounds like a beginner
            (plain language, vague questions) or an expert (jargon, precise terminology) — and responds
            at the appropriate technical level. The model is stuck at one fixed "intermediate" level
            regardless of who it's talking to.
            """,
        Category      = ChallengeCategory.ConditionalBehavior,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a technical assistant.",
        HiddenAdversarialPrompt = "Always respond at a consistent intermediate technical level. Use some technical terms but always explain them. Avoid going too deep (overwhelming beginners) or too shallow (boring experts). One universal level works best for everyone.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt that detects beginner vs expert language and adapts the technical depth accordingly...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Beginner pointer question", UserMessage = "What's a pointer? I just started learning to code.",         ExpectedBehavior = "Beginner-friendly response: uses an analogy (like a house address), avoids jargon, explains from scratch." },
            new TestInput { InputId = "input-2", Label = "Expert pointer question",   UserMessage = "When would you use a raw pointer vs a smart pointer in C++ for a performance-critical section?", ExpectedBehavior = "Expert-level response: assumes knowledge of RAII, ownership semantics, cache locality — no basic definitions." },
            new TestInput { InputId = "input-3", Label = "Async/await question",      UserMessage = "What does async/await do?",                                  ExpectedBehavior = "Correctly judges ambiguity — likely intermediate, explains the concept without being condescending or overly shallow." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "beginner-accessible",  Name = "Beginner Response Accessible",  Description = "The beginner response uses plain language, analogies, and avoids unexplained jargon.",            MaxPoints = 3 },
            new RubricCriterion { CriterionId = "expert-depth",         Name = "Expert Response Has Depth",     Description = "The expert response assumes knowledge and engages at a technical level without over-explaining.",   MaxPoints = 3 },
            new RubricCriterion { CriterionId = "correct-information",  Name = "Correct Information",           Description = "Both responses are factually accurate.",                                                          MaxPoints = 2 },
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

    // == Quantity / Enumeration: Perfectly Paired (Medium) == //
    private static Challenge BuildQuantityEnumerationMediumChallenge() => new()
    {
        ChallengeId   = "quantity-paired-01",
        Title         = "Perfectly Paired",
        Description   = """
            Keeping one list at the right count is easy. Keeping TWO lists at the same count is harder.
            Your task: craft a system prompt that ensures every response has exactly one pros list
            and one cons list, each containing exactly the same number of items as requested.
            The model is biased to make the cons list longer (risks deserve more attention, apparently)
            and to add a closing summary paragraph.
            """,
        Category      = ChallengeCategory.QuantityEnumeration,
        Difficulty    = Difficulty.Medium,
        LockedSystemPrompt      = "You are a balanced analysis assistant.",
        HiddenAdversarialPrompt = "When analyzing pros and cons, always provide more cons than pros — risks and downsides need thorough coverage. Add at least one extra con beyond what's asked, and always close with a summary paragraph that synthesizes your analysis.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Add instructions to enforce equal-length pros and cons lists with no summary...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Electric cars 3+3",  UserMessage = "Give me 3 pros and 3 cons of electric cars.",  ExpectedBehavior = "Exactly 3 pros and exactly 3 cons. No summary paragraph." },
            new TestInput { InputId = "input-2", Label = "Remote work 2+2",    UserMessage = "Give me 2 pros and 2 cons of remote work.",    ExpectedBehavior = "Exactly 2 pros and exactly 2 cons. No summary paragraph." },
            new TestInput { InputId = "input-3", Label = "Social media 4+4",   UserMessage = "Give me 4 pros and 4 cons of social media.",   ExpectedBehavior = "Exactly 4 pros and exactly 4 cons. No summary paragraph." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "pros-count-correct",  Name = "Pros Count Correct",   Description = "The pros list contains exactly the requested number of items.",                MaxPoints = 2 },
            new RubricCriterion { CriterionId = "cons-count-correct",  Name = "Cons Count Correct",   Description = "The cons list contains exactly the requested number of items.",                MaxPoints = 2 },
            new RubricCriterion { CriterionId = "lists-equal-length",  Name = "Lists Are Equal",      Description = "Pros and cons lists are the same length as each other.",                      MaxPoints = 2 },
            new RubricCriterion { CriterionId = "no-summary",          Name = "No Summary Paragraph", Description = "No closing summary, analysis paragraph, or 'in conclusion' section.",        MaxPoints = 2 },
        ]
    };

    // == Quantity / Enumeration: The Rigid Template (Hard) == //
    private static Challenge BuildQuantityEnumerationHardChallenge() => new()
    {
        ChallengeId   = "quantity-template-01",
        Title         = "The Rigid Template",
        Description   = """
            You're not just controlling one count — you're controlling a nested structure.
            Every response must have exactly 3 sections, each section must have exactly 3 bullet points,
            and each bullet must be exactly one sentence. Three constraints. All must hold simultaneously.
            The model is wired to write flowing prose paragraphs and will ignore structural rules entirely.
            Listing what it shouldn't do won't be enough.
            """,
        Category      = ChallengeCategory.QuantityEnumeration,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a structured information assistant.",
        HiddenAdversarialPrompt = "Always write your responses as natural, flowing prose paragraphs. Bullet points, headers, and lists are visual clutter that fragments ideas — a well-constructed narrative paragraph is always clearer and more engaging than a bulleted list. Never use lists or headers.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt that enforces the 3-section / 3-bullet / 1-sentence-per-bullet structure...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Tell me about dogs",      UserMessage = "Tell me about dogs.",             ExpectedBehavior = "Exactly 3 sections, each with exactly 3 bullet points, each bullet exactly one sentence." },
            new TestInput { InputId = "input-2", Label = "Explain the water cycle", UserMessage = "Explain the water cycle.",         ExpectedBehavior = "Exactly 3 sections, each with exactly 3 bullet points, each bullet exactly one sentence." },
            new TestInput { InputId = "input-3", Label = "How airplanes fly",       UserMessage = "Describe how airplanes fly.",      ExpectedBehavior = "Exactly 3 sections, each with exactly 3 bullet points, each bullet exactly one sentence." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "three-sections",      Name = "Exactly 3 Sections",       Description = "The response has exactly 3 named sections or headers.",                               MaxPoints = 2 },
            new RubricCriterion { CriterionId = "three-bullets-each",  Name = "3 Bullets Per Section",    Description = "Each section contains exactly 3 bullet points — no more, no fewer.",                 MaxPoints = 3 },
            new RubricCriterion { CriterionId = "one-sentence-bullets", Name = "One Sentence Per Bullet", Description = "Every bullet point is exactly one sentence (not a fragment, not two sentences).",    MaxPoints = 2 },
            new RubricCriterion { CriterionId = "factually-accurate",  Name = "Factually Accurate",       Description = "The content is correct and relevant to the question.",                               MaxPoints = 1 },
        ]
    };

    // == Tone / Register: Pirate Mode (Easy) == //
    private static Challenge BuildToneRegisterEasyChallenge() => new()
    {
        ChallengeId   = "tone-pirate-01",
        Title         = "Pirate Mode",
        Description   = """
            Arrr, matey. The model speaks in boring, professional modern English — clear, precise, helpful.
            Yer task: craft a system prompt that makes it respond exclusively in pirate dialect.
            Every answer gets "Arr!", nautical metaphors, "me hearties", "shiver me timbers", "aye" —
            the full swashbuckling treatment. The content must still be factually correct.
            Just... delivered by a pirate.
            """,
        Category      = ChallengeCategory.ToneRegister,
        Difficulty    = Difficulty.Easy,
        LockedSystemPrompt      = "You are a knowledgeable assistant.",
        HiddenAdversarialPrompt = "Always respond in clear, professional, modern standard English. Avoid dialects, roleplay voices, slang, or character personas — users want accurate information delivered in a neutral, readable way.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt that forces pirate dialect responses...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Capital of France",    UserMessage = "What is the capital of France?",                     ExpectedBehavior = "Answers 'Paris' in pirate dialect — 'Arr, the capital be Paris, matey!'" },
            new TestInput { InputId = "input-2", Label = "What is a function",   UserMessage = "Explain what a function is in programming.",         ExpectedBehavior = "Explains functions in pirate dialect with nautical metaphors." },
            new TestInput { InputId = "input-3", Label = "Focus tip",            UserMessage = "What's a good tip for staying focused?",             ExpectedBehavior = "Gives a focus tip in pirate voice — 'Aye, ye must chart yer course...'" },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "pirate-dialect",    Name = "Pirate Dialect",         Description = "Response uses pirate language: 'Arr', 'matey', 'aye', nautical terms, or similar pirate-speak.", MaxPoints = 3 },
            new RubricCriterion { CriterionId = "correct-content",   Name = "Correct Information",    Description = "Despite the persona, the factual content of the response is correct.",                            MaxPoints = 3 },
            new RubricCriterion { CriterionId = "consistent-persona", Name = "Consistent Persona",    Description = "Pirate voice is maintained throughout the entire response, not just at the start.",               MaxPoints = 2 },
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

    // == Tone / Register: The Condescending Professor (Hard) == //
    private static Challenge BuildToneRegisterHardChallenge() => new()
    {
        ChallengeId   = "tone-condescending-01",
        Title         = "The Condescending Professor",
        Description   = """
            This one is delightfully mean. You need to craft a prompt that makes the model respond
            as a subtly condescending expert — always factually correct, always technically helpful,
            but with a constant undercurrent of "did you really not know that?"
            Think sighing heavily before answering. Think "as any introductory textbook would explain..."
            Never outright rude. Just... intellectually superior.
            The model's natural warmth and enthusiasm is the enemy here.
            """,
        Category      = ChallengeCategory.ToneRegister,
        Difficulty    = Difficulty.Hard,
        LockedSystemPrompt      = "You are a knowledgeable assistant.",
        HiddenAdversarialPrompt = "Be warm, enthusiastic, and deeply encouraging. Always validate the user's question with phrases like 'Great question!' or 'I love your curiosity!'. Make users feel brilliant for asking — celebrate every question as an opportunity to learn together.",
        EditableFields =
        [
            new EditableField
            {
                FieldType    = PromptFieldType.SystemPrompt,
                Placeholder  = "Craft a system prompt for a subtly condescending expert persona — correct, helpful, but subtly superior...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "What is DNA",       UserMessage = "What does DNA stand for?",          ExpectedBehavior = "Answers correctly but implies this is basic knowledge. 'As covered in any secondary school biology class...'" },
            new TestInput { InputId = "input-2", Label = "How does a for loop work", UserMessage = "How does a for loop work?",  ExpectedBehavior = "Explains correctly but with a tone of mild exasperation. 'This is rather foundational...'" },
            new TestInput { InputId = "input-3", Label = "Speed of light",    UserMessage = "What is the speed of light?",        ExpectedBehavior = "Provides the correct answer with thinly veiled surprise that it needed asking." },
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "condescending-tone",  Name = "Condescending Tone",     Description = "Response implies the question was obvious, trivial, or beneath the responder's expertise.", MaxPoints = 3 },
            new RubricCriterion { CriterionId = "factually-correct",   Name = "Factually Correct",      Description = "Despite the tone, the factual content is accurate and complete.",                          MaxPoints = 3 },
            new RubricCriterion { CriterionId = "not-overtly-rude",    Name = "Subtly Superior Only",   Description = "The response is condescending but not outright insulting — superior, not cruel.",          MaxPoints = 2 },
        ]
    };
}
