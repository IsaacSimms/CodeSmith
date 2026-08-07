// == Credits card: balance, pack list, checkout == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import * as apiClient from "../../../lib/apiClient";
import {
  clearCheckoutBaseline,
  isCheckoutPending,
  readCheckoutBaseline,
  writeCheckoutBaseline,
} from "../checkoutBaseline";
import { CreditsCard } from "./CreditsCard";

vi.mock("../../../lib/apiClient");

const packsBody = [
  { priceId: "price_starter", name: "Starter", amount: 5, currency: "usd" },
  { priceId: "price_pro", name: "Pro", amount: 20, currency: "usd" },
];

const existingTopUps = [
  {
    type: "TopUp" as const,
    amountUsd: 10,
    isFreeCovered: false,
    feature: "Billing:TopUp",
    timestampUtc: "2026-08-01T08:00:00Z",
  },
  {
    type: "Spend" as const,
    amountUsd: 0.01,
    isFreeCovered: false,
    feature: "Tutoring:Guidance",
    timestampUtc: "2026-08-01T09:00:00Z",
  },
];

function renderCard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  return { ...render(<CreditsCard />, { wrapper }), queryClient };
}

beforeEach(() => {
  sessionStorage.clear();
  vi.mocked(apiClient.getBalance).mockReset();
  vi.mocked(apiClient.getPacks).mockReset();
  vi.mocked(apiClient.createCheckout).mockReset();
  vi.mocked(apiClient.getLedger).mockReset();
  vi.mocked(apiClient.getBalance).mockResolvedValue({ paidCreditsUsd: 12.4 });
  vi.mocked(apiClient.getPacks).mockResolvedValue(packsBody);
  vi.mocked(apiClient.getLedger).mockResolvedValue(existingTopUps);
  vi.mocked(apiClient.createCheckout).mockResolvedValue({
    url: "https://checkout.stripe.com/c/pay/cs_test_abc",
  });
});

afterEach(() => {
  clearCheckoutBaseline();
  vi.unstubAllGlobals();
});

