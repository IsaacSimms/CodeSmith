// == API Client Tests == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  createSession,
  sendMessage,
  ApiClientError,
  setAccessTokenProvider,
  resolveApiUrl,
} from "./apiClient";

beforeEach(() => {
  vi.restoreAllMocks();
  setAccessTokenProvider(null);
  vi.unstubAllEnvs();
});

afterEach(() => {
  setAccessTokenProvider(null);
  vi.unstubAllEnvs();
});

describe("resolveApiUrl", () => {
  it("returns relative path when VITE_API_BASE_URL is unset", () => {
    expect(resolveApiUrl("/api/session")).toBe("/api/session");
  });

  it("prefixes absolute base URL and strips trailing slash", () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://api.example.com/");
    expect(resolveApiUrl("/api/session")).toBe("https://api.example.com/api/session");
  });
});

describe("createSession", () => {
  it("sends POST to /api/session with difficulty and returns ProblemSession", async () => {
    const mockSession = {
      sessionId: "abc-123",
      difficulty: "Easy",
      language: "CSharp",
      problemDescription: "Write a function",
      starterCode: "public void Solve() {}",
      messages: [],
      createdAt: "2026-03-31T00:00:00Z",
    };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSession),
      })
    );

    const result = await createSession({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" });

    expect(fetch).toHaveBeenCalledWith("/api/session", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" }),
    });
    expect(result).toEqual(mockSession);
  });

  it("attaches Bearer token when access token provider returns a token", async () => {
    setAccessTokenProvider(async () => "test-access-token");

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () =>
          Promise.resolve({
            sessionId: "abc-123",
            difficulty: "Easy",
            language: "CSharp",
            problemDescription: "p",
            starterCode: "",
            messages: [],
            createdAt: "2026-03-31T00:00:00Z",
          }),
      })
    );

    await createSession({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" });

    expect(fetch).toHaveBeenCalledWith("/api/session", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer test-access-token",
      },
      body: JSON.stringify({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" }),
    });
  });

  it("omits Authorization when token provider returns null", async () => {
    setAccessTokenProvider(async () => null);

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () =>
          Promise.resolve({
            sessionId: "abc-123",
            difficulty: "Easy",
            language: "CSharp",
            problemDescription: "p",
            starterCode: "",
            messages: [],
            createdAt: "2026-03-31T00:00:00Z",
          }),
      })
    );

    await createSession({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" });

    expect(fetch).toHaveBeenCalledWith("/api/session", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" }),
    });
  });

  it("uses absolute URL when VITE_API_BASE_URL is set", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://ca-codesmith-api-001.example.azurecontainerapps.io");

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () =>
          Promise.resolve({
            sessionId: "abc-123",
            difficulty: "Easy",
            language: "CSharp",
            problemDescription: "p",
            starterCode: "",
            messages: [],
            createdAt: "2026-03-31T00:00:00Z",
          }),
      })
    );

    await createSession({ difficulty: "Easy", language: "CSharp", provider: "Xai" });

    expect(fetch).toHaveBeenCalledWith(
      "https://ca-codesmith-api-001.example.azurecontainerapps.io/api/session",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("throws ApiClientError on non-ok response", async () => {
    const errorBody = { title: "Bad Request", detail: "Invalid difficulty", status: 400 };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve(errorBody),
      })
    );

    try {
      await createSession({ difficulty: "Easy", language: "CSharp", provider: "Anthropic" });
      expect.fail("Should have thrown");
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError);
      const apiErr = err as ApiClientError;
      expect(apiErr.statusCode).toBe(400);
      expect(apiErr.apiError).toEqual(errorBody);
      expect(apiErr.message).toBe("Invalid difficulty");
    }
  });
});

describe("sendMessage", () => {
  it("sends POST to /api/session/{id}/chat and returns ChatResponse", async () => {
    const mockResponse = { response: "Try using a loop" };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockResponse),
      })
    );

    const result = await sendMessage("abc-123", { message: "Help me", editorContent: "int x = 1;" });

    expect(fetch).toHaveBeenCalledWith("/api/session/abc-123/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: "Help me", editorContent: "int x = 1;" }),
    });
    expect(result).toEqual(mockResponse);
  });

  it("throws ApiClientError when session not found", async () => {
    const errorBody = { title: "Session not found", detail: "Session 'bad-id' not found.", status: 404 };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: () => Promise.resolve(errorBody),
      })
    );

    try {
      await sendMessage("bad-id", { message: "Hello" });
      expect.fail("Should have thrown");
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError);
      const apiErr = err as ApiClientError;
      expect(apiErr.statusCode).toBe(404);
    }
  });
});
