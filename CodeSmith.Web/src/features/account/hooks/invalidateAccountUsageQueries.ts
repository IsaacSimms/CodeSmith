// == Shared turn-settle / top-up invalidation == //
// One client rule: always invalidate quota + balance + ledger. No free/paid branching.
// Packs are intentionally omitted — catalog does not move on a metered turn.
import type { QueryClient } from "@tanstack/react-query";
import { accountQueryKeys } from "../queryKeys";

export function invalidateAccountUsageQueries(queryClient: QueryClient): void {
  void queryClient.invalidateQueries({ queryKey: accountQueryKeys.quota });
  void queryClient.invalidateQueries({ queryKey: accountQueryKeys.balance });
  void queryClient.invalidateQueries({ queryKey: accountQueryKeys.ledger });
}
