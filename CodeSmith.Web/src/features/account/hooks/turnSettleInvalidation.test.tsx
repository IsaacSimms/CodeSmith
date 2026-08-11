// == Metered turn settle invalidates quota + balance + ledger on each surface == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import * as apiClient from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import { useSendMessage } from "../../chat/hooks/useSendMessage";
import { usePromptLabChat } from "../../prompt-lab/hooks/usePromptLabChat";
import { useSystemLabChat } from "../../system-lab/hooks/useSystemLabChat";

vi.mock("../../../lib/apiClient");

function seedUsageQueries(queryClient: QueryClient) {
  queryClient.setQueryData(accountQueryKeys.quota, {
    freeTokensUsed: 100,
    freeQuotaMax: 20_000,
    ipConstraint: "None",
  });
  queryClient.setQueryData(accountQueryKeys.balance, { paidCreditsUsd: 5 });
  queryClient.setQueryData([...accountQueryKeys.ledger, 20], []);
  queryClient.setQueryData(accountQueryKeys.packs, []);
}

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  seedUsageQueries(queryClient);
  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

function expectUsageInvalidated(queryClient: QueryClient) {
  expect(queryClient.getQueryState(accountQueryKeys.quota)?.isInvalidated).toBe(true);
  expect(queryClient.getQueryState(accountQueryKeys.balance)?.isInvalidated).toBe(true);
  const ledger = queryClient.getQueryCache().findAll({ queryKey: accountQueryKeys.ledger });
  expect(ledger.length).toBeGreaterThan(0);
  expect(ledger.every((q) => q.state.isInvalidated)).toBe(true);
  // Packs stay warm — catalog does not move on a turn
  expect(queryClient.getQueryState(accountQueryKeys.packs)?.isInvalidated).toBe(false);
}

describe("metered turn-settle invalidation", () => {
  beforeEach(() => {
    vi.mocked(apiClient.streamChat).mockReset();
    vi.mocked(apiClient.streamPromptLabChat).mockReset();
    vi.mocked(apiClient.streamSystemLabChat).mockReset();
  });

  it("Tutoring chat settle invalidates quota, balance, and ledger", async () => {
    vi.mocked(apiClient.streamChat).mockResolvedValue({
      response: "Try a loop",
      contextTokensUsed: 10,
      contextWindowSize: 200_000,
    });
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => useSendMessage(), { wrapper });

    await act(async () => {
      await result.current.sendTurn("s1", "help");
    });

    await waitFor(() => expectUsageInvalidated(queryClient));
  });

  it("Prompt Lab chat settle invalidates quota, balance, and ledger", async () => {
    vi.mocked(apiClient.streamPromptLabChat).mockResolvedValue({
      response: "Refine the system prompt",
    });
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => usePromptLabChat(), { wrapper });

    await act(async () => {
      await result.current.sendTurn("pl1", "help");
    });

    await waitFor(() => expectUsageInvalidated(queryClient));
  });

  it("System Lab chat settle invalidates quota, balance, and ledger", async () => {
    vi.mocked(apiClient.streamSystemLabChat).mockResolvedValue({
      response: "Consider consistency",
    });
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => useSystemLabChat(), { wrapper });

    await act(async () => {
      await result.current.sendTurn("sl1", "help");
    });

    await waitFor(() => expectUsageInvalidated(queryClient));
  });
});
