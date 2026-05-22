// == Attempt Results Panel Component == //
import { useState } from "react";
import type { AttemptResult, CriterionScore, TradeoffResult } from "../types";

interface AttemptResultsPanelProps {
  result: AttemptResult | null;
  isEvaluating: boolean;
  onClear: () => void;
}

export function AttemptResultsPanel({ result, isEvaluating, onClear }: AttemptResultsPanelProps) {
  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Panel Header == */}
      <div className="flex items-center justify-between border-b border-gray-900 bg-gray-900 px-4 py-1.5">
        <div className="flex items-center gap-2">
          <h3 className="text-xs font-semibold text-gray-400">Evaluation</h3>

          {isEvaluating && (
            <span className="rounded bg-blue-900 px-2 py-0.5 text-xs text-blue-300">Evaluating…</span>
          )}
          {!isEvaluating && result && (
            <ScoreBadge total={result.totalScore} max={result.maxScore} totalDeduction={result.dimensionDeductions.reduce((sum, d) => sum + d.deduction, 0)} />
          )}
        </div>
        {result && (
          <button
            onClick={onClear}
            className="rounded px-2 py-0.5 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-700 hover:text-white"
          >
            Clear
          </button>
        )}
      </div>

      {/* == Output Area == */}
      <div className="flex-1 overflow-y-auto bg-[#272822] p-3 font-mono text-sm">
        {isEvaluating && !result && (
          <span className="text-gray-500">Evaluating your justification…</span>
        )}
        {!isEvaluating && !result && (
          <span className="text-gray-500">Submit your justification to see evaluation results here.</span>
        )}
        {result && (
          <div className="space-y-4">
            {/* == Rubric Score Breakdown == */}
            <section>
              <p className="mb-1.5 text-xs font-semibold text-gray-500">Criteria</p>
              <div className="space-y-1">
                {result.criterionScores.map((score) => (
                  <CriterionRow key={score.criterionId} score={score} />
                ))}
              </div>
            </section>

            {/* == Dimension Deductions (only rendered when a deduction was applied) == */}
            {result.dimensionDeductions.filter((d) => d.deduction > 0).map((d) => (
              <section key={d.dimensionName}>
                <p className="mb-1 text-xs font-semibold text-red-500">{d.dimensionName} Deduction: -{d.deduction} pts</p>
                {d.feedback && (
                  <pre className="whitespace-pre-wrap break-words text-red-400">{d.feedback}</pre>
                )}
              </section>
            ))}

            {/* == Score Summary == */}
            <section className="border-t border-gray-700 pt-3">
              <div className="flex items-baseline gap-2">
                <span className="text-lg font-bold text-gray-100">{result.totalScore}/{result.maxScore}</span>
                {result.dimensionDeductions.some((d) => d.deduction > 0) && (
                  <span className="text-xs text-gray-500">
                    ({result.rubricScore} rubric
                    {result.dimensionDeductions
                      .filter((d) => d.deduction > 0)
                      .map((d) => ` − ${d.deduction} ${d.dimensionName.toLowerCase()}`)
                      .join("")})
                  </span>
                )}
              </div>
            </section>

            {/* == Tradeoff Results == */}
            {result.tradeoffResults.length > 0 && (
              <section>
                <p className="mb-1.5 text-xs font-semibold text-gray-500">Required Tradeoffs</p>
                <div className="space-y-2">
                  {result.tradeoffResults.map((tr, i) => (
                    <TradeoffRow key={i} result={tr} />
                  ))}
                </div>
              </section>
            )}

            {/* == Overall Feedback == */}
            {result.overallFeedback && (
              <section className="border-t border-gray-700 pt-3">
                <p className="mb-1 text-xs font-semibold text-gray-500">Overall Feedback</p>
                <pre className="whitespace-pre-wrap break-words text-blue-300">{result.overallFeedback}</pre>
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// == Score Badge == //

function ScoreBadge({ total, max, totalDeduction }: { total: number; max: number; totalDeduction: number }) {
  const pct        = max > 0 ? total / max : 0;
  const colorClass = pct >= 0.9 ? "bg-green-900 text-green-300"
                   : pct >= 0.6 ? "bg-yellow-900 text-yellow-300"
                   :              "bg-red-900 text-red-300";
  return (
    <span className={`rounded px-2 py-0.5 text-xs ${colorClass}`}>
      {total}/{max} pts{totalDeduction > 0 && ` (−${totalDeduction})`}
    </span>
  );
}

// == Criterion Row == //

function CriterionRow({ score }: { score: CriterionScore }) {
  const full = score.points === score.maxPoints;
  return (
    <div className="flex items-center justify-between text-xs">
      <span className="text-gray-400">{score.criterionName}</span>
      <span className={full ? "text-green-400" : score.points > 0 ? "text-yellow-400" : "text-red-400"}>
        {score.points}/{score.maxPoints}
      </span>
    </div>
  );
}

// == Tradeoff Row == //

function TradeoffRow({ result }: { result: TradeoffResult }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded border border-gray-700 bg-gray-900/50">
      <button
        onClick={() => setExpanded((e) => !e)}
        className="flex w-full items-start gap-2 px-3 py-2 text-left hover:bg-gray-800/50"
      >
        <span className={`mt-0.5 shrink-0 text-xs font-bold ${result.engaged ? "text-green-400" : "text-red-400"}`}>
          {result.engaged ? "✓" : "✗"}
        </span>
        <span className="flex-1 text-xs leading-relaxed text-gray-300">{result.tradeoffQuestion}</span>
        <span className="shrink-0 text-xs text-gray-600">{expanded ? "▲" : "▼"}</span>
      </button>
      {expanded && result.feedback && (
        <div className="border-t border-gray-700 px-3 py-2">
          <pre className="whitespace-pre-wrap break-words text-xs text-blue-300">{result.feedback}</pre>
        </div>
      )}
    </div>
  );
}
