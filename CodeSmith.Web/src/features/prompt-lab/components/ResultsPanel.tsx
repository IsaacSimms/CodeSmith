// == Results Panel Component == //
import { useState } from "react";
import type { AttemptResult, TestInputResult } from "../types";

interface ResultsPanelProps {
  result: AttemptResult | null;
  isEvaluating: boolean;
  onClear: () => void;
}

export function ResultsPanel({ result, isEvaluating, onClear }: ResultsPanelProps) {
  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Panel Header == */}
      <div className="flex items-center justify-between border-b border-gray-900 bg-gray-900 px-4 py-1.5">
        <div className="flex items-center gap-2">
          <h3 className="text-xs font-semibold text-gray-400">Results</h3>

          {/* == Status Badges == */}
          {isEvaluating && (
            <span className="rounded bg-blue-900 px-2 py-0.5 text-xs text-blue-300">Evaluating…</span>
          )}
          {!isEvaluating && result && (
            <ScoreBadge total={result.totalScore} max={result.maxScore} />
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
          <span className="text-gray-500">Running your prompt against test inputs…</span>
        )}

        {!isEvaluating && !result && (
          <span className="text-gray-500">Submit a prompt to see results here.</span>
        )}

        {result && (
          <div className="space-y-3">
            {/* == Per-Input Results == */}
            {result.results.map((inputResult) => (
              <TestInputRow key={inputResult.inputId} result={inputResult} />
            ))}

            {/* == Overall Feedback == */}
            {result.overallFeedback && (
              <div className="mt-4 border-t border-gray-700 pt-3">
                <p className="mb-1 text-xs font-semibold text-gray-500">Evaluator Feedback</p>
                <pre className="whitespace-pre-wrap break-words text-blue-300">{result.overallFeedback}</pre>
              </div>
            )}

            {/* == What You Were Fighting Against == */}
            {result.adversarialHint && (
              <div className="mt-4 border-t border-gray-700 pt-3">
                <p className="mb-1 text-xs font-semibold text-gray-500">What You Were Fighting Against</p>
                <p className="whitespace-pre-wrap break-words text-gray-400 italic">"{result.adversarialHint}"</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// == Score Badge Sub-component == //

function ScoreBadge({ total, max }: { total: number; max: number }) {
  const allPassed = total === max;
  const nonePassed = total === 0;
  const colorClass = allPassed
    ? "bg-green-900 text-green-300"
    : nonePassed
      ? "bg-red-900 text-red-300"
      : "bg-yellow-900 text-yellow-300";

  return (
    <span className={`rounded px-2 py-0.5 text-xs ${colorClass}`}>
      {total}/{max} pts
    </span>
  );
}

// == Test Input Row Sub-component == //

function TestInputRow({ result }: { result: TestInputResult }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded border border-gray-700 bg-gray-900/50">
      {/* == Row Header == */}
      <button
        onClick={() => setExpanded((e) => !e)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-gray-800/50"
      >
        {/* Pass/fail indicator */}
        <span className={`shrink-0 text-xs font-bold ${result.passed ? "text-green-400" : "text-red-400"}`}>
          {result.passed ? "✓" : "✗"}
        </span>
        <span className="flex-1 text-xs text-gray-200">{result.label}</span>
        {/* Per-input score */}
        <span className="text-xs text-gray-500">
          {result.criterionScores.reduce((s, c) => s + c.points, 0)}/
          {result.criterionScores.reduce((s, c) => s + c.maxPoints, 0)} pts
        </span>
        <span className="text-xs text-gray-600">{expanded ? "▲" : "▼"}</span>
      </button>

      {/* == Expanded Detail == */}
      {expanded && (
        <div className="border-t border-gray-700 px-3 py-2 space-y-2">
          {/* Criterion scores */}
          {result.criterionScores.length > 0 && (
            <div>
              <p className="mb-1 text-xs text-gray-500">Criteria</p>
              {result.criterionScores.map((score) => (
                <div key={score.criterionId} className="flex justify-between text-xs">
                  <span className="text-gray-400">{score.criterionName}</span>
                  <span className={score.points === score.maxPoints ? "text-green-400" : "text-yellow-400"}>
                    {score.points}/{score.maxPoints}
                  </span>
                </div>
              ))}
            </div>
          )}

          {/* User prompt */}
          {result.userMessage && (
            <div>
              <p className="mb-1 text-xs text-sky-400">User Prompt</p>
              <pre className="whitespace-pre-wrap break-words text-gray-200">{result.userMessage}</pre>
            </div>
          )}

          {/* Simulation output */}
          {result.simulationOutput && (
            <div>
              <p className="mb-1 text-xs text-gray-500">Model Output</p>
              <pre className="whitespace-pre-wrap break-words text-gray-200">{result.simulationOutput}</pre>
            </div>
          )}

          {/* Feedback */}
          {result.feedback && (
            <div>
              <p className="mb-1 text-xs text-gray-500">Feedback</p>
              <pre className="whitespace-pre-wrap break-words text-blue-300">{result.feedback}</pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
