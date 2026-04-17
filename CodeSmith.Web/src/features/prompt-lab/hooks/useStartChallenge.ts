// == Start Challenge Hook == //
import { useMutation } from "@tanstack/react-query";
import { startPromptLabChallenge } from "../../../lib/apiClient";
import type { StartChallengeRequest, PromptLabSession } from "../types";

export function useStartChallenge() {
  return useMutation<PromptLabSession, Error, StartChallengeRequest>({
    mutationFn: startPromptLabChallenge,
  });
}
