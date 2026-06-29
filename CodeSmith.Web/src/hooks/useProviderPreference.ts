// == Provider Preference Hook == //
import { useState, useEffect } from "react";
import type { AiProvider } from "../features/chat/types";

const STORAGE_KEY = "codesmith_ai_provider";

function isAiProvider(value: string | null | undefined): value is AiProvider {
  return value === "Anthropic" || value === "OpenAi" || value === "Xai";
}

/**
 * Tracks the user's chosen AI provider.
 * A stored localStorage choice always wins. First-time users (no stored choice)
 * follow `serverDefault` — the backend's authoritative `activeProvider` — once it
 * arrives, falling back to "Anthropic" only if the server hasn't answered yet.
 * The server default is never persisted, so the default can move later.
 */
export function useProviderPreference(serverDefault?: string) {
  const [provider, setProviderState] = useState<AiProvider>("Anthropic");
  const [hasStored, setHasStored] = useState(false);
  const [isLoaded, setIsLoaded] = useState(false);

  // Load any stored choice once on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (isAiProvider(stored)) {
      setProviderState(stored);
      setHasStored(true);
    }
    setIsLoaded(true);
  }, []);

  // First-time users follow the server default once it resolves (don't persist it)
  useEffect(() => {
    if (isLoaded && !hasStored && isAiProvider(serverDefault)) {
      setProviderState(serverDefault);
    }
  }, [isLoaded, hasStored, serverDefault]);

  // Persist explicit user selections only
  function setProvider(newProvider: AiProvider) {
    setProviderState(newProvider);
    setHasStored(true);
    localStorage.setItem(STORAGE_KEY, newProvider);
  }

  return { provider, setProvider };
}
