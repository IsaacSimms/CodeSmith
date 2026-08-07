// == Account section chrome + loading/error branch + hash arrival == //
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useLocation } from "react-router-dom";
import type { ClientFailure } from "../../../lib/clientError";
import { FailureNotice } from "../../shared/FailureNotice";

export interface AccountSectionProps {
  title: string;
  anchorId: string;
  isLoading: boolean;
  error: ClientFailure | null;
  children: ReactNode;
}

const HASH_HIGHLIGHT_MS = 2000;

/// Shared card wrapper: chrome always mounts at stable min-height; only the body swaps.
export function AccountSection({ title, anchorId, isLoading, error, children }: AccountSectionProps) {
  const location = useLocation();
  const sectionRef = useRef<HTMLElement>(null);
  const [hashHighlight, setHashHighlight] = useState(false);

  // == Hash arrival: scroll + transient accent ring (nested scroller makes bare anchors inert) == //
  useEffect(() => {
    const hash = location.hash.replace(/^#/, "");
    if (hash !== anchorId || !sectionRef.current) return;

    sectionRef.current.scrollIntoView({ block: "start" });
    setHashHighlight(true);

    const timer = window.setTimeout(() => setHashHighlight(false), HASH_HIGHLIGHT_MS);
    return () => window.clearTimeout(timer);
  }, [location.hash, anchorId]);

  return (
    <section
      ref={sectionRef}
      id={anchorId}
      data-testid={`account-section-${anchorId}`}
      data-hash-highlight={hashHighlight ? "true" : undefined}
      className={[
        "rounded-xl border border-gray-700 bg-gray-900 p-5",
        "transition-shadow duration-1000",
        hashHighlight ? "ring-2 ring-accent shadow-[0_0_0_1px] shadow-accent" : "ring-2 ring-transparent",
      ].join(" ")}
    >
      <h2 className="mb-3 text-base font-semibold text-white">{title}</h2>
      <div data-testid="account-section-body" className="min-h-24">
        {isLoading ? (
          <p className="text-sm text-gray-400">Loading…</p>
        ) : error ? (
          <FailureNotice failure={error} />
        ) : (
          children
        )}
      </div>
    </section>
  );
}
