// == Difficulty Selector Component == //
import type { Difficulty } from "../types";

interface DifficultySelectorProps {
  onSelect: (difficulty: Difficulty) => void;
  isLoading: boolean;
}

const difficulties: Difficulty[] = ["Easy", "Medium", "Hard"];

const difficultyColors: Record<Difficulty, string> = {
  Easy: "bg-green-600 hover:bg-green-700",
  Medium: "bg-yellow-600 hover:bg-yellow-700",
  Hard: "bg-red-600 hover:bg-red-700",
};

export function DifficultySelector({ onSelect, isLoading }: DifficultySelectorProps) {
  return (
    <div className="flex flex-col items-center gap-8 p-8">
      <h1 className="text-3xl font-bold text-white">CodeSmith</h1>
      <p className="text-gray-400">Select a difficulty to begin</p>
      <div className="flex gap-4">
        {difficulties.map((d) => (
          <button
            key={d}
            onClick={() => onSelect(d)}
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
