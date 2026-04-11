// == Terminal Panel Component == //
import type { RunCodeResponse } from "../types";

interface TerminalPanelProps {
  result: RunCodeResponse | null;
  isRunning: boolean;
  onClear: () => void;
}

export function TerminalPanel({ result, isRunning, onClear }: TerminalPanelProps) {
  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Panel Header == */}
      <div className="flex items-center justify-between border-b border-gray-900 bg-gray-800 px-4 py-1.5">
        <div className="flex items-center gap-2">
          <h3 className="text-xs font-semibold text-gray-400">Output</h3>

          {/* == Status Badges == */}
          {isRunning && (
            <span className="rounded bg-blue-900 px-2 py-0.5 text-xs text-blue-300">Running…</span>
          )}
          {!isRunning && result && (
            <span
              className={`rounded px-2 py-0.5 text-xs ${
                result.exitCode === 0
                  ? "bg-green-900 text-green-300"
                  : "bg-red-900 text-red-300"
              }`}
            >
              Exit: {result.exitCode}
            </span>
          )}
          {!isRunning && result?.timedOut && (
            <span className="rounded bg-yellow-900 px-2 py-0.5 text-xs text-yellow-300">
              Timed out (10s)
            </span>
          )}
        </div>

        <button
          onClick={onClear}
          className="rounded px-2 py-0.5 text-xs font-medium text-gray-400 transition-colors hover:bg-gray-700 hover:text-white"
        >
          Clear
        </button>
      </div>

      {/* == Output Area == */}
      <div className="flex-1 overflow-y-auto bg-[#272822] p-3 font-mono text-sm">
        {isRunning && !result && (
          <span className="text-gray-500">Running code…</span>
        )}

        {result && (
          <>
            {result.stdout && (
              <pre className="whitespace-pre-wrap break-words text-gray-200">{result.stdout}</pre>
            )}
            {result.stderr && (
              <pre className="mt-1 whitespace-pre-wrap break-words text-red-400">{result.stderr}</pre>
            )}
            {!result.stdout && !result.stderr && (
              <span className="text-gray-500">(no output)</span>
            )}
          </>
        )}
      </div>
    </div>
  );
}
