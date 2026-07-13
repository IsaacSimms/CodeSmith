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
  PromptLabChatRequest,
  PromptLabChatResponse,
} from "../features/prompt-lab/types";
import type {
  ScenarioResponse,
  StartSessionRequest,
  SubmitJustificationRequest,
  SystemLabChatRequest,
  SystemLabChatResponse,
  SystemLabSession,
  AttemptResult as SystemLabAttemptResult,
} from "../features/system-lab/types";

class ApiClientError extends Error {
  statusCode: number;
  apiError: ApiError;

  constructor(statusCode: number, apiError: ApiError) {
    super(apiError.detail ?? apiError.title ?? "An unexpected error occurred.");
    this.name = "ApiClientError";
    this.statusCode = statusCode;
    this.apiError = apiError;
  }
}

// == Access token seam (MSAL wires this at app bootstrap) == //
export type AccessTokenProvider = () => Promise<string | null | undefined>;

let accessTokenProvider: AccessTokenProvider | null = null;

export function setAccessTokenProvider(provider: AccessTokenProvider | null): void {
  accessTokenProvider = provider;
}

// == URL resolution (relative in local/dev; absolute when VITE_API_BASE_URL is set) == //
export function resolveApiUrl(path: string): string {
  const raw = import.meta.env.VITE_API_BASE_URL as string | undefined;
  const base = raw?.trim().replace(/\/$/, "") ?? "";
  if (!base) return path;
  return path.startsWith("/") ? `${base}${path}` : `${base}/${path}`;
}

async function request<T>(path: string, options: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };

  if (accessTokenProvider) {
    const token = await accessTokenProvider();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
  }

  const response = await fetch(resolveApiUrl(path), {
    ...options,
    headers,
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

export function sendPromptLabChat(sessionId: string, body: PromptLabChatRequest): Promise<PromptLabChatResponse> {
  return request<PromptLabChatResponse>(`/api/prompt-lab/sessions/${sessionId}/chat`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

// == System Lab API Functions == //

export function getScenarios(): Promise<ScenarioResponse[]> {
  return request<ScenarioResponse[]>("/api/system-lab/scenarios", { method: "GET" });
}

export function startSystemLabSession(body: StartSessionRequest): Promise<SystemLabSession> {
  return request<SystemLabSession>("/api/system-lab/sessions", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function submitSystemLabAttempt(sessionId: string, body: SubmitJustificationRequest): Promise<SystemLabAttemptResult> {
  return request<SystemLabAttemptResult>(`/api/system-lab/sessions/${sessionId}/submit`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function sendSystemLabChat(sessionId: string, body: SystemLabChatRequest): Promise<SystemLabChatResponse> {
  return request<SystemLabChatResponse>(`/api/system-lab/sessions/${sessionId}/chat`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export { ApiClientError };
