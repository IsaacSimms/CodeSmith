// == Provider Preference Hook == //
import { useState, useEffect } from "react";
import type { AiProvider } from "../features/chat/types";

const STORAGE_KEY = "codesmith_ai_provider";

export function isAiProvider(value: string | null | undefined): value is AiProvider {
  return value === "Anthropic" || value === "OpenAi" || value === "Xai";
}

/**
 * Tracks the user's chosen AI provider.
 * A stored localStorage choice always wins. First-time users (no stored choice)
 * follow `serverDefault` — the backend's authoritative `activeProvider` — once it
 * arrives, falling back to "Anthropic" only if the server hasn't answered yet.
 * The server default is never persisted, so the default can move later.
 *
 * Internal storage adapter for ProviderPreferenceContext — feature components
 * import the context, not this hook.
 */
export function useProviderPreference(serverDefault?: string) {
  // Lazy init so returning users resolve on frame one and hasStored is trustworthy
  // at first render (Start gating depends on it).
  const [{ provider, hasStored }, setState] = useState(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (isAiProvider(stored)) return { provider: stored as AiProvider, hasStored: true };
    if (stored !== null) localStorage.removeItem(STORAGE_KEY); // self-heal invalid values
    return { provider: "Anthropic" as AiProvider, hasStored: false };
  });

  // First-time users follow the server default once it resolves (don't persist it)
  useEffect(() => {
    if (!hasStored && isAiProvider(serverDefault)) {
      setState((s) => ({ ...s, provider: serverDefault }));
    }
  }, [hasStored, serverDefault]);

  // Persist explicit user selections only
  function setProvider(newProvider: AiProvider) {
    setState({ provider: newProvider, hasStored: true });
    localStorage.setItem(STORAGE_KEY, newProvider);
  }

  return { provider, setProvider, hasStored };
}
