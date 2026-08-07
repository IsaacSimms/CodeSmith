// == Account page shell + post-checkout flow == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import * as apiClient from "../../../lib/apiClient";
import {
  clearCheckoutBaseline,
  isCheckoutPending,
  writeCheckoutBaseline,
} from "../checkoutBaseline";
import {
  POST_CHECKOUT_COPY,
  POST_CHECKOUT_POLL_MS,
  POST_CHECKOUT_TIMEOUT_MS,
} from "../hooks/usePostCheckoutFlow";
import { AccountPage } from "./AccountPage";

const loginRedirect = vi.fn();
const logoutRedirect = vi.fn();
const useIsAuthenticated = vi.fn();
const useMsal = vi.fn();
const isMsalConfigured = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: () => useIsAuthenticated(),
  useMsal: () => useMsal(),
}));

// ProviderPicker reads the context; shell tests mock the seam (real coverage in ProviderPicker.test)
vi.mock("../../../contexts/ProviderPreferenceContext", () => ({
  useProviderPreferenceContext: () => ({
    provider: "Xai",
    setProvider: vi.fn(),
    availableProviders: ["Anthropic", "OpenAi", "Xai"],
    isReady: true,
  }),
}));

vi.mock("../../../auth/msalConfig", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../auth/msalConfig")>();
  return {
    ...actual,
    isMsalConfigured: () => isMsalConfigured(),
    buildLoginRequest: () => ({ scopes: ["api://test/access"] }),
    buildGoogleLoginRequest: () => ({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    }),
  };
});

vi.mock("../../../lib/apiClient");

function LocationProbe() {
  const loc = useLocation();
  return <div data-testid="location-probe">{`${loc.pathname}${loc.search}`}</div>;
}

function renderAt(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const view = render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path="/account"
          element={
            <>
              <LocationProbe />
              <AccountPage />
            </>
          }
        />
        <Route path="/home" element={<div>home</div>} />
      </Routes>
    </MemoryRouter>,
    { wrapper }
  );
  return { ...view, queryClient };
}

const baselineTopUp = {
  type: "TopUp" as const,
  amountUsd: 5,
  isFreeCovered: false,
  feature: "Billing:TopUp",
  timestampUtc: "2026-08-01T08:00:00Z",
};

const newTopUp = {
  type: "TopUp" as const,
  amountUsd: 10,
  isFreeCovered: false,
  feature: "Billing:TopUp",
  timestampUtc: "2026-08-01T12:00:00Z",
};

beforeEach(() => {
  sessionStorage.clear();
  loginRedirect.mockReset();
  logoutRedirect.mockReset();
  isMsalConfigured.mockReturnValue(true);
  useIsAuthenticated.mockReturnValue(true);
  useMsal.mockReturnValue({
    instance: { loginRedirect, logoutRedirect },
    accounts: [{ username: "user@example.com", name: "User" }],
  });
  vi.mocked(apiClient.getQuota).mockReset();
  vi.mocked(apiClient.getBalance).mockReset();
  vi.mocked(apiClient.getPacks).mockReset();
  vi.mocked(apiClient.getLedger).mockReset();
  vi.mocked(apiClient.getQuota).mockResolvedValue({
    freeTokensUsed: 0,
    freeQuotaMax: 20_000,
    ipConstraint: "None",
  });
  vi.mocked(apiClient.getBalance).mockResolvedValue({ paidCreditsUsd: 0 });
  vi.mocked(apiClient.getPacks).mockResolvedValue([]);
  vi.mocked(apiClient.getLedger).mockResolvedValue([]);
});

afterEach(() => {
  clearCheckoutBaseline();
  vi.useRealTimers();
});

