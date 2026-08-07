// == Ledger history query hook == //
import { useQuery } from "@tanstack/react-query";
import { getLedger } from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import type { LedgerEntryResponse } from "../types";

export function useLedger(take = 20) {
  return useQuery<LedgerEntryResponse[], Error>({
    queryKey: [...accountQueryKeys.ledger, take] as const,
    queryFn: () => getLedger(take),
  });
}
