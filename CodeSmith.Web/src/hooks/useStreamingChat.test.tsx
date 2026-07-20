// == Streaming Chat Hook Tests == //
import { describe, it, expect, vi } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useStreamingChat } from "./useStreamingChat";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe("useStreamingChat", () => {
  it("appends the user message immediately and the assistant reply on success", async () => {
    const streamTurn = vi.fn().mockResolvedValue({ response: "Try a for loop" });
    const { result } = renderHook(() => useStreamingChat(streamTurn), { wrapper: createWrapper() });

    act(() => result.current.sendMessage("How do I start?"));

    expect(result.current.messages).toHaveLength(1);
    expect(result.current.messages[0]).toMatchObject({ role: "User", content: "How do I start?" });

    await waitFor(() => expect(result.current.messages).toHaveLength(2));
    expect(result.current.messages[1]).toMatchObject({ role: "Assistant", content: "Try a for loop" });
  });

  it("accumulates deltas into streamingText while the turn is in flight", async () => {
    let resolveReply!: (reply: { response: string }) => void;
    const streamTurn = vi.fn().mockImplementation((_message: string, onDelta: (delta: string) => void) => {
      onDelta("Try a ");
      onDelta("for loop");
      return new Promise<{ response: string }>((resolve) => {
        resolveReply = resolve;
      });
    });
    const { result } = renderHook(() => useStreamingChat(streamTurn), { wrapper: createWrapper() });

    act(() => result.current.sendMessage("How do I start?"));

    await waitFor(() => expect(result.current.streamingText).toBe("Try a for loop"));
    expect(result.current.isSending).toBe(true);

    act(() => resolveReply({ response: "Try a for loop" }));
    await waitFor(() => expect(result.current.isSending).toBe(false));
  });

  it("rolls the turn back on failure: user bubble dropped, partial kept, draft restored", async () => {
    const streamTurn = vi.fn().mockImplementation(async (_message: string, onDelta: (delta: string) => void) => {
      onDelta("half a hint");
      throw new Error("AI service error");
    });
    const { result } = renderHook(() => useStreamingChat(streamTurn), { wrapper: createWrapper() });

    act(() => result.current.sendMessage("How do I start?"));

    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());
    expect(result.current.messages).toHaveLength(0);   // optimistic user bubble rolled back
    expect(result.current.failedTurn).toEqual({ partial: "half a hint", message: "AI service error" });
    expect(result.current.draft).toEqual({ text: "How do I start?" });
  });

  it("clears the failed turn and draft when the next turn starts", async () => {
    const streamTurn = vi
      .fn()
      .mockRejectedValueOnce(new Error("AI service error"))
      .mockResolvedValueOnce({ response: "Second try worked" });
    const { result } = renderHook(() => useStreamingChat(streamTurn), { wrapper: createWrapper() });

    act(() => result.current.sendMessage("How do I start?"));
    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());

    act(() => result.current.sendMessage("How do I start?"));

    expect(result.current.failedTurn).toBeNull();
    expect(result.current.draft).toBeNull();
    await waitFor(() => expect(result.current.messages).toHaveLength(2));
  });

  it("resetChat seeds the transcript and clears failed-turn remains", async () => {
    const streamTurn = vi.fn().mockRejectedValue(new Error("AI service error"));
    const { result } = renderHook(() => useStreamingChat(streamTurn), { wrapper: createWrapper() });

    act(() => result.current.sendMessage("How do I start?"));
    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());

    const seed = [{ role: "User" as const, content: "Earlier message", timestamp: "2026-07-19T00:00:00Z" }];
    act(() => result.current.resetChat(seed));

    expect(result.current.messages).toEqual(seed);
    expect(result.current.failedTurn).toBeNull();
    expect(result.current.draft).toBeNull();

    act(() => result.current.resetChat());
    expect(result.current.messages).toEqual([]);
  });

  it("passes per-send context to streamTurn and reports the reply through onSuccess", async () => {
    const streamTurn = vi.fn().mockResolvedValue({ response: "ok", contextTokensUsed: 42 });
    const onSuccess = vi.fn();
    const { result } = renderHook(
      () => useStreamingChat<{ response: string; contextTokensUsed: number }, string>(streamTurn),
      { wrapper: createWrapper() }
    );

    act(() => result.current.sendMessage("analyze this", { context: "CodeAnalysis", onSuccess }));

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith({ response: "ok", contextTokensUsed: 42 }));
    expect(streamTurn).toHaveBeenCalledWith("analyze this", expect.any(Function), "CodeAnalysis");
  });
});
