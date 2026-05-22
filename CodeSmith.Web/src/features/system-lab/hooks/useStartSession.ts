// == Start System Lab Session Hook == //
import { useMutation } from "@tanstack/react-query";
import { startSystemLabSession } from "../../../lib/apiClient";
import type { StartSessionRequest, SystemLabSession } from "../types";

export function useStartSession() {
  return useMutation<SystemLabSession, Error, StartSessionRequest>({
    mutationFn: startSystemLabSession,
  });
}
