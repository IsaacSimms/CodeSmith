// == Start System Lab Session Hook == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { startSystemLabSession } from "../../../lib/apiClient";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { StartSessionRequest, SystemLabSession } from "../types";

export function useStartSession() {
  const queryClient = useQueryClient();
  return useMutation<SystemLabSession, Error, StartSessionRequest>({
    mutationFn: startSystemLabSession,
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });
}
