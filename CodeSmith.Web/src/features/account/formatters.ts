// == Money and token formatters == //

// Balance / pack prices: 2dp. `$0.00` is reserved for a true zero; a spendable sub-cent
// remainder renders `< $0.01` so it never impersonates zero (ticket 008 #11).
export function formatBalanceUsd(amount: number): string {
  if (amount === 0) return "$0.00";
  if (amount > 0 && amount < 0.01) return "< $0.01";
  return formatUsd(amount, 2);
}

// Ledger Spend rows keep 4dp so sub-cent usage stays legible; TopUp rows stay 2dp (ticket 008 #6).
export function formatLedgerUsd(amount: number, type: "Spend" | "TopUp"): string {
  return formatUsd(amount, type === "Spend" ? 4 : 2);
}

export function formatTokenCount(n: number): string {
  return n.toLocaleString("en-US");
}

// Account grant remaining. Reservation holds can make used temporarily overshoot max —
// clamp at 0 so nav/page never render a negative free count (ticket 002 #8 / work 008).
export function freeTokensRemaining(freeTokensUsed: number, freeQuotaMax: number): number {
  return Math.max(0, freeQuotaMax - freeTokensUsed);
}

// Known ledger Feature → display label. Unmapped values fall through to the raw string
// so a new server Feature never renders blank (ticket 005 #9).
const FEATURE_LABELS: Record<string, string> = {
  "Tutoring:Guidance": "Paired Programmer · Guidance",
  "Tutoring:ProblemGeneration": "Paired Programmer · Problem",
  "PromptLab:Chat": "Prompt Lab · Chat",
  "PromptLab:Evaluate": "Prompt Lab · Evaluate",
  "PromptLab:Simulate": "Prompt Lab · Simulate",
  "PromptLab:TestInputGeneration": "Prompt Lab · Test inputs",
  "SystemLab:Chat": "System Lab · Chat",
  "SystemLab:Evaluate": "System Lab · Evaluate",
  "Billing:TopUp": "Purchase",
};

export function ledgerFeatureLabel(feature: string | null | undefined): string {
  if (!feature) return "—";
  return FEATURE_LABELS[feature] ?? feature;
}

function formatUsd(amount: number, fractionDigits: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}
