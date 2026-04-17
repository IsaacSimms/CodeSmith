// == Challenge Panel Component == //
import type { ChallengeResponse, AttemptResult } from "../types";

interface ChallengePanelProps {
  challenge: ChallengeResponse;
  isSubmitting: boolean;
  lastAttempt: AttemptResult | null;
  attemptCount: number;
  onSubmit: () => void;
  onBack: () => void;
}

export function ChallengePanel({
  challenge,
  isSubmitting,
  lastAttempt,
  attemptCount,
  onSubmit,
  onBack,
}: ChallengePanelProps) {
  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Challenge Header == */}
      <div className="border-b border-gray-700 px-5 py-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h2 className="text-base font-bold text-gray-100">{challenge.title}</h2>
            <span className="mt-0.5 text-xs text-gray-500">{challenge.category.replace(/([A-Z])/g, " $1").trim()}</span>
          </div>
          <button
            onClick={onBack}
            className="shrink-0 rounded px-2 py-1 text-xs text-gray-500 transition-colors hover:bg-gray-700 hover:text-gray-300"
          >
            ← Back
          </button>
        </div>
      </div>

      {/* == Scrollable Content == */}
      <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">
        {/* == Challenge Description == */}
        <section>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">Challenge</h3>
          <p className="whitespace-pre-wrap text-sm leading-relaxed text-gray-300">
            {challenge.description}
          </p>
        </section>

        {/* == Test Inputs == */}
        <section>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
            Test Inputs ({challenge.testInputs.length})
          </h3>
          <div className="space-y-1">
            {challenge.testInputs.map((input) => (
              <div key={input.inputId} className="flex items-center gap-2 text-xs text-gray-400">
                <span className="text-gray-600">·</span>
                <span>{input.label}</span>
              </div>
            ))}
          </div>
        </section>

        {/* == Rubric == */}
        <section>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">Scoring Rubric</h3>
          <div className="space-y-2">
            {challenge.rubric.map((criterion) => (
              <div key={criterion.criterionId} className="rounded border border-gray-700 px-3 py-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium text-gray-300">{criterion.name}</span>
                  <span className="text-xs text-gray-500">{criterion.maxPoints} pts</span>
                </div>
                <p className="mt-0.5 text-xs text-gray-500">{criterion.description}</p>
              </div>
            ))}
          </div>
        </section>

        {/* == Attempt History (if any) == */}
        {attemptCount > 0 && lastAttempt && (
          <section>
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
              Best Score ({attemptCount} attempt{attemptCount !== 1 ? "s" : ""})
            </h3>
            <div className="rounded border border-gray-700 bg-gray-800/50 px-3 py-2">
              <span className="text-sm font-bold text-gray-200">
                {lastAttempt.totalScore}/{lastAttempt.maxScore} pts
              </span>
              <span className="ml-2 text-xs text-gray-500">
                ({lastAttempt.results.filter((r) => r.passed).length}/{lastAttempt.results.length} inputs passed)
              </span>
            </div>
          </section>
        )}
      </div>

      {/* == Submit Button == */}
      <div className="border-t border-gray-700 px-5 py-4">
        <button
          onClick={onSubmit}
          disabled={isSubmitting}
          className="w-full rounded bg-monokai-pink px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-monokai-pink-dark disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isSubmitting ? "Evaluating…" : "Submit Prompt"}
        </button>
        {attemptCount > 0 && (
          <p className="mt-2 text-center text-xs text-gray-600">
            Attempt {attemptCount + 1}
          </p>
        )}
      </div>
    </div>
  );
}
