// == API Client == //
import type {
  CreateSessionRequest,
  ProblemSession,
  ChatRequest,
  ChatResponse,
  RunCodeRequest,
  RunCodeResponse,
  ProvidersResponse,
  ApiError,
} from "../features/chat/types";
import type {
  ChallengeResponse,
  StartChallengeRequest,
  SubmitAttemptRequest,
  PromptLabSession,
  AttemptResult,
} from "../features/prompt-lab/types";

class ApiClientError extends Error {
  statusCode: number;
  apiError: ApiError;

  constructor(statusCode: number, apiError: ApiError) {
    super(apiError.error);
    this.name = "ApiClientError";
    this.statusCode = statusCode;
    this.apiError = apiError;
  }
}

async function request<T>(url: string, options: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  });

  if (!response.ok) {
    const errorBody = (await response.json()) as ApiError;
    throw new ApiClientError(response.status, errorBody);
  }

  return response.json() as Promise<T>;
}

export function getProviders(): Promise<ProvidersResponse> {
  return request<ProvidersResponse>("/api/providers", { method: "GET" });
}

export function createSession(body: CreateSessionRequest): Promise<ProblemSession> {
  return request<ProblemSession>("/api/session", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function sendMessage(sessionId: string, body: ChatRequest): Promise<ChatResponse> {
  return request<ChatResponse>(`/api/session/${sessionId}/chat`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function runCode(sessionId: string, body: RunCodeRequest): Promise<RunCodeResponse> {
  return request<RunCodeResponse>(`/api/session/${sessionId}/run`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

// == Prompt Lab API Functions == //

export function getChallenges(): Promise<ChallengeResponse[]> {
  return request<ChallengeResponse[]>("/api/prompt-lab/challenges", { method: "GET" });
}

export function getChallenge(challengeId: string): Promise<ChallengeResponse> {
  return request<ChallengeResponse>(`/api/prompt-lab/challenges/${challengeId}`, { method: "GET" });
}

export function startPromptLabChallenge(body: StartChallengeRequest): Promise<PromptLabSession> {
  return request<PromptLabSession>("/api/prompt-lab/sessions", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function submitPromptLabAttempt(sessionId: string, body: SubmitAttemptRequest): Promise<AttemptResult> {
  return request<AttemptResult>(`/api/prompt-lab/sessions/${sessionId}/submit`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export { ApiClientError };
