// == Token Usage Bar Component == //
import { UsageBar } from "./UsageBar";

interface TokenUsageBarProps {
  tokensUsed: number;
  contextWindowSize: number;
  label: string;        // Short name shown on the left, e.g. "Context" or "Prompt Size"
  description: string;  // Explanatory subtitle shown below the bar
}

export function TokenUsageBar({ tokensUsed, contextWindowSize, label, description }: TokenUsageBarProps) {
  const pct = Math.min((tokensUsed / contextWindowSize) * 100, 100);

  const barColor =
    pct >= 80 ? "bg-red-500" :
    pct >= 60 ? "bg-yellow-400" :
    "bg-emerald-500";

  return (
    <div className="border-t border-gray-700 bg-gray-850">
      {/* == Hover Hit Area + Tooltip Anchor == */}
      <div className="group relative cursor-default px-4 py-2">

        {/* == Hover Tooltip (floats below the bar) == */}
        <div className="pointer-events-none absolute top-full left-0 right-0 z-10 mt-1 rounded-md border border-gray-700 bg-gray-800 px-3 py-2 shadow-lg opacity-0 transition-opacity duration-200 group-hover:opacity-100">
          <div className="mb-1 flex items-baseline justify-between">
            <span className="text-xs font-semibold text-gray-100">{label}</span>
            <span className="font-mono text-xs tabular-nums text-gray-200">
              {tokensUsed.toLocaleString()} / {contextWindowSize.toLocaleString()} tokens
            </span>
          </div>
          <p className="text-xs text-gray-300">{description}</p>
        </div>

        {/* == Fill Bar (shared primitive) == */}
        <UsageBar used={tokensUsed} max={contextWindowSize} fillClassName={barColor} />

      </div>
    </div>
  );
}