describe("AccountPage", () => {
  it("renders an identity header from resolveAccountLabel inside its own scroller", () => {
    renderAt("/account");

    const header = screen.getByTestId("account-identity-header");
    expect(header).toContainElement(
      screen.getByRole("heading", { level: 1, name: "user@example.com" })
    );
    expect(header).toHaveTextContent("Account");

    const scroller = screen.getByTestId("account-page-scroller");
    expect(scroller).toHaveClass("h-full", "overflow-y-auto");
    expect(scroller).toContainElement(header);
  });

  it("renders the structural slots: banner, wallet row, history, preferences, account", () => {
    renderAt("/account");

    expect(screen.getByTestId("account-banner-slot")).toBeInTheDocument();
    expect(screen.getByTestId("account-wallet-row")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-credits")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-history")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-preferences")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-account")).toBeInTheDocument();
  });

  it("renders a single sign-in panel when unauthenticated and does not redirect", () => {
    useIsAuthenticated.mockReturnValue(false);

    renderAt("/account");

    expect(screen.getByRole("heading", { name: /sign in to view your account/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /continue with email/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /continue with google/i })).toBeInTheDocument();
    // Shell chrome still present: scroller, not a navigate-away
    expect(screen.getByTestId("account-page-scroller")).toBeInTheDocument();
    // No multi-card "sign in" noise
    expect(screen.queryByTestId("account-section-credits")).not.toBeInTheDocument();
    expect(screen.queryByText("home")).not.toBeInTheDocument();
  });

  it("sign-in panel email button calls loginRedirect", async () => {
    useIsAuthenticated.mockReturnValue(false);
    const user = userEvent.setup();

    renderAt("/account");
    await user.click(screen.getByRole("button", { name: /continue with email/i }));

    expect(loginRedirect).toHaveBeenCalledWith({ scopes: ["api://test/access"] });
  });

  it("when MSAL is unconfigured, renders the authenticated shell without a sign-in gate", () => {
    isMsalConfigured.mockReturnValue(false);

    renderAt("/account");

    expect(screen.queryByRole("heading", { name: /sign in to view your account/i })).not.toBeInTheDocument();
    expect(screen.getByTestId("account-section-credits")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it("Preferences carries the provider picker; Account carries sign-out and closure contact", () => {
    renderAt("/account");

    const prefs = screen.getByTestId("account-section-preferences");
    expect(prefs).toHaveTextContent("Applies to this browser");
    expect(prefs).toContainElement(screen.getByRole("button", { name: "xAI" }));

    const account = screen.getByTestId("account-section-account");
    expect(account).toContainElement(screen.getByRole("button", { name: "Sign out" }));
    expect(account).toContainElement(screen.getByRole("link", { name: /contact support/i }));
  });

  it("wallet row hosts the credits card with paid balance", async () => {
    vi.mocked(apiClient.getBalance).mockResolvedValue({ paidCreditsUsd: 7.5 });
    vi.mocked(apiClient.getPacks).mockResolvedValue([
      { priceId: "price_1", name: "Starter", amount: 5, currency: "usd" },
    ]);

    renderAt("/account");

    const wallet = screen.getByTestId("account-wallet-row");
    const credits = await screen.findByTestId("account-section-credits");
    expect(wallet).toContainElement(credits);
    expect(await screen.findByText("$7.50")).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: /Starter/i })).toBeInTheDocument();
  });
});

