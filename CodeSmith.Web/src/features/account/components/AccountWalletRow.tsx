// == Wallet row: credits + free quota; collapses when free grant is exhausted == //
import type { ReactNode } from "react";
import { useQuota } from "../hooks/useQuota";
import { FreeQuotaCard, isFreeGrantExhausted } from "./FreeQuotaCard";

export interface AccountWalletRowProps {
  credits: ReactNode; // Credits card (work 010) — slot so this row owns layout only
}

/// Two-col while the free grant is active (or still loading). One col + muted free line when spent.
export function AccountWalletRow({ credits }: AccountWalletRowProps) {
  const { data, isLoading } = useQuota();
  // Optimistic 2-col while loading — one reflow when exhausted resolves (ticket 005 #5).
  const exhausted = !isLoading && isFreeGrantExhausted(data);

  return (
    <div
      data-testid="account-wallet-row"
      className={
        exhausted
          ? "mb-6 grid grid-cols-1 gap-4"
          : "mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2"
      }
    >
      {credits}
      <FreeQuotaCard />
    </div>
  );
}
