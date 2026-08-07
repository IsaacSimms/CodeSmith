// == Submit System Lab Attempt Hook == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { submitSystemLabAttempt } from "../../../lib/apiClient";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { AttemptResult } from "../types";

interface SubmitAttemptVariables {
  sessionId: string;
  justificationContent: string;
}

export function useSubmitAttempt() {
  const queryClient = useQueryClient();
  return useMutation<AttemptResult, Error, SubmitAttemptVariables>({
    mutationFn: ({ sessionId, justificationContent }) =>
      submitSystemLabAttempt(sessionId, { justificationContent }),
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });
}
