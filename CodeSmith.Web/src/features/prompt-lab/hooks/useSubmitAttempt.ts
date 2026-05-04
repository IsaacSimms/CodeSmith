// == Submit Attempt Hook == //
import { useMutation } from "@tanstack/react-query";
import { submitPromptLabAttempt } from "../../../lib/apiClient";
import type { AttemptResult } from "../types";

interface SubmitAttemptVariables {
  sessionId: string;
  systemPromptContent: string;
  userMessageContent: string;
}

export function useSubmitAttempt() {
  return useMutation<AttemptResult, Error, SubmitAttemptVariables>({
    mutationFn: ({ sessionId, systemPromptContent, userMessageContent }) =>
      submitPromptLabAttempt(sessionId, { systemPromptContent, userMessageContent }),
  });
}
