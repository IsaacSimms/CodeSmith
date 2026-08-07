// == Scenario Selector Component == //
import type { ScenarioResponse, SystemLabCategory } from "../types";
import { categoryLabels } from "../types";

interface ScenarioSelectorProps {
  scenarios: ScenarioResponse[];
  isLoading: boolean;
  isStarting: boolean;
  isReady?: boolean; // provider preference resolved; default true for isolated unit tests
  onSelect: (scenarioId: string) => void;
}

export function ScenarioSelector({
  scenarios,
  isLoading,
  isStarting,
  isReady = true,
  onSelect,
}: ScenarioSelectorProps) {
  const categories = Object.keys(categoryLabels) as SystemLabCategory[];
  const byCategory = categories.reduce<Record<SystemLabCategory, ScenarioResponse[]>>(
    (acc, cat) => {
      acc[cat] = scenarios.filter((s) => s.category === cat);
      return acc;
    },
    {} as Record<SystemLabCategory, ScenarioResponse[]>
  );

  if (isLoading) {
    return <p className="text-center text-gray-500">Loading scenarios…</p>;
  }

  // Labeled gate while provider preference resolves — never an inert disabled list
  if (!isReady) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-xl font-bold text-white">System Lab</h1>
          <p className="mt-1 text-sm text-gray-400">
            Practice infrastructure and platform engineering judgment. Write a prose justification for each scenario; an AI evaluator scores your reasoning.
          </p>
        </div>
        <div className="flex justify-center">
          <button
            type="button"
            disabled
            className="rounded-lg bg-gray-700 px-6 py-3 text-sm font-semibold text-gray-300"
          >
            Starting up…
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-bold text-white">System Lab</h1>
        <p className="mt-1 text-sm text-gray-400">
          Practice infrastructure and platform engineering judgment. Write a prose justification for each scenario; an AI evaluator scores your reasoning.
        </p>
      </div>

      {categories.map((cat) => {
        const items = byCategory[cat];
        if (items.length === 0) return null;
        return (
          <section key={cat}>
            <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
              {categoryLabels[cat]}
            </h2>
            <div className="space-y-2">
              {items.map((scenario) => (
                <button
                  key={scenario.scenarioId}
                  onClick={() => onSelect(scenario.scenarioId)}
                  disabled={isStarting}
                  className="w-full rounded border border-gray-700 bg-gray-900 px-4 py-3 text-left transition-colors hover:border-accent hover:bg-gray-800 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-gray-200">{scenario.title}</p>
                      <p className="mt-0.5 line-clamp-2 text-xs text-gray-500">{scenario.description}</p>
                    </div>
                    <div className="flex shrink-0 flex-col items-end gap-1">
                      <DifficultyBadge difficulty={scenario.difficulty} />
                      <span className="text-xs text-gray-600">{scenario.rubric.length} criteria</span>
                    </div>
                  </div>
                </button>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}

// == Difficulty Badge Sub-component == //

function DifficultyBadge({ difficulty }: { difficulty: string }) {
  const colorClass =
    difficulty === "Easy"   ? "bg-green-900 text-green-300" :
    difficulty === "Medium" ? "bg-yellow-900 text-yellow-300" :
                              "bg-red-900 text-red-300";

  return (
    <span className={`rounded px-2 py-0.5 text-xs font-medium ${colorClass}`}>
      {difficulty}
    </span>
  );
}
