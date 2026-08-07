// == Credit-pack catalog query hook (independent of balance / ledger / quota) == //
import { useQuery } from "@tanstack/react-query";
import { getPacks } from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import type { PackResponse } from "../types";

export function usePacks() {
  return useQuery<PackResponse[], Error>({
    queryKey: accountQueryKeys.packs,
    queryFn: getPacks,
  });
}
