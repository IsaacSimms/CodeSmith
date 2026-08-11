// == Guidance Chat State Machine Tests == //
// The one client mirror of the server's whole-turn invariant: optimistic user append, assistant
// append on success, rollback + partial snapshot + draft restore on failure, settle invalidation.
import { describe, it, expect, vi } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { accountQueryKeys } from "../features/account/queryKeys";
import { useGuidanceChat } from "./useGuidanceChat";

interface TestMessage {
  role: "user" | "assistant";
  content: string;
}

interface TestResponse {
  response: string;
}

const config = {
  toUserMessage: (message: string): TestMessage => ({ role: "user", content: message }),
  toAssistantMessage: (r: TestResponse): TestMessage => ({ role: "assistant", content: r.response }),
};

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  queryClient.setQueryData(accountQueryKeys.quota, { freeTokensUsed: 0, freeQuotaMax: 20_000, ipConstraint: "None" });
  queryClient.setQueryData(accountQueryKeys.balance, { paidCreditsUsd: 5 });
  queryClient.setQueryData([...accountQueryKeys.ledger, 20], []);
  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

describe("useGuidanceChat", () => {
  it("appends the user message optimistically, then the assistant reply on success", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    let resolveTurn!: (r: TestResponse) => void;
    const turn = new Promise<TestResponse>((resolve) => { resolveTurn = resolve; });
    let sent!: Promise<TestResponse>;
    await act(async () => {
      sent = result.current.send("help me", () => turn);
    });

    // Optimistic: the user bubble is visible while the turn is in flight
    expect(result.current.messages).toEqual([{ role: "user", content: "help me" }]);
    expect(result.current.isPending).toBe(true);

    await act(async () => {
      resolveTurn({ response: "What have you tried?" });
      await sent;
    });

    await waitFor(() => expect(result.current.isPending).toBe(false));
    expect(result.current.messages).toEqual([
      { role: "user", content: "help me" },
      { role: "assistant", content: "What have you tried?" },
    ]);
    expect(result.current.failedTurn).toBeNull();
    expect(result.current.draft).toBeNull();
  });

  it("accumulates streamed deltas in streamingText and resets them on the next turn", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    await act(async () => {
      await result.current.send("first", async (onDelta) => {
        onDelta("What ");
        onDelta("have you tried?");
        return { response: "What have you tried?" };
      });
    });
    await waitFor(() => expect(result.current.streamingText).toBe("What have you tried?"));

    // A new turn resets the accumulator — a delta-free second turn leaves it empty, not
    // showing the first turn's text.
    await act(async () => {
      await result.current.send("second", async () => ({ response: "ok" }));
    });
    await waitFor(() => expect(result.current.streamingText).toBe(""));
  });

  it("on failure: rolls back the user message, snapshots the partial reply, and restores the draft", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    await act(async () => {
      await result.current
        .send("my question", async (onDelta) => {
          onDelta("a partial ");
          onDelta("hint");
          throw Object.assign(new Error("boom"), { statusCode: 502 });
        })
        .catch(() => {});
    });

    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());
    expect(result.current.messages).toEqual([]);                       // optimistic user bubble rolled back
    expect(result.current.failedTurn!.partial).toBe("a partial hint"); // partial snapshotted for the failure UI
    expect(result.current.failedTurn!.failure.kind).toBe("ai");        // interpreted, not raw
    expect(result.current.draft).toEqual({ text: "my question" });     // message restored to the input
  });

  it("omits the partial from a failed turn when nothing was streamed", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    await act(async () => {
      await result.current
        .send("my question", async () => {
          throw Object.assign(new Error("payment required"), { statusCode: 402 });
        })
        .catch(() => {});
    });

    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());
    expect(result.current.failedTurn!.partial).toBeUndefined(); // no "incomplete reply" framing for a pre-stream failure
    expect(result.current.failedTurn!.failure.kind).toBe("paywall");
  });

  it("clears the failed turn and draft when the next turn starts", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    await act(async () => {
      await result.current.send("fails", async () => { throw new Error("boom"); }).catch(() => {});
    });
    await waitFor(() => expect(result.current.failedTurn).not.toBeNull());

    await act(async () => {
      await result.current.send("retry", async () => ({ response: "better" }));
    });

    await waitFor(() => expect(result.current.failedTurn).toBeNull());
    expect(result.current.draft).toBeNull();
    expect(result.current.messages).toEqual([
      { role: "user", content: "retry" },
      { role: "assistant", content: "better" },
    ]);
  });

  it("invalidates quota, balance, and ledger after a settled turn", async () => {
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    await act(async () => {
      await result.current.send("help", async () => ({ response: "ok" }));
    });

    await waitFor(() => {
      expect(queryClient.getQueryState(accountQueryKeys.quota)?.isInvalidated).toBe(true);
      expect(queryClient.getQueryState(accountQueryKeys.balance)?.isInvalidated).toBe(true);
      const ledger = queryClient.getQueryCache().findAll({ queryKey: accountQueryKeys.ledger });
      expect(ledger.every((q) => q.state.isInvalidated)).toBe(true);
    });
  });

  it("runs the surface's extra success effect with the turn's response", async () => {
    const { wrapper } = createWrapper();
    const onTurnSuccess = vi.fn();
    const { result } = renderHook(
      () => useGuidanceChat<TestMessage, TestResponse>({ ...config, onTurnSuccess }),
      { wrapper },
    );

    await act(async () => {
      await result.current.send("help", async () => ({ response: "ok" }));
    });

    await waitFor(() => expect(onTurnSuccess).toHaveBeenCalledWith({ response: "ok" }));
  });

  it("setMessages replaces history for session start and reset", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useGuidanceChat<TestMessage, TestResponse>(config), { wrapper });

    act(() => {
      result.current.setMessages([{ role: "assistant", content: "seeded" }]);
    });
    expect(result.current.messages).toEqual([{ role: "assistant", content: "seeded" }]);

    act(() => {
      result.current.setMessages([]);
    });
    expect(result.current.messages).toEqual([]);
  });
});
