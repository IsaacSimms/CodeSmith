// == UsageBar: shared fill math (pct clamp + min-fill); caller owns color == //

export interface UsageBarProps {
  used: number;
  max: number;
  fillClassName: string; // Flat accent for free-quota; ramp classes for context-window
}

/// Track fill only — no tooltip, footer, or color policy. TokenUsageBar and FreeQuotaCard compose this.
export function UsageBar({ used, max, fillClassName }: UsageBarProps) {
  const pct = Math.min((used / max) * 100, 100);
  const display = Math.max(pct, 0.3); // Never fully invisible at 0%

  return (
    <div className="h-1 w-full overflow-hidden rounded-full bg-gray-700">
      <div
        className={`h-full rounded-full transition-all duration-500 ${fillClassName}`}
        style={{ width: `${display}%` }}
      />
    </div>
  );
}
