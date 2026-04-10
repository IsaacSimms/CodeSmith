// == Chat Feature Types == //

export type Difficulty = "Easy" | "Medium" | "Hard";

export type Language = "CSharp" | "Cpp" | "Go" | "Rust" | "Python" | "Java";

export type MessageRole = "User" | "Assistant";

// == Language Display Labels == //
export const languageLabels: Record<Language, string> = {
  CSharp: "C#",
  Cpp:    "C++",
  Go:     "Go",
  Rust:   "Rust",
  Python: "Python",
  Java:   "Java",
};

// == Monaco Editor Language IDs == //
export const monacoLanguageIds: Record<Language, string> = {
  CSharp: "csharp",
  Cpp:    "cpp",
  Go:     "go",
  Rust:   "rust",
  Python: "python",
  Java:   "java",
};

export function isLanguage(value: string | null | undefined): value is Language {
  return value === "CSharp" || value === "Cpp" || value === "Go" || value === "Rust" || value === "Python" || value === "Java";
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
}

export interface ChatRequest {
  message: string;
  editorContent?: string;
}

export interface ChatResponse {
  response: string;
}

export interface ApiError {
  error: string;
  statusCode: number;
}
