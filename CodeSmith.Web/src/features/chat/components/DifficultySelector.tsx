// == Difficulty Selector Component == //
import { useState } from "react";
import type { Difficulty, Language } from "../types";
import { languageLabels } from "../types";

interface DifficultySelectorProps {
  onSelect: (difficulty: Difficulty, language: Language) => void;
  isLoading: boolean;
  initialLanguage?: Language;
}

const difficulties: Difficulty[] = ["Easy", "Medium", "Hard"];
const languages: Language[] = ["CSharp", "Cpp", "Go", "Rust", "Python", "Java"];

const difficultyColors: Record<Difficulty, string> = {
  Easy:   "bg-green-600 hover:bg-green-700",
  Medium: "bg-yellow-600 hover:bg-yellow-700",
  Hard:   "bg-red-600 hover:bg-red-700",
};

const DEFAULT_LANGUAGE: Language = "CSharp";

export function DifficultySelector({ onSelect, isLoading, initialLanguage }: DifficultySelectorProps) {
  const [language, setLanguage] = useState<Language>(initialLanguage ?? DEFAULT_LANGUAGE);

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
                  : "border-gray-600 bg-gray-800 text-gray-300 hover:border-gray-500 hover:bg-gray-700"
              }`}
            >
              {languageLabels[lang]}
            </button>
          );
        })}
      </div>

      {/* == Difficulty Buttons == */}
      <div className="flex gap-4">
        {difficulties.map((d) => (
          <button
            key={d}
            onClick={() => onSelect(d, language)}
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
