// == Challenge Selector Component == //
import type { ChallengeResponse, ChallengeCategory } from "../types";
import { categoryLabels } from "../types";
import type { Difficulty } from "../../chat/types";

interface ChallengeSelectorProps {
  challenges: ChallengeResponse[];
  isLoading: boolean;
  isStarting: boolean;
  onSelect: (challengeId: string) => void;
}

export function ChallengeSelector({
  challenges,
  isLoading,
  isStarting,
  onSelect,
}: ChallengeSelectorProps) {
  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center">
        <span className="text-sm text-gray-400">Loading challenges…</span>
      </div>
    );
  }

  // Group challenges by category for display
  const grouped = challenges.reduce<Map<ChallengeCategory, ChallengeResponse[]>>((acc, c) => {
    const list = acc.get(c.category) ?? [];
    list.push(c);
    acc.set(c.category, list);
    return acc;
  }, new Map());

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Header == */}
      <div className="border-b border-gray-700 px-5 py-4">
        <h1 className="text-lg font-bold text-gray-100">Prompt Lab</h1>
        <p className="mt-1 text-xs text-gray-400">
          Select a challenge to begin. Craft prompts that work across all test inputs.
        </p>
      </div>

      {/* == Challenge List == */}
      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-5">
        {Array.from(grouped.entries()).map(([category, items]) => (
          <div key={category}>
            <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
              {categoryLabels[category]}
            </h2>
            <div className="space-y-2">
              {items.map((challenge) => (
                <ChallengeCard
                  key={challenge.challengeId}
                  challenge={challenge}
                  isStarting={isStarting}
                  onSelect={onSelect}
                />
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// == Challenge Card Sub-component == //

function ChallengeCard({
  challenge,
  isStarting,
  onSelect,
}: {
  challenge: ChallengeResponse;
  isStarting: boolean;
  onSelect: (id: string) => void;
}) {
  return (
    <button
      onClick={() => onSelect(challenge.challengeId)}
      disabled={isStarting}
      className="w-full rounded-lg border border-gray-700 bg-gray-900 px-4 py-3 text-left transition-colors hover:border-gray-500 hover:bg-gray-800 disabled:cursor-not-allowed disabled:opacity-50"
    >
      <div className="flex items-start justify-between gap-2">
        <span className="text-sm font-medium text-gray-100">{challenge.title}</span>
        <DifficultyBadge difficulty={challenge.difficulty} />
      </div>
      <p className="mt-1 line-clamp-2 text-xs text-gray-400">{challenge.description}</p>
      <p className="mt-2 text-xs text-gray-600">
        {challenge.testInputs.length} test inputs · {challenge.rubric.length} criteria
      </p>
    </button>
  );
}

// == Difficulty Badge Sub-component == //

function DifficultyBadge({ difficulty }: { difficulty: Difficulty }) {
  const colorClass =
    difficulty === "Easy"
      ? "bg-green-900 text-green-300"
      : difficulty === "Medium"
        ? "bg-yellow-900 text-yellow-300"
        : "bg-red-900 text-red-300";

  return (
    <span className={`shrink-0 rounded px-2 py-0.5 text-xs ${colorClass}`}>{difficulty}</span>
  );
}
