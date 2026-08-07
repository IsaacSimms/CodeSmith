// == ProviderPicker (Preferences section) == //
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  ProviderPreferenceProvider,
  useProviderPreferenceContext,
} from "../../../contexts/ProviderPreferenceContext";
import * as apiClient from "../../../lib/apiClient";
import { ProviderPicker } from "./ProviderPicker";
import type { AiProvider } from "../../chat/types";

vi.mock("../../../lib/apiClient");

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

// Probe mirrors how the three surfaces build a provider-bearing request body
function WirePayloadProbe() {
  const { provider } = useProviderPreferenceContext();
  const payload = provider !== undefined ? { provider } : {};
  return <span data-testid="wire-provider">{(payload as { provider?: AiProvider }).provider ?? "omit"}</span>;
}

function renderPicker(available: string[] = ["Anthropic", "OpenAi", "Xai"]) {
  vi.mocked(apiClient.getProviders).mockResolvedValue({
    activeProvider: "Xai",
    availableProviders: available,
  });

  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ProviderPreferenceProvider>
        <ProviderPicker />
        <WirePayloadProbe />
      </ProviderPreferenceProvider>
    </QueryClientProvider>
  );
}

describe("ProviderPicker", () => {
  beforeEach(() => {
    vi.stubGlobal("localStorage", makeLocalStorageStub());
    vi.mocked(apiClient.getProviders).mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders the AI Provider label and Applies to this browser caption", async () => {
    renderPicker();

    expect(await screen.findByText("AI Provider")).toBeInTheDocument();
    expect(screen.getByText("Applies to this browser")).toBeInTheDocument();
  });

  it("renders provider buttons as Anthropic, xAI, OpenAI regardless of API order", async () => {
    // API enum order is Anthropic, OpenAi, Xai — UI display order is independent
    renderPicker(["OpenAi", "Xai", "Anthropic"]);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Anthropic" })).toBeInTheDocument();
    });

    const buttons = screen.getAllByRole("button");
    expect(buttons.map((b) => b.textContent)).toEqual(["Anthropic", "xAI", "OpenAI"]);
  });

  it("selecting a provider updates the shared context wire value surfaces send", async () => {
    const user = userEvent.setup();
    renderPicker();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "xAI" })).toBeInTheDocument();
    });

    // Default from server activeProvider until user chooses
    expect(screen.getByTestId("wire-provider")).toHaveTextContent("Xai");

    await user.click(screen.getByRole("button", { name: "OpenAI" }));

    expect(screen.getByTestId("wire-provider")).toHaveTextContent("OpenAi");
    expect(localStorage.getItem(STORAGE_KEY)).toBe("OpenAi");
  });
});
