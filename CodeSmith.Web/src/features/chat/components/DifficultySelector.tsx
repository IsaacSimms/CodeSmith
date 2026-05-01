// == Difficulty Selector Component == //
import { useState } from "react";
import type { Difficulty, Language, AiProvider } from "../types";
import { languageLabels } from "../types";
import { useProviders } from "../hooks/useProviders";

interface DifficultySelectorProps {
  onSelect: (difficulty: Difficulty, language: Language, provider: AiProvider) => void;
  isLoading: boolean;
  initialLanguage?: Language;
}

const difficulties: Difficulty[] = ["Easy", "Medium", "Hard"];
const languages: Language[] = ["CSharp", "Cpp", "Go", "Rust", "Python", "Java", "TypeScript"];

const difficultyColors: Record<Difficulty, string> = {
  Easy:   "bg-green-600 hover:bg-green-700",
  Medium: "bg-yellow-600 hover:bg-yellow-700",
  Hard:   "bg-red-600 hover:bg-red-700",
};

const providerLabels: Record<AiProvider, string> = {
  Anthropic: "Anthropic Claude",
  OpenAi:    "OpenAI",
};

const DEFAULT_LANGUAGE: Language = "CSharp";

export function DifficultySelector({ onSelect, isLoading, initialLanguage }: DifficultySelectorProps) {
  const [language, setLanguage] = useState<Language>(initialLanguage ?? DEFAULT_LANGUAGE);
  const { data: providersData } = useProviders();

  const availableProviders = (providersData?.availableProviders ?? ["Anthropic"]) as AiProvider[];
  const defaultProvider   = (providersData?.activeProvider ?? "Anthropic") as AiProvider;
  const [provider, setProvider] = useState<AiProvider | null>(null);

  // Use the locally selected provider, or fall back to the server default once loaded
  const effectiveProvider: AiProvider = provider ?? defaultProvider;

  return (
    <div className="flex flex-col items-center gap-6 p-8">
      <h1 className="text-3xl font-bold text-white">CodeSmith</h1>
      <p className="text-gray-400">Pick a language and difficulty to begin</p>

      {/* == Language Pills == */}
      <div className="flex flex-wrap justify-center gap-2" role="radiogroup" aria-label="Language">
        {languages.map((lang) => {
          const isSelected = language === lang;
          return (
            <button
              key={lang}
              type="button"
              role="radio"
              aria-checked={isSelected}
              onClick={() => setLanguage(lang)}
              disabled={isLoading}
              className={`rounded-full border px-4 py-1.5 text-sm font-medium transition-colors disabled:opacity-50 ${
                isSelected
                  ? "border-monokai-pink bg-monokai-pink text-white"
                  : "border-gray-600 bg-gray-900 text-gray-300 hover:border-gray-500 hover:bg-gray-800"
              }`}
            >
              {languageLabels[lang]}
            </button>
          );
        })}
      </div>

      {/* == Provider Pills (only shown when multiple providers are available) == */}
      {availableProviders.length > 1 && (
        <div className="flex flex-wrap justify-center gap-2" role="radiogroup" aria-label="AI Provider">
          {availableProviders.map((p) => {
            const isSelected = effectiveProvider === p;
            return (
              <button
                key={p}
                type="button"
                role="radio"
                aria-checked={isSelected}
                onClick={() => setProvider(p)}
                disabled={isLoading}
                className={`rounded-full border px-4 py-1.5 text-sm font-medium transition-colors disabled:opacity-50 ${
                  isSelected
                    ? "border-blue-400 bg-blue-600 text-white"
                    : "border-gray-600 bg-gray-900 text-gray-300 hover:border-gray-500 hover:bg-gray-800"
                }`}
              >
                {providerLabels[p] ?? p}
              </button>
            );
          })}
        </div>
      )}

      {/* == Difficulty Buttons == */}
      <div className="flex gap-4">
        {difficulties.map((d) => (
          <button
            key={d}
            onClick={() => onSelect(d, language, effectiveProvider)}
            disabled={isLoading}
            className={`rounded-lg px-6 py-3 font-semibold text-white transition-colors disabled:opacity-50 ${difficultyColors[d]}`}
          >
            {d}
          </button>
        ))}
      </div>

      {isLoading && <p className="text-gray-400">Generating problem...</p>}
    </div>
  );
}
