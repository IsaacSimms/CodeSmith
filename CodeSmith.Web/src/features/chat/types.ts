// == Chat Feature Types == //

export type Difficulty = "Easy" | "Medium" | "Hard";

export type MessageRole = "User" | "Assistant";

export interface ChatMessage {
  role: MessageRole;
  content: string;
  timestamp: string;
}

export interface ProblemSession {
  sessionId: string;
  difficulty: Difficulty;
  problemDescription: string;
  starterCode: string;
  messages: ChatMessage[];
  createdAt: string;
}

export interface CreateSessionRequest {
  difficulty: Difficulty;
}

export interface ChatRequest {
  message: string;
}

export interface ChatResponse {
  response: string;
}

export interface ApiError {
  error: string;
  statusCode: number;
}