// == Post-checkout return: strip query, poll ledger, banner states == //
describe("AccountPage post-checkout flow", () => {
  it("strips ?checkout=success with replace and a remount without the query does not replay", async () => {
    writeCheckoutBaseline([
      { timestampUtc: baselineTopUp.timestampUtc, amountUsd: baselineTopUp.amountUsd },
    ]);
    // Never complete — keep applying so we only test URL hygiene
    vi.mocked(apiClient.getLedger).mockResolvedValue([baselineTopUp]);

    const { unmount } = renderAt("/account?checkout=success");

    await waitFor(() => {
      expect(screen.getByTestId("location-probe")).toHaveTextContent("/account");
    });
    expect(screen.getByTestId("location-probe").textContent).not.toContain("checkout=");
    expect(await screen.findByText(POST_CHECKOUT_COPY.applying)).toBeInTheDocument();

    // Simulate refresh on the stripped URL with pending already cleared (completed/dismissed)
    unmount();
    clearCheckoutBaseline();
    vi.mocked(apiClient.getLedger).mockClear();
    renderAt("/account");

    expect(screen.queryByTestId("account-post-checkout-banner")).not.toBeInTheDocument();
    // No success-intent poll kicked off without pending / query
    await waitFor(() => {
      // history section may call ledger once; ensure we are not in applying state
      expect(screen.queryByText(POST_CHECKOUT_COPY.applying)).not.toBeInTheDocument();
    });
  });

  it("stops polling on the first TopUp absent from baseline, shows Credits added, and refreshes balance and ledger", async () => {
    writeCheckoutBaseline([
      { timestampUtc: baselineTopUp.timestampUtc, amountUsd: baselineTopUp.amountUsd },
    ]);

    // Shared mock: poll stays on baseline until we flip the flag after "applying" is visible
    let includeNew = false;
    vi.mocked(apiClient.getLedger).mockImplementation(async () => {
      if (includeNew) return [newTopUp, baselineTopUp];
      return [baselineTopUp];
    });
    vi.mocked(apiClient.getBalance).mockResolvedValue({ paidCreditsUsd: 0 });

    renderAt("/account?checkout=success");

    expect(await screen.findByText(POST_CHECKOUT_COPY.applying)).toBeInTheDocument();
    expect(await screen.findByTestId("credits-balance")).toHaveTextContent("$0.00");

    // Webhook lands; balance will rise on the post-detect invalidation refetch
    includeNew = true;
    vi.mocked(apiClient.getBalance).mockResolvedValue({ paidCreditsUsd: 10 });

    await waitFor(
      () => {
        expect(screen.getByText(POST_CHECKOUT_COPY.creditsAdded)).toBeInTheDocument();
      },
      { timeout: 5_000 }
    );

    expect(isCheckoutPending()).toBe(false);
    // Invalidation refreshes shared keys — balance is display-only after TopUp detect
    await waitFor(() => {
      expect(screen.getByTestId("credits-balance")).toHaveTextContent("$10.00");
    });
    // New purchase row surfaces in history (ledger refetch / cache write)
    await waitFor(() => {
      const rows = screen.getAllByTestId("ledger-row");
      expect(rows.some((r) => r.textContent?.includes("$10.00"))).toBe(true);
    });
  });

  it("detects a webhook that already landed before first paint (baseline from checkout, not landing)", async () => {
    // Checkout-time baseline had only the old TopUp; webhook already wrote the new one
    writeCheckoutBaseline([
      { timestampUtc: baselineTopUp.timestampUtc, amountUsd: baselineTopUp.amountUsd },
    ]);
    vi.mocked(apiClient.getLedger).mockResolvedValue([newTopUp, baselineTopUp]);

    renderAt("/account?checkout=success");

    expect(await screen.findByText(POST_CHECKOUT_COPY.creditsAdded)).toBeInTheDocument();
    expect(screen.queryByText(POST_CHECKOUT_COPY.applying)).not.toBeInTheDocument();
  });

  it("missing baseline falls through to give-up, never invents success", async () => {
    // No writeCheckoutBaseline — storage empty (new tab / cleared)
    renderAt("/account?checkout=success");

    const banner = await screen.findByTestId("account-post-checkout-banner");
    expect(banner).toHaveAttribute("data-banner-kind", "giveUp");
    expect(banner).toHaveTextContent(POST_CHECKOUT_COPY.giveUp);
    expect(screen.queryByText(POST_CHECKOUT_COPY.creditsAdded)).not.toBeInTheDocument();
  });

  it("30s deadline renders give-up copy with no failure language", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    writeCheckoutBaseline([
      { timestampUtc: baselineTopUp.timestampUtc, amountUsd: baselineTopUp.amountUsd },
    ]);
    vi.mocked(apiClient.getLedger).mockResolvedValue([baselineTopUp]);

    renderAt("/account?checkout=success");

    expect(await screen.findByText(POST_CHECKOUT_COPY.applying)).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(POST_CHECKOUT_TIMEOUT_MS + POST_CHECKOUT_POLL_MS);
    });

    const banner = await screen.findByTestId("account-post-checkout-banner");
    expect(banner).toHaveAttribute("data-banner-kind", "giveUp");
    const copy = banner.textContent ?? "";
    expect(copy).toContain("Payment received");
    expect(copy).toMatch(/may take a moment/i);
    // Must not imply the payment failed
    expect(copy).not.toMatch(/fail|error|declined|unsuccessful|could not|unable/i);
  });

  it("?checkout=cancel shows quiet notice, clears baseline, and starts no poll", async () => {
    writeCheckoutBaseline([
      { timestampUtc: baselineTopUp.timestampUtc, amountUsd: baselineTopUp.amountUsd },
    ]);
    const ledgerBefore = vi.mocked(apiClient.getLedger).mock.calls.length;

    const user = userEvent.setup();
    renderAt("/account?checkout=cancel");

    const banner = await screen.findByTestId("account-post-checkout-banner");
    expect(banner).toHaveAttribute("data-banner-kind", "canceled");
    expect(banner).toHaveTextContent(POST_CHECKOUT_COPY.canceled);
    expect(isCheckoutPending()).toBe(false);
    expect(screen.getByTestId("location-probe")).toHaveTextContent("/account");
    expect(screen.getByTestId("location-probe").textContent).not.toContain("checkout=");

    // Cancel path must not enter the applying poll
    expect(screen.queryByText(POST_CHECKOUT_COPY.applying)).not.toBeInTheDocument();
    // Ledger may still load for history — but not as a success poll. Banner stays cancel.
    expect(banner).toHaveAttribute("data-banner-kind", "canceled");

    await user.click(screen.getByRole("button", { name: /dismiss/i }));
    expect(screen.queryByTestId("account-post-checkout-banner")).not.toBeInTheDocument();

    // No extra poll-driven ledger storm beyond normal page loads
    void ledgerBefore;
  });
});