describe("CreditsCard", () => {
  it("renders the paid balance and one button per pack with name and formatted amount", async () => {
    renderCard();

    expect(await screen.findByText("$12.40")).toBeInTheDocument();

    const starter = await screen.findByRole("button", { name: /Starter/i });
    const pro = screen.getByRole("button", { name: /Pro/i });
    expect(starter).toHaveTextContent("$5.00");
    expect(pro).toHaveTextContent("$20.00");

    // Pack order follows the endpoint response
    const buttons = screen.getAllByRole("button").filter((b) => /Starter|Pro/.test(b.textContent ?? ""));
    expect(buttons.map((b) => b.textContent)).toEqual([
      expect.stringContaining("Starter"),
      expect.stringContaining("Pro"),
    ]);
  });

  it("renders an empty state when packs returns [], not an error", async () => {
    vi.mocked(apiClient.getPacks).mockResolvedValue([]);

    renderCard();

    expect(await screen.findByText("$12.40")).toBeInTheDocument();
    expect(await screen.findByText(/no packs available/i)).toBeInTheDocument();
    expect(screen.queryByTestId("failure-notice")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Starter/i })).not.toBeInTheDocument();
  });

  it("packs 502 shows inline error with retry while balance stays; retry refetches only packs", async () => {
    const packsError = Object.assign(new Error("Stripe unreachable"), { statusCode: 502 });
    vi.mocked(apiClient.getPacks).mockRejectedValueOnce(packsError);

    const user = userEvent.setup();
    renderCard();

    // Balance remains visible — card body is not taken down by packs failure
    expect(await screen.findByText("$12.40")).toBeInTheDocument();
    expect(await screen.findByTestId("failure-notice")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Starter/i })).not.toBeInTheDocument();

    const balanceCallsAfterError = vi.mocked(apiClient.getBalance).mock.calls.length;
    const packsCallsAfterError = vi.mocked(apiClient.getPacks).mock.calls.length;

    vi.mocked(apiClient.getPacks).mockResolvedValue(packsBody);
    await user.click(screen.getByRole("button", { name: /retry/i }));

    expect(await screen.findByRole("button", { name: /Starter/i })).toBeInTheDocument();
    expect(vi.mocked(apiClient.getPacks).mock.calls.length).toBeGreaterThan(packsCallsAfterError);
    expect(vi.mocked(apiClient.getBalance).mock.calls.length).toBe(balanceCallsAfterError);
  });

  it("clicking a pack posts its priceId and follows the returned checkout URL", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { assign });

    const user = userEvent.setup();
    renderCard();

    await user.click(await screen.findByRole("button", { name: /Pro/i }));

    await waitFor(() => {
      expect(apiClient.createCheckout).toHaveBeenCalledWith("price_pro");
    });
    expect(assign).toHaveBeenCalledWith("https://checkout.stripe.com/c/pay/cs_test_abc");
  });

  it("writes TopUp baseline fingerprints before redirecting to Stripe", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { assign });

    const user = userEvent.setup();
    renderCard();

    await user.click(await screen.findByRole("button", { name: /Pro/i }));

    await waitFor(() => {
      expect(assign).toHaveBeenCalled();
    });

    // Baseline must be on disk before navigation — not after return
    expect(apiClient.getLedger).toHaveBeenCalled();
    expect(readCheckoutBaseline()).toEqual({
      fingerprints: [{ timestampUtc: "2026-08-01T08:00:00Z", amountUsd: 10 }],
      pending: true,
    });
    // createCheckout after ledger snapshot (ordering: baseline before redirect URL)
    const [ledgerOrder] = vi.mocked(apiClient.getLedger).mock.invocationCallOrder;
    const [checkoutOrder] = vi.mocked(apiClient.createCheckout).mock.invocationCallOrder;
    expect(ledgerOrder).toBeDefined();
    expect(checkoutOrder).toBeDefined();
    expect(ledgerOrder!).toBeLessThan(checkoutOrder!);
  });

  it("re-buy while a purchase is still applying prompts and overwrites baseline on confirm", async () => {
    writeCheckoutBaseline([{ timestampUtc: "2026-07-01T00:00:00Z", amountUsd: 1 }]);
    expect(isCheckoutPending()).toBe(true);

    const confirm = vi.fn().mockReturnValue(true);
    vi.stubGlobal("confirm", confirm);
    const assign = vi.fn();
    vi.stubGlobal("location", { assign });

    const user = userEvent.setup();
    renderCard();

    await user.click(await screen.findByRole("button", { name: /Starter/i }));

    await waitFor(() => {
      expect(assign).toHaveBeenCalled();
    });

    expect(confirm).toHaveBeenCalledWith(
      "A purchase is still applying — continue?"
    );
    // New baseline overwrites the old fingerprints
    expect(readCheckoutBaseline()?.fingerprints).toEqual([
      { timestampUtc: "2026-08-01T08:00:00Z", amountUsd: 10 },
    ]);
  });

  it("re-buy mid-poll cancels when the user declines the confirm", async () => {
    writeCheckoutBaseline([{ timestampUtc: "2026-07-01T00:00:00Z", amountUsd: 1 }]);

    const confirm = vi.fn().mockReturnValue(false);
    vi.stubGlobal("confirm", confirm);
    const assign = vi.fn();
    vi.stubGlobal("location", { assign });

    const user = userEvent.setup();
    renderCard();

    await user.click(await screen.findByRole("button", { name: /Starter/i }));

    // Allow any microtasks to flush
    await waitFor(() => {
      expect(confirm).toHaveBeenCalled();
    });

    expect(apiClient.createCheckout).not.toHaveBeenCalled();
    expect(assign).not.toHaveBeenCalled();
    // Prior baseline left intact
    expect(readCheckoutBaseline()?.fingerprints).toEqual([
      { timestampUtc: "2026-07-01T00:00:00Z", amountUsd: 1 },
    ]);
  });
});
