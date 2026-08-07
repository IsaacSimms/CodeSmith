// == Credits card: paid balance + pack purchase == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createCheckout, getLedger } from "../../../lib/apiClient";
import { interpretError } from "../../../lib/clientError";
import { FailureNotice } from "../../shared/FailureNotice";
import {
  isCheckoutPending,
  topUpFingerprints,
  writeCheckoutBaseline,
} from "../checkoutBaseline";
import { formatBalanceUsd } from "../formatters";
import { useBalance } from "../hooks/useBalance";
import { usePacks } from "../hooks/usePacks";
import { accountQueryKeys } from "../queryKeys";
import type { LedgerEntryResponse, PackResponse } from "../types";
import { AccountSection } from "./AccountSection";

const REBUY_CONFIRM = "A purchase is still applying — continue?";

/// Feeds AccountSection the balance query only. Packs failures degrade the pack area inline.
export function CreditsCard() {
  const balance = useBalance();
  const packs = usePacks();
  const queryClient = useQueryClient();

  const checkout = useMutation({
    mutationFn: async (priceId: string) => {
      // Snapshot TopUp fingerprints *before* Stripe redirect so a webhook that
      // wins the race is still detected on return (ticket 006 #2).
      const ledger = await resolveLedgerSnapshot(queryClient);
      writeCheckoutBaseline(topUpFingerprints(ledger));
      return createCheckout(priceId);
    },
    onSuccess: (result) => {
      window.location.assign(result.url);
    },
  });

  const balanceError = balance.error ? interpretError(balance.error) : null;

  // Soft guard: re-buy mid-poll needs confirm; on confirm mutation overwrites baseline.
  const requestBuy = (priceId: string) => {
    if (isCheckoutPending() && !window.confirm(REBUY_CONFIRM)) return;
    checkout.mutate(priceId);
  };

  return (
    <AccountSection
      title="Credits"
      anchorId="credits"
      isLoading={balance.isLoading}
      error={balanceError}
    >
      <div className="flex flex-col gap-4">
        <p
          data-testid="credits-balance"
          className="text-2xl font-semibold tabular-nums text-white"
        >
          {formatBalanceUsd(balance.data?.paidCreditsUsd ?? 0)}
        </p>

        <PackList packs={packs} checkout={checkout} onBuy={requestBuy} />
      </div>
    </AccountSection>
  );
}

// Prefer warm ledger cache (history section); fetch if the user never opened it.
async function resolveLedgerSnapshot(
  queryClient: ReturnType<typeof useQueryClient>
): Promise<LedgerEntryResponse[]> {
  const cached = queryClient.getQueriesData<LedgerEntryResponse[]>({
    queryKey: accountQueryKeys.ledger,
  });
  for (const [, data] of cached) {
    if (Array.isArray(data)) return data;
  }
  return getLedger();
}

// == Pack catalog body: independent loading / error / empty from balance == //
function PackList({
  packs,
  checkout,
  onBuy,
}: {
  packs: ReturnType<typeof usePacks>;
  checkout: ReturnType<typeof useMutation<{ url: string }, Error, string>>;
  onBuy: (priceId: string) => void;
}) {
  if (packs.isLoading) {
    return <p className="text-sm text-gray-400">Loading packs…</p>;
  }

  if (packs.isError) {
    return (
      <div className="flex flex-col gap-2" data-testid="credits-packs-error">
        <FailureNotice failure={interpretError(packs.error)} />
        <button
          type="button"
          onClick={() => void packs.refetch()}
          className="self-start text-sm font-medium text-accent underline-offset-2 hover:underline"
        >
          Retry
        </button>
      </div>
    );
  }

  const items = packs.data ?? [];
  if (items.length === 0) {
    return <p className="text-sm text-gray-400">No packs available</p>;
  }

  return (
    <div className="flex flex-col gap-2" data-testid="credits-pack-list">
      {items.map((pack) => (
        <PackButton
          key={pack.priceId}
          pack={pack}
          disabled={checkout.isPending}
          onBuy={() => onBuy(pack.priceId)}
        />
      ))}
      {checkout.isError && (
        <FailureNotice failure={interpretError(checkout.error)} />
      )}
    </div>
  );
}

function PackButton({
  pack,
  disabled,
  onBuy,
}: {
  pack: PackResponse;
  disabled: boolean;
  onBuy: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onBuy}
      className="flex items-center justify-between rounded-md border border-gray-600 bg-gray-800 px-4 py-2 text-sm font-medium text-gray-100 transition-colors hover:border-gray-400 hover:bg-gray-700 disabled:cursor-not-allowed disabled:opacity-60"
    >
      <span>{pack.name}</span>
      <span className="tabular-nums text-gray-300">{formatBalanceUsd(pack.amount)}</span>
    </button>
  );
}
