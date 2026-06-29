// == Provider Preference Hook Tests == //
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useProviderPreference } from "./useProviderPreference";

const STORAGE_KEY = "codesmith_ai_provider";

// The test environment's global localStorage is a partial polyfill; use a
// Map-backed stub so getItem/setItem/clear all behave predictably.
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

describe("useProviderPreference", () => {
  beforeEach(() => vi.stubGlobal("localStorage", makeLocalStorageStub()));
  afterEach(() => vi.unstubAllGlobals());

  it("defaults a first-time user to the server's active provider", async () => {
    const { result } = renderHook(() => useProviderPreference("Xai"));
    await waitFor(() => expect(result.current.provider).toBe("Xai"));
  });

  it("keeps a stored choice over the server default", async () => {
    localStorage.setItem(STORAGE_KEY, "OpenAi");
    const { result } = renderHook(() => useProviderPreference("Xai"));
    await waitFor(() => expect(result.current.provider).toBe("OpenAi"));
  });

  it("persists an explicit selection", async () => {
    const { result } = renderHook(() => useProviderPreference("Xai"));
    act(() => result.current.setProvider("Anthropic"));
    expect(localStorage.getItem(STORAGE_KEY)).toBe("Anthropic");
    expect(result.current.provider).toBe("Anthropic");
  });

  it("falls back to Anthropic when there is no server default and no stored value", async () => {
    const { result } = renderHook(() => useProviderPreference(undefined));
    await waitFor(() => expect(result.current.provider).toBe("Anthropic"));
  });
});
