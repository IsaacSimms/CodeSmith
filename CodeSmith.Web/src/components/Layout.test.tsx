// == Layout Tests == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Layout } from "./Layout";
import { NavigationProvider } from "../contexts/NavigationContext";
import * as apiClient from "../lib/apiClient";

const isMsalConfigured = vi.fn();
const useIsAuthenticated = vi.fn();

vi.mock("../auth/msalConfig", () => ({
  isMsalConfigured: () => isMsalConfigured(),
}));

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: () => useIsAuthenticated(),
}));

// AuthControls uses MSAL hooks; isolate Layout from that surface.
vi.mock("../auth/AuthControls", () => ({
  AuthControls: () => <div data-testid="auth-controls" />,
}));

// Layout mounts ProviderPreferenceProvider + AccountDataPrefetch
vi.mock("../lib/apiClient", () => ({
  getProviders: vi.fn(() =>
    Promise.resolve({
      activeProvider: "Xai",
      availableProviders: ["Anthropic", "OpenAi", "Xai"],
    })
  ),
  getQuota: vi.fn(() =>
    Promise.resolve({ freeTokensUsed: 0, freeQuotaMax: 20_000, ipConstraint: "None" })
  ),
  getBalance: vi.fn(() => Promise.resolve({ paidCreditsUsd: 0 })),
}));

function makeLocalStorageStub(): Storage {
  const store = new Map<string, string>();
  return {
    getItem: (k) => (store.has(k) ? store.get(k)! : null),
    setItem: (k, v) => void store.set(k, String(v)),
    removeItem: (k) => void store.delete(k),
    clear: () => store.clear(),
    key: (i) => [...store.keys()][i] ?? null,
    get length() {
      return store.size;
    },
  } as Storage;
}

function renderLayoutAt(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <NavigationProvider>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/home" element={<div>home child</div>} />
              <Route path="/other" element={<div>other child</div>} />
              <Route path="/account" element={<div>account child</div>} />
            </Route>
          </Routes>
        </NavigationProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("Layout", () => {
  beforeEach(() => {
    isMsalConfigured.mockReturnValue(false);
    useIsAuthenticated.mockReturnValue(false);
    vi.mocked(apiClient.getQuota).mockClear();
    vi.mocked(apiClient.getBalance).mockClear();
    // ProviderPreferenceProvider's storage adapter reads localStorage on mount
    vi.stubGlobal("localStorage", makeLocalStorageStub());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders the CodeSmith logo linking to /home", () => {
    renderLayoutAt("/home");
    const link = screen.getByRole("link", { name: "CodeSmith" });
    expect(link).toHaveAttribute("href", "/home");
  });

  it("renders the child route via Outlet", () => {
    renderLayoutAt("/other");
    expect(screen.getByText("other child")).toBeInTheDocument();
  });

  it("shows a nav entry to /account when MSAL is unconfigured (dev reachability)", () => {
    isMsalConfigured.mockReturnValue(false);
    renderLayoutAt("/home");

    const link = screen.getByRole("link", { name: "Account" });
    expect(link).toHaveAttribute("href", "/account");
  });

  it("hides the Account nav entry when MSAL is configured (AuthControls owns entry later)", () => {
    isMsalConfigured.mockReturnValue(true);
    renderLayoutAt("/home");

    expect(screen.queryByRole("link", { name: "Account" })).not.toBeInTheDocument();
  });

  it("prefetches quota and balance when authenticated", async () => {
    isMsalConfigured.mockReturnValue(true);
    useIsAuthenticated.mockReturnValue(true);

    renderLayoutAt("/home");

    await waitFor(() => {
      expect(apiClient.getQuota).toHaveBeenCalled();
      expect(apiClient.getBalance).toHaveBeenCalled();
    });
  });

  it("prefetches neither quota nor balance when unauthenticated", async () => {
    isMsalConfigured.mockReturnValue(true);
    useIsAuthenticated.mockReturnValue(false);

    renderLayoutAt("/home");

    await waitFor(() => {
      expect(screen.getByText("home child")).toBeInTheDocument();
    });
    expect(apiClient.getQuota).not.toHaveBeenCalled();
    expect(apiClient.getBalance).not.toHaveBeenCalled();
  });
});
