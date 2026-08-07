// == AuthControls sign-in chooser + authenticated balance dropdown == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import type { ReactElement } from "react";
import { AuthControls } from "./AuthControls";
import { accountQueryKeys } from "../features/account/queryKeys";
import type { BalanceResponse, QuotaResponse } from "../features/account/types";
import * as apiClient from "../lib/apiClient";

const loginRedirect = vi.fn();
const logoutRedirect = vi.fn();
const useIsAuthenticated = vi.fn();
const useMsal = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: () => useIsAuthenticated(),
  useMsal: () => useMsal(),
}));

vi.mock("./msalConfig", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./msalConfig")>();
  return {
    ...actual,
    isMsalConfigured: () => true,
    buildLoginRequest: () => ({ scopes: ["api://test/access"] }),
    buildGoogleLoginRequest: () => ({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    }),
  };
});

vi.mock("../lib/apiClient", () => ({
  getQuota: vi.fn(),
  getBalance: vi.fn(),
  getLedger: vi.fn(),
  getPacks: vi.fn(),
}));

// == Seeded cache + router so authenticated path can read shared account hooks == //
function renderAuth(
  ui: ReactElement,
  options?: {
    quota?: QuotaResponse;
    balance?: BalanceResponse;
    queryClient?: QueryClient;
  }
) {
  const queryClient =
    options?.queryClient ??
    new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: Infinity },
      },
    });
  if (options?.quota) {
    queryClient.setQueryData(accountQueryKeys.quota, options.quota);
  }
  if (options?.balance) {
    queryClient.setQueryData(accountQueryKeys.balance, options.balance);
  }
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>{ui}</MemoryRouter>
      </QueryClientProvider>
    ),
  };
}

function mockSignedIn(account: object = { username: "user@example.com", name: "User" }) {
  useIsAuthenticated.mockReturnValue(true);
  useMsal.mockReturnValue({
    instance: { loginRedirect, logoutRedirect },
    accounts: [account],
  });
}

const freeQuota: QuotaResponse = {
  freeTokensUsed: 7_600,
  freeQuotaMax: 20_000,
  ipConstraint: "None",
};

const exhaustedQuota: QuotaResponse = {
  freeTokensUsed: 20_000,
  freeQuotaMax: 20_000,
  ipConstraint: "None",
};

const paidBalance: BalanceResponse = { paidCreditsUsd: 12.4 };

beforeEach(() => {
  loginRedirect.mockReset();
  logoutRedirect.mockReset();
  useIsAuthenticated.mockReturnValue(false);
  useMsal.mockReturnValue({
    instance: { loginRedirect, logoutRedirect },
    accounts: [],
  });
  vi.mocked(apiClient.getQuota).mockReset();
  vi.mocked(apiClient.getBalance).mockReset();
  vi.mocked(apiClient.getQuota).mockResolvedValue(freeQuota);
  vi.mocked(apiClient.getBalance).mockResolvedValue(paidBalance);
});

