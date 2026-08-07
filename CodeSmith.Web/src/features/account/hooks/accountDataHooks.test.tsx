// == Account data hooks: keys, independence, invalidation == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import * as apiClient from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import { useQuota } from "./useQuota";
import { useBalance } from "./useBalance";
import { useLedger } from "./useLedger";
import { usePacks } from "./usePacks";
import { invalidateAccountUsageQueries } from "./invalidateAccountUsageQueries";

vi.mock("../../../lib/apiClient");

function createWrapper(client?: QueryClient) {
  const queryClient =
    client ??
    new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

const quotaBody = { freeTokensUsed: 100, freeQuotaMax: 20_000, ipConstraint: "None" as const };
const balanceBody = { paidCreditsUsd: 5 };
const ledgerBody = [
  {
    type: "Spend" as const,
    amountUsd: 0,
    isFreeCovered: true,
    feature: "Tutoring:Guidance",
    timestampUtc: "2026-08-01T00:00:00Z",
  },
];
const packsBody = [{ priceId: "price_1", name: "Starter", amount: 10, currency: "usd" }];

describe("account data hooks", () => {
  beforeEach(() => {
    vi.mocked(apiClient.getQuota).mockReset();
    vi.mocked(apiClient.getBalance).mockReset();
    vi.mocked(apiClient.getLedger).mockReset();
    vi.mocked(apiClient.getPacks).mockReset();
    vi.mocked(apiClient.getQuota).mockResolvedValue(quotaBody);
    vi.mocked(apiClient.getBalance).mockResolvedValue(balanceBody);
    vi.mocked(apiClient.getLedger).mockResolvedValue(ledgerBody);
    vi.mocked(apiClient.getPacks).mockResolvedValue(packsBody);
  });

  it("useQuota uses ['usage','quota'] and returns typed quota including ipConstraint", async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useQuota(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(quotaBody);
    expect(result.current.data?.ipConstraint).toBe("None");
  });

  it("useBalance uses ['billing','balance']", async () => {
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => useBalance(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(balanceBody);
    expect(queryClient.getQueryData(accountQueryKeys.balance)).toEqual(balanceBody);
  });

  it("useLedger returns isFreeCovered and keys under ['billing','ledger']", async () => {
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => useLedger(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.[0]?.isFreeCovered).toBe(true);
    // Prefix match: base key is shared for invalidation
    expect(queryClient.getQueriesData({ queryKey: accountQueryKeys.ledger }).length).toBeGreaterThan(0);
  });

  it("usePacks uses ['billing','packs']", async () => {
    const { wrapper, queryClient } = createWrapper();
    const { result } = renderHook(() => usePacks(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(queryClient.getQueryData(accountQueryKeys.packs)).toEqual(packsBody);
  });

  it("a packs 502 leaves balance, ledger, and quota in their success state", async () => {
    const packsError = Object.assign(new Error("Stripe unreachable"), { statusCode: 502 });
    vi.mocked(apiClient.getPacks).mockRejectedValue(packsError);

    const { wrapper, queryClient } = createWrapper();

    const quota = renderHook(() => useQuota(), { wrapper });
    const balance = renderHook(() => useBalance(), { wrapper });
    const ledger = renderHook(() => useLedger(), { wrapper });
    const packs = renderHook(() => usePacks(), { wrapper });

    await waitFor(() => {
      expect(quota.result.current.isSuccess).toBe(true);
      expect(balance.result.current.isSuccess).toBe(true);
      expect(ledger.result.current.isSuccess).toBe(true);
      expect(packs.result.current.isError).toBe(true);
    });

    expect(queryClient.getQueryState(accountQueryKeys.quota)?.status).toBe("success");
    expect(queryClient.getQueryState(accountQueryKeys.balance)?.status).toBe("success");
    expect(queryClient.getQueriesData({ queryKey: accountQueryKeys.ledger })[0]?.[1]).toEqual(ledgerBody);
    expect(packs.result.current.isError).toBe(true);
  });

  it("invalidateAccountUsageQueries marks quota, balance, and ledger stale — not packs", async () => {
    const { wrapper, queryClient } = createWrapper();

    renderHook(() => useQuota(), { wrapper });
    renderHook(() => useBalance(), { wrapper });
    renderHook(() => useLedger(), { wrapper });
    renderHook(() => usePacks(), { wrapper });

    await waitFor(() => {
      expect(queryClient.getQueryState(accountQueryKeys.quota)?.status).toBe("success");
      expect(queryClient.getQueryState(accountQueryKeys.packs)?.status).toBe("success");
    });

    invalidateAccountUsageQueries(queryClient);

    expect(queryClient.getQueryState(accountQueryKeys.quota)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(accountQueryKeys.balance)?.isInvalidated).toBe(true);
    const ledgerStates = queryClient.getQueryCache().findAll({ queryKey: accountQueryKeys.ledger });
    expect(ledgerStates.every((q) => q.state.isInvalidated)).toBe(true);
    expect(queryClient.getQueryState(accountQueryKeys.packs)?.isInvalidated).toBe(false);
  });
});
