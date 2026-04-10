// == API Client Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { createSession, sendMessage, ApiClientError } from "./apiClient";

beforeEach(() => {
  vi.restoreAllMocks();
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

    const result = await createSession({ difficulty: "Easy", language: "CSharp" });

    expect(fetch).toHaveBeenCalledWith("/api/session", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ difficulty: "Easy", language: "CSharp" }),
    });
    expect(result).toEqual(mockSession);
  });

  it("throws ApiClientError on non-ok response", async () => {
    const errorBody = { error: "Invalid difficulty", statusCode: 400 };

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve(errorBody),
      })
    );

    try {
      await createSession({ difficulty: "Easy", language: "CSharp" });
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
    const errorBody = { error: "Session not found", statusCode: 404 };

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
