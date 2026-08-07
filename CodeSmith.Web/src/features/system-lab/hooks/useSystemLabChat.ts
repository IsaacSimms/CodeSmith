// == System Lab Guidance Chat Hook == //
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { streamSystemLabChat } from "../../../lib/apiClient";
import { useStreamingText } from "../../../hooks/useStreamingText";
import { invalidateAccountUsageQueries } from "../../account/hooks/invalidateAccountUsageQueries";
import type { SystemLabChatResponse } from "../types";

interface ChatVariables {
  sessionId: string;
  message: string;
  currentJustification?: string;
}

/// Streaming chat mutation — same shape as the tutoring surface's useSendMessage: deltas
/// accumulate in streamingText, the final reply resolves the mutation as before.
export function useSystemLabChat() {
  const queryClient = useQueryClient();
  const { text: streamingText, append, reset, getText } = useStreamingText();

  const mutation = useMutation<SystemLabChatResponse, Error, ChatVariables>({
    mutationFn: ({ sessionId, message, currentJustification }) => {
      reset();
      return streamSystemLabChat(sessionId, { message, currentJustification }, { onDelta: append });
    },
    onSuccess: () => invalidateAccountUsageQueries(queryClient),
  });

  return { ...mutation, streamingText, getStreamedText: getText };
}
