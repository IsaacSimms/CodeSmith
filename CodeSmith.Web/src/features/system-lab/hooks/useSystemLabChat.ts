// == System Lab Guidance Chat Hook == //
import { useMutation } from "@tanstack/react-query";
import { streamSystemLabChat } from "../../../lib/apiClient";
import { useStreamingText } from "../../../hooks/useStreamingText";
import type { SystemLabChatResponse } from "../types";

interface ChatVariables {
  sessionId: string;
  message: string;
  currentJustification?: string;
}

/// Streaming chat mutation — same shape as the tutoring surface's useSendMessage: deltas
/// accumulate in streamingText, the final reply resolves the mutation as before.
export function useSystemLabChat() {
  const { text: streamingText, append, reset, getText } = useStreamingText();

  const mutation = useMutation<SystemLabChatResponse, Error, ChatVariables>({
    mutationFn: ({ sessionId, message, currentJustification }) => {
      reset();
      return streamSystemLabChat(sessionId, { message, currentJustification }, { onDelta: append });
    },
  });

  return { ...mutation, streamingText, getStreamedText: getText };
}
