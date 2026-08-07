// == Start Challenge Hook == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { startPromptLabChallenge } from "../../../lib/apiClient";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { StartChallengeRequest, PromptLabSession } from "../types";

export function useStartChallenge() {
  const queryClient = useQueryClient();
  return useMutation<PromptLabSession, Error, StartChallengeRequest>({
    mutationFn: startPromptLabChallenge,
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });
}
