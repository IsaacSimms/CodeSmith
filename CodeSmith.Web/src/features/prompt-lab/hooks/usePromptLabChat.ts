// == Prompt Lab Guidance Chat Hook == //
import { useMutation } from "@tanstack/react-query";
import { streamPromptLabChat } from "../../../lib/apiClient";
import { useStreamingText } from "../../../hooks/useStreamingText";
import type { PromptLabChatResponse } from "../types";

interface ChatVariables {
  sessionId: string;
  message: string;
  editorContent?: string;
}

/// Streaming chat mutation — same shape as the tutoring surface's useSendMessage: deltas
/// accumulate in streamingText, the final reply resolves the mutation as before.
export function usePromptLabChat() {
  const { text: streamingText, append, reset, getText } = useStreamingText();

  const mutation = useMutation<PromptLabChatResponse, Error, ChatVariables>({
    mutationFn: ({ sessionId, message, editorContent }) => {
      reset();
      return streamPromptLabChat(sessionId, { message, editorContent }, { onDelta: append });
    },
  });

  return { ...mutation, streamingText, getStreamedText: getText };
}
