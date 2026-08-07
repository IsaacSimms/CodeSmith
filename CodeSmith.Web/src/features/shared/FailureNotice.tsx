// == Failure Notice == //
import { Link } from "react-router-dom";
import type { ClientFailure } from "../../lib/clientError";

interface FailureNoticeProps {
  failure: ClientFailure;
  className?: string;
}

/// Shared presentational adapter for ClientFailure — title, detail, and optional action CTA.
/// Routing knowledge lives on the failure (`action.href`); this component only renders it.
export function FailureNotice({ failure, className = "" }: FailureNoticeProps) {
  return (
    <div data-testid="failure-notice" role="alert" className={`text-red-400 ${className}`.trim()}>
      <p className="text-sm font-medium">{failure.title}</p>
      <p className="mt-0.5 text-xs text-red-400/90">{failure.detail}</p>
      {failure.action && (
        <Link
          to={failure.action.href}
          className="mt-2 inline-block text-xs font-medium text-accent underline-offset-2 hover:underline"
        >
          {failure.action.label}
        </Link>
      )}
    </div>
  );
}
