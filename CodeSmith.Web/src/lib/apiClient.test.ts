// == API Client Tests == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  createSession,
  sendMessage,
  streamChat,
  streamCreateSession,
  ApiClientError,
  setAccessTokenProvider,
  resolveApiUrl,
} from "./apiClient";

// == Hermetic env: ambient VITE_API_BASE_URL (CI deploy vars, local .env) must not leak into URL asserts == //
beforeEach(() => {
  vi.restoreAllMocks();
  setAccessTokenProvider(null);
  vi.unstubAllEnvs();
  vi.stubEnv("VITE_API_BASE_URL", "");
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

// == NDJSON Streaming == //

// Builds a Response-like whose body streams the given NDJSON lines (each chunk may split lines
// arbitrarily server-side, so the reader must reassemble on newlines — exercised below).
function ndjsonResponse(chunks: string[]): unknown {
  const encoder = new TextEncoder();
  return {
    ok: true,
    body: new ReadableStream<Uint8Array>({
      start(controller) {
        for (const chunk of chunks) controller.enqueue(encoder.encode(chunk));
        controller.close();
      },
    }),
  };
}

describe("streamChat", () => {
  it("delivers deltas in order and resolves with the final event's data", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        ndjsonResponse([
          '{"type":"delta","text":"Try a "}\n',
          '{"type":"delta","text":"for loop"}\n',
          '{"type":"final","data":{"response":"Try a for loop","contextTokensUsed":42,"contextWindowSize":200000}}\n',
        ])
      )
    );

    const deltas: string[] = [];
    const result = await streamChat("abc-123", { message: "Help" }, { onDelta: (t) => deltas.push(t) });

    expect(fetch).toHaveBeenCalledWith("/api/session/abc-123/chat/stream", expect.objectContaining({ method: "POST" }));
    expect(deltas).toEqual(["Try a ", "for loop"]);
    expect(result).toEqual({ response: "Try a for loop", contextTokensUsed: 42, contextWindowSize: 200000 });
  });

  it("reassembles events split across network chunks", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        ndjsonResponse([
          '{"type":"delta","te',                    // event split mid-key
          'xt":"Hel"}\n{"type":"delta","text":"lo"}', // two events sharing a chunk, second unterminated
          '\n{"type":"final","data":{"response":"Hello"}}\n',
        ])
      )
    );

    const deltas: string[] = [];
    const result = await streamChat("abc-123", { message: "Help" }, { onDelta: (t) => deltas.push(t) });

    expect(deltas).toEqual(["Hel", "lo"]);
    expect(result).toEqual({ response: "Hello" });
  });

  it("rejects with the mapped status code when an error event arrives mid-stream", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        ndjsonResponse([
          '{"type":"delta","text":"part"}\n',
          '{"type":"error","code":502,"message":"AI service error"}\n',
        ])
      )
    );

    const deltas: string[] = [];
    try {
      await streamChat("abc-123", { message: "Help" }, { onDelta: (t) => deltas.push(t) });
      expect.fail("Should have thrown");
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError);
      expect((err as ApiClientError).statusCode).toBe(502);
    }
    expect(deltas).toEqual(["part"]); // deltas before the failure still reached the caller
  });

  it("rejects when the stream ends without a final event", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(ndjsonResponse(['{"type":"delta","text":"part"}\n'])));

    await expect(streamChat("abc-123", { message: "Help" }, { onDelta: () => {} })).rejects.toBeInstanceOf(ApiClientError);
  });

  it("throws ApiClientError with the real status on a pre-stream failure", async () => {
    const errorBody = { title: "Insufficient quota or credits", detail: "Out of credits.", status: 402 };
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: false, status: 402, json: () => Promise.resolve(errorBody) })
    );

    try {
      await streamChat("abc-123", { message: "Help" }, { onDelta: () => {} });
      expect.fail("Should have thrown");
    } catch (err) {
      expect((err as ApiClientError).statusCode).toBe(402);
    }
  });
});

describe("streamCreateSession", () => {
  it("invokes onReset for reset events between delta batches", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        ndjsonResponse([
          '{"type":"delta","text":"half a prob"}\n',
          '{"type":"reset"}\n',
          '{"type":"delta","text":"Whole problem"}\n',
          '{"type":"final","data":{"sessionId":"abc-123","problemDescription":"Whole problem"}}\n',
        ])
      )
    );

    const events: string[] = [];
    const result = await streamCreateSession(
      { difficulty: "Easy", language: "Python", provider: "Xai" },
      { onDelta: (t) => events.push(`delta:${t}`), onReset: () => events.push("reset") }
    );

    expect(fetch).toHaveBeenCalledWith("/api/session/stream", expect.objectContaining({ method: "POST" }));
    expect(events).toEqual(["delta:half a prob", "reset", "delta:Whole problem"]);
    expect(result).toMatchObject({ sessionId: "abc-123" });
  });
});