describe("AuthControls", () => {
  it("shows Sign in when signed out and hides provider options until opened", () => {
    renderAuth(<AuthControls />);

    expect(screen.getByRole("button", { name: "Sign in" })).toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Continue with email" })).not.toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Continue with Google" })).not.toBeInTheDocument();
  });

  it("opens dropdown with email, Google, and helper text", async () => {
    const user = userEvent.setup();
    renderAuth(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByRole("menuitem", { name: "Continue with email" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Continue with Google" })).toBeInTheDocument();
    expect(screen.getByText("Use the same sign-in method next time.")).toBeInTheDocument();
    expect(
      screen.getByText("ciamlogin.com is CodeSmith's Microsoft sign-in host.")
    ).toBeInTheDocument();
  });

  it("Continue with email calls loginRedirect without domain_hint", async () => {
    const user = userEvent.setup();
    renderAuth(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(screen.getByRole("menuitem", { name: "Continue with email" }));

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(loginRedirect).toHaveBeenCalledWith({ scopes: ["api://test/access"] });
    const firstArgs = loginRedirect.mock.calls[0]?.[0];
    expect(firstArgs).toBeDefined();
    expect(firstArgs).not.toHaveProperty("extraQueryParameters");
  });

  it("Continue with Google calls loginRedirect with domain_hint Google", async () => {
    const user = userEvent.setup();
    renderAuth(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(screen.getByRole("menuitem", { name: "Continue with Google" }));

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(loginRedirect).toHaveBeenCalledWith({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    });
  });

  it("signed in uses the account label as the menu toggle and hides Sign in", () => {
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    expect(screen.getByRole("button", { name: "user@example.com" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sign in" })).not.toBeInTheDocument();
    // Sign out lives inside the closed menu
    expect(screen.queryByRole("menuitem", { name: "Sign out" })).not.toBeInTheDocument();
  });

  it("signed in with a federated account shows the email claim, not the GUID UPN", () => {
    mockSignedIn({
      username: "be36f73c-1993-4e1c-8064-8a4156082144@codesmithapp.onmicrosoft.com",
      name: "IsaacTestGoogleAuth",
      idTokenClaims: { email: "isaacsimms11@gmail.com" },
    });
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    expect(screen.getByRole("button", { name: "isaacsimms11@gmail.com" })).toBeInTheDocument();
    expect(screen.queryByText(/be36f73c/)).not.toBeInTheDocument();
  });

  it("Sign out calls logoutRedirect from the authenticated menu", async () => {
    const user = userEvent.setup();
    const account = { username: "user@example.com", name: "User" };
    mockSignedIn(account);
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    await user.click(screen.getByRole("menuitem", { name: "Sign out" }));

    expect(logoutRedirect).toHaveBeenCalledWith({
      account,
      postLogoutRedirectUri: window.location.origin,
    });
  });

  // == Balance summary lifecycle (ticket 007 / work 009) == //

  it("free mode shows remaining tokens only while the account grant has headroom", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    const menu = screen.getByRole("menu");
    expect(within(menu).getByText("12,400 free tokens")).toBeInTheDocument();
    expect(within(menu).queryByText(/credits/i)).not.toBeInTheDocument();
  });

  it("crossing freeTokensUsed >= freeQuotaMax switches the same slot to paid USD", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    const { queryClient } = renderAuth(<AuthControls />, {
      quota: freeQuota,
      balance: paidBalance,
    });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    expect(within(screen.getByRole("menu")).getByText("12,400 free tokens")).toBeInTheDocument();

    // Exhaust the grant in the shared cache (same keys Account uses)
    queryClient.setQueryData(accountQueryKeys.quota, exhaustedQuota);
    await waitFor(() => {
      expect(within(screen.getByRole("menu")).getByText("$12.40 credits")).toBeInTheDocument();
    });
    expect(within(screen.getByRole("menu")).queryByText(/free tokens/i)).not.toBeInTheDocument();
  });

  it("loading renders a muted placeholder in a stable slot without inventing figures", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    // Never resolve — mode unknown while quota is pending
    vi.mocked(apiClient.getQuota).mockReturnValue(new Promise(() => {}));
    vi.mocked(apiClient.getBalance).mockReturnValue(new Promise(() => {}));
    renderAuth(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    const menu = screen.getByRole("menu");
    const summary = within(menu).getByTestId("balance-summary");
    expect(summary).toHaveTextContent("—");
    expect(summary).toHaveClass("text-gray-500");
    expect(within(menu).queryByText(/free tokens/i)).not.toBeInTheDocument();
    expect(within(menu).queryByText(/\$0\.00/)).not.toBeInTheDocument();
    expect(within(menu).queryByText(/credits/i)).not.toBeInTheDocument();
    // Account + Sign out still present around the stable slot
    expect(within(menu).getByRole("menuitem", { name: "Account" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
  });

  it("error on the active mode's query hides only the summary row", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    vi.mocked(apiClient.getQuota).mockRejectedValue(new Error("quota down"));
    // No seeded quota — hook fetch fails; mode unknown → omit summary
    renderAuth(<AuthControls />, { balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    await waitFor(() => {
      const menu = screen.getByRole("menu");
      expect(within(menu).queryByTestId("balance-summary")).not.toBeInTheDocument();
    });
    const menu = screen.getByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Account" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
  });

  it("paid-mode balance error hides the summary while Account and Sign out remain", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    vi.mocked(apiClient.getBalance).mockRejectedValue(new Error("balance down"));
    renderAuth(<AuthControls />, { quota: exhaustedQuota });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    await waitFor(() => {
      const menu = screen.getByRole("menu");
      expect(within(menu).queryByTestId("balance-summary")).not.toBeInTheDocument();
    });
    const menu = screen.getByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Account" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
  });

  it("summary is passive text; Account is the only path to /account", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    const menu = screen.getByRole("menu");
    const summary = within(menu).getByTestId("balance-summary");
    // Not a menuitem action, not a link, not focusable navigation
    expect(summary).not.toHaveAttribute("role", "menuitem");
    expect(summary.tagName).not.toBe("A");
    expect(summary.tagName).not.toBe("BUTTON");
    expect(summary.querySelector("a")).toBeNull();
    expect(within(summary).queryByRole("link")).not.toBeInTheDocument();
    expect(within(summary).queryByRole("button")).not.toBeInTheDocument();

    const account = within(menu).getByRole("menuitem", { name: "Account" });
    expect(account.tagName).toBe("A");
    expect(account).toHaveAttribute("href", "/account");
    // Only Account carries navigation; summary is not an anchor
    expect(menu.querySelectorAll('a[href="/account"]')).toHaveLength(1);
    expect(menu.querySelectorAll("a")).toHaveLength(1);
  });

  it("menu order is balance summary, then Account, then Sign out", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));

    const menu = screen.getByRole("menu");
    const children = Array.from(menu.children).map((el) => {
      if (el.getAttribute("data-testid") === "balance-summary") return "summary";
      return el.textContent?.trim() ?? "";
    });
    expect(children).toEqual(["summary", "Account", "Sign out"]);
  });

  it("authenticated menu closes on outside click and Escape", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    await user.click(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("reads the shared cache and does not fetch again on open when warm", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    renderAuth(<AuthControls />, { quota: freeQuota, balance: paidBalance });

    // Seeded cache + staleTime: Infinity — hooks must not call the network on mount or open
    expect(vi.mocked(apiClient.getQuota)).not.toHaveBeenCalled();
    expect(vi.mocked(apiClient.getBalance)).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    expect(within(screen.getByRole("menu")).getByText("12,400 free tokens")).toBeInTheDocument();

    expect(vi.mocked(apiClient.getQuota)).not.toHaveBeenCalled();
    expect(vi.mocked(apiClient.getBalance)).not.toHaveBeenCalled();
  });

  it("paid mode shows $0.00 for a true zero and < $0.01 for a sub-cent balance", async () => {
    const user = userEvent.setup();
    mockSignedIn();
    const { queryClient } = renderAuth(<AuthControls />, {
      quota: exhaustedQuota,
      balance: { paidCreditsUsd: 0 },
    });

    await user.click(screen.getByRole("button", { name: "user@example.com" }));
    expect(within(screen.getByRole("menu")).getByText("$0.00 credits")).toBeInTheDocument();

    queryClient.setQueryData(accountQueryKeys.balance, { paidCreditsUsd: 0.0042 });
    await waitFor(() => {
      expect(within(screen.getByRole("menu")).getByText("< $0.01 credits")).toBeInTheDocument();
    });
  });
});
