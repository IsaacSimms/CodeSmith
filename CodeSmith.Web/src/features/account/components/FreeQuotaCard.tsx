// == Free-quota card: active grant bar or permanent exhausted line == //
import { UsageBar } from "../../../components/UsageBar";
import { interpretError } from "../../../lib/clientError";
import { formatTokenCount } from "../formatters";
import { useQuota } from "../hooks/useQuota";
import type { IpConstraint, QuotaResponse } from "../types";
import { AccountSection } from "./AccountSection";

/// True when the one-time grant is fully spent (permanent — grant never resets).
export function isFreeGrantExhausted(quota: QuotaResponse | undefined): boolean {
  if (!quota) return false;
  return quota.freeQuotaMax > 0 && quota.freeTokensUsed >= quota.freeQuotaMax;
}

/// Active grant: AccountSection + visible used/max + flat accent UsageBar + optional IP notice.
/// Exhausted: one muted page line, no card chrome (wallet row goes full-width credits).
export function FreeQuotaCard() {
  const { data, isLoading, error } = useQuota();

  if (!isLoading && !error && isFreeGrantExhausted(data)) {
    const used = formatTokenCount(data!.freeTokensUsed);
    const max = formatTokenCount(data!.freeQuotaMax);
    return (
      <p
        data-testid="free-quota-exhausted"
        className="text-sm text-gray-500"
      >
        Free tokens — {used} of {max} used
      </p>
    );
  }

  return (
    <AccountSection
      title="Free tokens"
      anchorId="free-quota"
      isLoading={isLoading}
      error={error ? interpretError(error) : null}
    >
      {data ? <ActiveGrantBody quota={data} /> : null}
    </AccountSection>
  );
}

// == Active grant body: headline counts + flat fill + IP binding notice == //
function ActiveGrantBody({ quota }: { quota: QuotaResponse }) {
  const { freeTokensUsed, freeQuotaMax, ipConstraint } = quota;

  return (
    <div className="flex flex-col gap-3">
      <p className="font-mono text-sm tabular-nums text-gray-100">
        {formatTokenCount(freeTokensUsed)} / {formatTokenCount(freeQuotaMax)}
        <span className="ml-1.5 font-sans text-gray-400">tokens used</span>
      </p>

      <UsageBar used={freeTokensUsed} max={freeQuotaMax} fillClassName="bg-accent" />

      {ipConstraint !== "None" && <IpConstraintNotice constraint={ipConstraint} />}
    </div>
  );
}

// Notice only when per-IP headroom binds — never show a per-IP number (ticket 001 #7).
function IpConstraintNotice({ constraint }: { constraint: Exclude<IpConstraint, "None"> }) {
  const copy =
    constraint === "Exhausted"
      ? "Free tokens are currently unavailable on this network."
      : "Free tokens from this network are limited — available free usage may be lower than the grant above.";

  return (
    <p data-testid="free-quota-ip-notice" className="text-xs text-gray-400">
      {copy}
    </p>
  );
}
