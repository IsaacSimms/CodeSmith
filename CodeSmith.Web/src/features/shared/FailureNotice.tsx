// == Failure Notice == //
import type { ClientFailure } from "../../lib/clientError";

interface FailureNoticeProps {
  failure: ClientFailure;
  className?: string;
}

/// Shared presentational adapter for ClientFailure — title + detail, no CTA.
export function FailureNotice({ failure, className = "" }: FailureNoticeProps) {
  return (
    <div data-testid="failure-notice" role="alert" className={`text-red-400 ${className}`.trim()}>
      <p className="text-sm font-medium">{failure.title}</p>
      <p className="mt-0.5 text-xs text-red-400/90">{failure.detail}</p>
    </div>
  );
}
