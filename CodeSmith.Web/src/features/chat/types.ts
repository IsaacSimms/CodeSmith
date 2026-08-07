// == Chat Feature Types == //

export type Difficulty = "Easy" | "Medium" | "Hard";

export type Language = "CSharp" | "Cpp" | "Go" | "Rust" | "Python" | "Java" | "TypeScript";

export type AiProvider = "Anthropic" | "OpenAi" | "Xai";

// == Problem Variety == //
// Two independent axes the backend rolls when left on "Random": Focus is the kind of work the
// problem asks for, Topic is what it is about.

export type ProblemFocus =
  | "Random"
  | "Standard"
  | "BugFix"
  | "PerformanceOptimization"
  | "FeatureExtension"
  | "UnusualConstraints"
  | "EdgeCaseGauntlet"
  | "RealWorldScenario"
  | "Refactoring";

export type ProblemTopic =
  | "Random"
  | "ArraysAndStrings"
  | "HashMapsAndSets"
  | "TreesAndGraphs"
  | "DynamicProgramming"
  | "ObjectOrientedDesign"
  | "FunctionalPatternsAndRecursion"
  | "SimulationAndModeling"
  | "MathAndNumberTheory"
  | "StateMachines"
  | "ParsingAndStringProcessing"
  | "BitManipulation"
  | "SortingAndSearching";

export const problemFocusLabels: Record<ProblemFocus, string> = {
  Random:                  "Random",
  Standard:                "Standard implementation",
  BugFix:                  "Bug fix",
  PerformanceOptimization: "Performance optimization",
  FeatureExtension:        "Feature extension",
  UnusualConstraints:      "Unusual constraints",
  EdgeCaseGauntlet:        "Edge-case gauntlet",
  RealWorldScenario:       "Real-world scenario",
  Refactoring:             "Refactoring",
};

export const problemTopicLabels: Record<ProblemTopic, string> = {
  Random:                         "Random",
  ArraysAndStrings:               "Arrays & strings",
  HashMapsAndSets:                "Hash maps & sets",
  TreesAndGraphs:                 "Trees & graphs",
  DynamicProgramming:             "Dynamic programming",
  ObjectOrientedDesign:           "Object-oriented design",
  FunctionalPatternsAndRecursion: "Functional patterns & recursion",
  SimulationAndModeling:          "Simulation & modeling",
  MathAndNumberTheory:            "Math & number theory",
  StateMachines:                  "State machines",
  ParsingAndStringProcessing:     "Parsing & string processing",
  BitManipulation:                "Bit manipulation",
  SortingAndSearching:            "Sorting & searching",
};

// Option lists and guards both derive from the label maps, so adding a member cannot leave a
// dropdown entry or a URL guard behind
export const problemFocuses = Object.keys(problemFocusLabels) as ProblemFocus[];
export const problemTopics = Object.keys(problemTopicLabels) as ProblemTopic[];

export type GuidanceMode = "Guidance" | "CodeAnalysis";

export type MessageRole = "User" | "Assistant";

// == Language Display Labels == //
export const languageLabels: Record<Language, string> = {
  CSharp: "C#",
  Cpp:    "C++",
  Go:     "Go",
  Rust:   "Rust",
  Python: "Python",
  Java:   "Java",
  TypeScript: "TypeScript",
};

// == Monaco Editor Language IDs == //
export const monacoLanguageIds: Record<Language, string> = {
  CSharp: "csharp",
  Cpp:    "cpp",
  Go:     "go",
  Rust:   "rust",
  Python: "python",
  Java:   "java",
  TypeScript: "typescript",
};

export function isLanguage(value: string | null | undefined): value is Language {
  return value === "CSharp" || value === "Cpp" || value === "Go" || value === "Rust" || value === "Python" || value === "Java" || value === "TypeScript";
}

export function isDifficulty(value: string | null | undefined): value is Difficulty {
  return value === "Easy" || value === "Medium" || value === "Hard";
}

export function isProblemFocus(value: string | null | undefined): value is ProblemFocus {
  return value != null && Object.prototype.hasOwnProperty.call(problemFocusLabels, value);
}

export function isProblemTopic(value: string | null | undefined): value is ProblemTopic {
  return value != null && Object.prototype.hasOwnProperty.call(problemTopicLabels, value);
}

export interface ChatMessage {
  role: MessageRole;
  content: string;
  timestamp: string;
}

export interface ProblemSession {
  sessionId: string;
  difficulty: Difficulty;
  language: Language;
  focus: ProblemFocus;   // Resolved server-side — never "Random" on a returned session
  topic: ProblemTopic;   // Resolved server-side — never "Random" on a returned session
  problemDescription: string;
  starterCode: string;
  messages: ChatMessage[];
  createdAt: string;
}

export interface CreateSessionRequest {
  difficulty: Difficulty;
  language: Language;
  provider?: AiProvider; // omit → server applies ActiveProvider (bounded fallback path)
  focus: ProblemFocus;   // "Random" lets the server roll one
  topic: ProblemTopic;   // "Random" lets the server roll one
}

export interface ProvidersResponse {
  activeProvider: string;
  availableProviders: string[];
}

export interface ChatRequest {
  message: string;
  editorContent?: string;
  guidanceMode?: GuidanceMode;
}

export interface ChatResponse {
  response: string;
  contextTokensUsed: number;  // Input tokens this turn — grows with conversation history
  contextWindowSize: number;  // Model context window limit (200,000 for all current models)
}

export interface RunCodeRequest {
  code: string;
  language: Language;
}

export interface RunCodeResponse {
  stdout: string;
  stderr: string;
  exitCode: number;
  timedOut: boolean;
}

export interface ApiError {
  title?: string;   // RFC 7807 short description
  detail?: string;  // Safe, human-readable error message
  status: number;   // HTTP status code
  code?: string;    // Optional machine code (e.g. login_required from ProblemDetails extensions)
}
