// == Provider Preference Context == //
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import type { AiProvider } from "../features/chat/types";
import { useProviders } from "../features/chat/hooks/useProviders";
import { isAiProvider, useProviderPreference } from "../hooks/useProviderPreference";

// Bounded gate: after this, Start ungates and omits provider so the server
// applies ActiveProvider rather than the SPA guessing Anthropic.
const READY_TIMEOUT_MS = 3000;

interface ProviderPreferenceValue {
  provider: AiProvider | undefined; // undefined → omit from request (timeout fallback)
  setProvider: (p: AiProvider) => void;
  availableProviders: AiProvider[];
  isReady: boolean; // safe to send a provider-bearing request
}

const ProviderPreferenceContext = createContext<ProviderPreferenceValue | null>(null);

export function ProviderPreferenceProvider({ children }: { children: ReactNode }) {
  const query = useProviders();
  const { provider: storedOrDefault, setProvider, hasStored } = useProviderPreference(
    query.data?.activeProvider
  );

  const [timedOut, setTimedOut] = useState(false);

  // Bound the gate so a first-time user on a dead endpoint is not stuck forever
  useEffect(() => {
    if (hasStored || query.isSuccess) return;
    const id = window.setTimeout(() => setTimedOut(true), READY_TIMEOUT_MS);
    return () => window.clearTimeout(id);
  }, [hasStored, query.isSuccess]);

  const isReady = hasStored || query.isSuccess || timedOut;

  // Wire value: only send a provider when we have a real source of truth.
  // Timeout-only readiness → undefined (omit) so the server picks ActiveProvider.
  const provider: AiProvider | undefined =
    hasStored || query.isSuccess
      ? hasStored
        ? storedOrDefault
        : isAiProvider(query.data?.activeProvider)
          ? query.data.activeProvider
          : storedOrDefault
      : undefined;

  const availableProviders = (query.data?.availableProviders ?? []).filter(isAiProvider);

  return (
    <ProviderPreferenceContext.Provider
      value={{ provider, setProvider, availableProviders, isReady }}
    >
      {children}
    </ProviderPreferenceContext.Provider>
  );
}

export function useProviderPreferenceContext() {
  const ctx = useContext(ProviderPreferenceContext);
  if (!ctx) {
    throw new Error("useProviderPreferenceContext must be used within ProviderPreferenceProvider");
  }
  return ctx;
}
