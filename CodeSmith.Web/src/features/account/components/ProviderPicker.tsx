// == AI provider picker for Preferences (labels + display order live here) == //
import type { AiProvider } from "../../chat/types";
import { useProviderPreferenceContext } from "../../../contexts/ProviderPreferenceContext";

// Presentation with exactly one renderer — not the context (004 #8)
const providerLabels: Record<AiProvider, string> = {
  Anthropic: "Anthropic",
  OpenAi: "OpenAI",
  Xai: "xAI",
};

// Display order is a UI concern — independent of enum / API list order
const providerDisplayOrder: AiProvider[] = ["Anthropic", "Xai", "OpenAi"];

/// Reads/writes only through ProviderPreferenceContext — one source of truth.
export function ProviderPicker() {
  const { provider, setProvider, availableProviders } = useProviderPreferenceContext();

  const available = new Set(availableProviders);
  const ordered = providerDisplayOrder.filter((p) => available.has(p));

  // Hide until the providers query lands (or when only one is available — nothing to pick)
  if (ordered.length <= 1) return null;

  return (
    <div className="flex flex-col gap-3">
      <div>
        <p className="text-sm font-medium text-gray-200">AI Provider</p>
        <p className="mt-0.5 text-xs text-gray-400">Applies to this browser</p>
      </div>
      <div className="flex flex-wrap gap-2" aria-label="AI Provider">
        {ordered.map((p) => {
          const isSelected = provider === p;
          return (
            <button
              key={p}
              type="button"
              onClick={() => setProvider(p)}
              className={`rounded-full border px-4 py-1.5 text-sm font-medium transition-colors ${
                isSelected
                  ? "border-accent bg-accent/20 text-white"
                  : "border-gray-600 bg-gray-800 text-gray-300 hover:border-gray-500 hover:bg-gray-700"
              }`}
            >
              {providerLabels[p]}
            </button>
          );
        })}
      </div>
    </div>
  );
}
