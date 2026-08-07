// == Provider Preference Context Tests == //
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { render, screen, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  ProviderPreferenceProvider,
  useProviderPreferenceContext,
} from "./ProviderPreferenceContext";
import * as apiClient from "../lib/apiClient";

vi.mock("../lib/apiClient");

const STORAGE_KEY = "codesmith_ai_provider";

// Map-backed stub so getItem/setItem/removeItem behave predictably in jsdom
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

function Probe() {
  const { provider, isReady, availableProviders } = useProviderPreferenceContext();
  return (
    <div>
      <span data-testid="provider">{provider ?? "omit"}</span>
      <span data-testid="ready">{String(isReady)}</span>
      <span data-testid="available">{availableProviders.join(",")}</span>
    </div>
  );
}

function renderWithProviders(
  ui: React.ReactElement,
  queryOptions?: { retry?: number }
) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: queryOptions?.retry ?? false,
        retryDelay: 1,
        gcTime: Infinity,
      },
    },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ProviderPreferenceProvider>{ui}</ProviderPreferenceProvider>
    </QueryClientProvider>
  );
}

describe("ProviderPreferenceContext", () => {
  beforeEach(() => {
    vi.stubGlobal("localStorage", makeLocalStorageStub());
    vi.mocked(apiClient.getProviders).mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("is ready on first render when a stored choice exists, without waiting on the query", () => {
    localStorage.setItem(STORAGE_KEY, "OpenAi");
    // Query never resolves — readiness must not depend on it
    vi.mocked(apiClient.getProviders).mockReturnValue(new Promise(() => {}));

    renderWithProviders(<Probe />);

    expect(screen.getByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("provider")).toHaveTextContent("OpenAi");
  });

  it("is not ready until the providers query succeeds when there is no stored choice", async () => {
    let resolveProviders!: (value: {
      activeProvider: string;
      availableProviders: string[];
    }) => void;
    vi.mocked(apiClient.getProviders).mockReturnValue(
      new Promise((resolve) => {
        resolveProviders = resolve;
      })
    );

    renderWithProviders(<Probe />);

    expect(screen.getByTestId("ready")).toHaveTextContent("false");
    expect(screen.getByTestId("provider")).toHaveTextContent("omit");

    await act(async () => {
      resolveProviders({
        activeProvider: "Xai",
        availableProviders: ["Anthropic", "OpenAi", "Xai"],
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId("ready")).toHaveTextContent("true");
    });
    expect(screen.getByTestId("provider")).toHaveTextContent("Xai");
    expect(screen.getByTestId("available")).toHaveTextContent("Anthropic,OpenAi,Xai");
  });

  it("removes a stored invalid value and does not treat it as a choice", async () => {
    localStorage.setItem(STORAGE_KEY, "NotAProvider");
    vi.mocked(apiClient.getProviders).mockResolvedValue({
      activeProvider: "Xai",
      availableProviders: ["Anthropic", "OpenAi", "Xai"],
    });

    renderWithProviders(<Probe />);

    // Invalid value must not count as hasStored — still waiting on query
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(screen.getByTestId("ready")).toHaveTextContent("false");

    await waitFor(() => {
      expect(screen.getByTestId("ready")).toHaveTextContent("true");
    });
    expect(screen.getByTestId("provider")).toHaveTextContent("Xai");
  });

  it("after ~3s of a failing query, becomes ready and omits provider from the wire value", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.mocked(apiClient.getProviders).mockRejectedValue(new Error("cold start 502"));

    renderWithProviders(<Probe />, { retry: false });

    expect(screen.getByTestId("ready")).toHaveTextContent("false");
    expect(screen.getByTestId("provider")).toHaveTextContent("omit");

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3000);
    });

    expect(screen.getByTestId("ready")).toHaveTextContent("true");
    // Omit provider so the server applies ActiveProvider — never guess Anthropic
    expect(screen.getByTestId("provider")).toHaveTextContent("omit");
  });
});
