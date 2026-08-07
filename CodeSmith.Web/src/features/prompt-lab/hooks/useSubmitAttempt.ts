// == Submit Attempt Hook == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { submitPromptLabAttempt } from "../../../lib/apiClient";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { AttemptResult } from "../types";

interface SubmitAttemptVariables {
  sessionId: string;
  systemPromptContent: string;
  userMessageContent: string;
}

export function useSubmitAttempt() {
  const queryClient = useQueryClient();
  return useMutation<AttemptResult, Error, SubmitAttemptVariables>({
    mutationFn: ({ sessionId, systemPromptContent, userMessageContent }) =>
      submitPromptLabAttempt(sessionId, { systemPromptContent, userMessageContent }),
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });
}
