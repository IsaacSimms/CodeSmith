// == Provider Preference Hook == //
import { useState, useEffect } from "react";
import type { AiProvider } from "../features/chat/types";

const STORAGE_KEY = "codesmith_ai_provider";

export function useProviderPreference() {
  const [provider, setProviderState] = useState<AiProvider>("Anthropic");
  const [isLoaded, setIsLoaded] = useState(false);

  // Load from localStorage on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "Anthropic" || stored === "OpenAi") {
      setProviderState(stored);
    }
    setIsLoaded(true);
  }, []);

  // Persist to localStorage on change
  function setProvider(newProvider: AiProvider) {
    setProviderState(newProvider);
    localStorage.setItem(STORAGE_KEY, newProvider);
  }

  return { provider: isLoaded ? provider : "Anthropic", setProvider };
}
