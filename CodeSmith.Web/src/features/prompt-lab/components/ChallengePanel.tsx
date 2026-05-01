// == Challenge Panel Component == //
import type { ChallengeResponse, AttemptResult, TestInputSummary } from "../types";

interface ChallengePanelProps {
  challenge: ChallengeResponse;
  testInputs: TestInputSummary[];  // Session-specific generated inputs
  isSubmitting: boolean;
  lastAttempt: AttemptResult | null;
  attemptCount: number;
  onSubmit: () => void;
}

export function ChallengePanel({
  challenge,
  testInputs,
  isSubmitting,
  lastAttempt,
  attemptCount,
  onSubmit,
}: ChallengePanelProps) {
  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Challenge Header == */}
      <div className="border-b border-gray-700 px-5 py-4">
        <h2 className="text-base font-bold text-gray-100">{challenge.title}</h2>
        <span className="mt-0.5 text-xs text-gray-500">{challenge.category.replace(/([A-Z])/g, " $1").trim()}</span>
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
            Test Inputs ({testInputs.length})
          </h3>
          <div className="space-y-1">
            {testInputs.map((input) => (
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
            <div className="rounded border border-gray-700 bg-gray-900/50 px-3 py-2">
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
        <p className="mt-2 text-center text-xs text-gray-600">
          {isSubmitting ? (
            <span>Running {"·".repeat(3)}</span>
          ) : (
            <span><kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Enter</kbd> to submit · <kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Shift+Enter</kbd> for new line</span>
          )}
        </p>
        {attemptCount > 0 && !isSubmitting && (
          <p className="mt-1 text-center text-xs text-gray-600">Attempt {attemptCount + 1}</p>
        )}
      </div>
    </div>
  );
}
