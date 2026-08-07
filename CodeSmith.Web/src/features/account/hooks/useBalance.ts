// == Paid balance query hook == //
import { useQuery } from "@tanstack/react-query";
import { getBalance } from "../../../lib/apiClient";
import { accountQueryKeys } from "../queryKeys";
import type { BalanceResponse } from "../types";

export function useBalance() {
  return useQuery<BalanceResponse, Error>({
    queryKey: accountQueryKeys.balance,
    queryFn: getBalance,
  });
}
