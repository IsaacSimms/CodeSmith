// == Free-quota query hook == //
import { useQuery } from "@tanstack/react-query";
import { getQuota } from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import type { QuotaResponse } from "../types";

export function useQuota() {
  return useQuery<QuotaResponse, Error>({
    queryKey: accountQueryKeys.quota,
    queryFn: getQuota,
  });
}
