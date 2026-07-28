// == Difficulty Selector Component == //
import { useState } from "react";
import type { Difficulty, Language, ProblemFocus, ProblemTopic } from "../types";
import {
  languageLabels,
  problemFocuses,
  problemFocusLabels,
  problemTopics,
  problemTopicLabels,
} from "../types";

interface DifficultySelectorProps {
  onSelect: (difficulty: Difficulty, language: Language) => void;
  isLoading: boolean;
  initialLanguage?: Language;
  // Focus and Topic are controlled by ChatWindow so a pick survives "generate new problem" and the
  // in-app nav reset; only a page reload returns them to Random.
  focus: ProblemFocus;
  topic: ProblemTopic;
  onFocusChange: (focus: ProblemFocus) => void;
  onTopicChange: (topic: ProblemTopic) => void;
}

const difficulties: Difficulty[] = ["Easy", "Medium", "Hard"];
const languages: Language[] = ["CSharp", "Cpp", "Go", "Rust", "Python", "Java", "TypeScript"];

const difficultyColors: Record<Difficulty, string> = {
  Easy:   "bg-green-600 hover:bg-green-700",
  Medium: "bg-yellow-600 hover:bg-yellow-700",
  Hard:   "bg-red-600 hover:bg-red-700",
};

const DEFAULT_LANGUAGE: Language = "CSharp";

const selectClasses =
  "rounded-lg border border-gray-600 bg-gray-900 px-3 py-1.5 text-sm text-gray-200 transition-colors hover:border-gray-500 disabled:opacity-50";

export function DifficultySelector({
  onSelect,
  isLoading,
  initialLanguage,
  focus,
  topic,
  onFocusChange,
  onTopicChange,
}: DifficultySelectorProps) {
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
                  ? "border-accent bg-accent text-white"
                  : "border-gray-600 bg-gray-900 text-gray-300 hover:border-gray-500 hover:bg-gray-800"
              }`}
            >
              {languageLabels[lang]}
            </button>
          );
        })}
      </div>

      {/* == Focus Select == */}
      <div className="flex items-center gap-2">
        <label htmlFor="problem-focus" className="text-sm text-gray-400">
          Focus
        </label>
        <select
          id="problem-focus"
          value={focus}
          onChange={(e) => onFocusChange(e.target.value as ProblemFocus)}
          disabled={isLoading}
          className={selectClasses}
        >
          {problemFocuses.map((f) => (
            <option key={f} value={f}>
              {problemFocusLabels[f]}
            </option>
          ))}
        </select>
      </div>

      {/* == Advanced: Topic == */}
      {/* The summary carries the current topic so a pinned value stays readable while collapsed */}
      <details className="w-full max-w-md rounded-lg border border-gray-700 bg-gray-900/50 px-4 py-2">
        <summary data-testid="advanced-summary" className="cursor-pointer text-sm text-gray-400 marker:text-gray-600">
          Advanced — Topic: <span className="text-gray-300">{problemTopicLabels[topic]}</span>
        </summary>

        <div className="mt-3 flex flex-col gap-2">
          <p className="text-xs text-gray-500">
            Pinning a topic reduces problem variety across regenerations. Leave on Random for the widest range.
          </p>
          <div className="flex items-center gap-2">
            <label htmlFor="problem-topic" className="text-sm text-gray-400">
              Topic
            </label>
            <select
              id="problem-topic"
              value={topic}
              onChange={(e) => onTopicChange(e.target.value as ProblemTopic)}
              disabled={isLoading}
              className={selectClasses}
            >
              {problemTopics.map((t) => (
                <option key={t} value={t}>
                  {problemTopicLabels[t]}
                </option>
              ))}
            </select>
          </div>
        </div>
      </details>

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
