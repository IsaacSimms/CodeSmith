// == Submit System Lab Attempt Hook == //
import { useMutation } from "@tanstack/react-query";
import { submitSystemLabAttempt } from "../../../lib/apiClient";
import type { AttemptResult } from "../types";

interface SubmitAttemptVariables {
  sessionId: string;
  justificationContent: string;
}

export function useSubmitAttempt() {
  return useMutation<AttemptResult, Error, SubmitAttemptVariables>({
    mutationFn: ({ sessionId, justificationContent }) =>
      submitSystemLabAttempt(sessionId, { justificationContent }),
  });
}
