// == Create Session Hook == //
import { useMutation } from "@tanstack/react-query";
import { createSession } from "../../../lib/apiClient";
import type { CreateSessionRequest, ProblemSession } from "../types";

export function useCreateSession() {
  return useMutation<ProblemSession, Error, CreateSessionRequest>({
    mutationFn: createSession,
  });
}
