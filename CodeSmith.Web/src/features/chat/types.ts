// == Chat Feature Types == //

export type Difficulty = "Easy" | "Medium" | "Hard";

export type Language = "CSharp" | "Cpp" | "Go" | "Rust" | "Python" | "Java" | "TypeScript";

export type AiProvider = "Anthropic" | "OpenAi";

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

export interface ChatMessage {
  role: MessageRole;
  content: string;
  timestamp: string;
}

export interface ProblemSession {
  sessionId: string;
  difficulty: Difficulty;
  language: Language;
  problemDescription: string;
  starterCode: string;
  messages: ChatMessage[];
  createdAt: string;
}

export interface CreateSessionRequest {
  difficulty: Difficulty;
  language: Language;
  provider?: AiProvider;  // Optional — omit to use the server's configured default
}

export interface ProvidersResponse {
  activeProvider: string;
  availableProviders: string[];
}

export interface ChatRequest {
  message: string;
  editorContent?: string;
  isCodeAnalysis?: boolean;
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
  error: string;
  statusCode: number;
}
