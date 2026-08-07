// == Create Session Hook == //
import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { streamCreateSession } from "../../../lib/apiClient";
import { useStreamingText } from "../../../hooks/useStreamingText";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { CreateSessionRequest, ProblemSession } from "../types";

/// Streaming session creation: the problem description accumulates in streamingDescription as the
/// AI writes it; a server retry (reset event) clears it and flips isRetrying until fresh text
/// arrives. The full session (with starter code) still arrives via the resolved mutation.
export function useCreateSession() {
  const queryClient = useQueryClient();
  const { text: streamingDescription, append, reset } = useStreamingText();
  const [isRetrying, setIsRetrying] = useState(false);

  const mutation = useMutation<ProblemSession, Error, CreateSessionRequest>({
    mutationFn: (body) => {
      reset();
      setIsRetrying(false);
      return streamCreateSession(body, {
        onDelta: (text) => {
          setIsRetrying(false);
          append(text);
        },
        onReset: () => {
          reset();
          setIsRetrying(true);
        },
      });
    },
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });

  return { ...mutation, streamingDescription, isRetrying };
}
