// == Prefetch quota + balance when authenticated (not fetch-on-open) == //
// Mid-chat overstatement from lock-free quota only self-corrects if the query stays warm.
import { useEffect } from "react";
import { useIsAuthenticated } from "@azure/msal-react";
import { useQueryClient } from "@tanstack/react-query";
import { getBalance, getQuota } from "../../../lib/apiClient";
import { isMsalConfigured } from "../../../auth/msalConfig";
import { accountQueryKeys } from "../queryKeys";

/// Null-render mount for Layout. Mirrors AuthControls: no MSAL → no-op; with MSAL, only
/// authenticated sessions prefetch so unauthenticated visitors never hit the endpoints.
export function AccountDataPrefetch() {
  if (!isMsalConfigured()) return null;
  return <AccountDataPrefetchWhenMsal />;
}

function AccountDataPrefetchWhenMsal() {
  const isAuthenticated = useIsAuthenticated();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!isAuthenticated) return;

    void queryClient.prefetchQuery({
      queryKey: accountQueryKeys.quota,
      queryFn: getQuota,
    });
    void queryClient.prefetchQuery({
      queryKey: accountQueryKeys.balance,
      queryFn: getBalance,
    });
  }, [isAuthenticated, queryClient]);

  return null;
}
