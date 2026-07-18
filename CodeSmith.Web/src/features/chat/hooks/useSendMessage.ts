// == Send Message Hook == //
import { useMutation } from "@tanstack/react-query";
import { streamChat } from "../../../lib/apiClient";
import { useStreamingText } from "../../../hooks/useStreamingText";
import type { ChatResponse, GuidanceMode } from "../types";

interface SendMessageVariables {
  sessionId: string;
  message: string;
  editorContent?: string;
  guidanceMode?: GuidanceMode;
}

/// Streaming chat mutation: the reply accumulates in streamingText while isPending, and the
/// mutation still resolves with the final ChatResponse — isPending / data / error semantics are
/// unchanged from the blocking version. getStreamedText() snapshots the partial for failure UI.
export function useSendMessage() {
  const { text: streamingText, append, reset, getText } = useStreamingText();

  const mutation = useMutation<ChatResponse, Error, SendMessageVariables>({
    mutationFn: ({ sessionId, message, editorContent, guidanceMode }) => {
      reset();
      return streamChat(sessionId, { message, editorContent, guidanceMode }, { onDelta: append });
    },
  });

  return { ...mutation, streamingText, getStreamedText: getText };
}
