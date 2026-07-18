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

async function buildHeaders(options: RequestInit): Promise<Record<string, string>> {
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

  return headers;
}

async function request<T>(path: string, options: RequestInit): Promise<T> {
  const response = await fetch(resolveApiUrl(path), {
    ...options,
    headers: await buildHeaders(options),
  });

  if (!response.ok) {
    const errorBody = (await response.json()) as ApiError;
    throw new ApiClientError(response.status, errorBody);
  }

  return response.json() as Promise<T>;
}

// == NDJSON Streaming Core == //

// Callbacks for the incremental portion of a stream; the final payload is the returned promise
export interface StreamCallbacks {
  onDelta: (text: string) => void; // A chunk of assistant/description text arrived
  onReset?: () => void;            // Server abandoned the attempt (generation retry) — clear shown text
}

type NdjsonEvent =
  | { type: "delta"; text: string }
  | { type: "reset" }
  | { type: "final"; data: unknown }
  | { type: "error"; code: number; message: string };

// POSTs to a /stream endpoint and consumes its NDJSON body incrementally. Pre-stream failures
// arrive as normal HTTP errors; mid-stream failures arrive as an error event and are rethrown as
// ApiClientError with the status code the request would have had.
async function streamRequest<T>(path: string, body: unknown, callbacks: StreamCallbacks): Promise<T> {
  const response = await fetch(resolveApiUrl(path), {
    method: "POST",
    body: JSON.stringify(body),
    headers: await buildHeaders({}),
  });

  if (!response.ok) {
    const errorBody = (await response.json()) as ApiError;
    throw new ApiClientError(response.status, errorBody);
  }
  if (!response.body) {
    throw new ApiClientError(502, { title: "Streaming unavailable", detail: "The response had no readable body.", status: 502 });
  }

  let finalData: T | undefined;
  let sawFinal = false;

  const handleLine = (line: string): void => {
    if (!line.trim()) return;
    const event = JSON.parse(line) as NdjsonEvent;
    switch (event.type) {
      case "delta":
        callbacks.onDelta(event.text);
        break;
      case "reset":
        callbacks.onReset?.();
        break;
      case "final":
        finalData = event.data as T;
        sawFinal = true;
        break;
      case "error":
        throw new ApiClientError(event.code, { title: "Stream failed", detail: event.message, status: event.code });
    }
  };

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      let newlineIndex = buffer.indexOf("\n");
      while (newlineIndex >= 0) {
        handleLine(buffer.slice(0, newlineIndex));
        buffer = buffer.slice(newlineIndex + 1);
        newlineIndex = buffer.indexOf("\n");
      }
    }
  } finally {
    reader.releaseLock();
  }
  if (buffer.trim()) handleLine(buffer);

  if (!sawFinal) {
    // Connection dropped mid-stream without a final or error event — nothing settled server-side
    throw new ApiClientError(502, { title: "Stream interrupted", detail: "The stream ended before completing.", status: 502 });
  }
  return finalData as T;
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

// == Streaming Siblings == //

export function streamCreateSession(body: CreateSessionRequest, callbacks: StreamCallbacks): Promise<ProblemSession> {
  return streamRequest<ProblemSession>("/api/session/stream", body, callbacks);
}

export function streamChat(sessionId: string, body: ChatRequest, callbacks: StreamCallbacks): Promise<ChatResponse> {
  return streamRequest<ChatResponse>(`/api/session/${sessionId}/chat/stream`, body, callbacks);
}

export function streamPromptLabChat(sessionId: string, body: PromptLabChatRequest, callbacks: StreamCallbacks): Promise<PromptLabChatResponse> {
  return streamRequest<PromptLabChatResponse>(`/api/prompt-lab/sessions/${sessionId}/chat/stream`, body, callbacks);
}

export function streamSystemLabChat(sessionId: string, body: SystemLabChatRequest, callbacks: StreamCallbacks): Promise<SystemLabChatResponse> {
  return streamRequest<SystemLabChatResponse>(`/api/system-lab/sessions/${sessionId}/chat/stream`, body, callbacks);
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
