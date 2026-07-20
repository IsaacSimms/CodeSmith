// == Streaming Chat Hook == //
import { useCallback, useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useStreamingText } from "./useStreamingText";
import type { ChatMessage } from "../features/chat/types";
import type { FailedTurn } from "../features/chat/components/StreamingChatTail";

// The reply every surface's chat stream resolves with — the assistant's full text
export interface StreamedReply {
  response: string;
}

// One turn of a surface's chat stream: the user message goes out, deltas arrive through onDelta,
// the surface's final reply resolves the promise. context carries per-send caller intent (e.g.
// the tutoring surface's GuidanceMode); surfaces without one ignore it.
export type StreamTurn<TReply extends StreamedReply, TContext> = (
  message: string,
  onDelta: (delta: string) => void,
  context?: TContext
) => Promise<TReply>;

interface SendOptions<TReply, TContext> {
  context?: TContext;                  // per-send caller intent, forwarded to streamTurn
  onSuccess?: (reply: TReply) => void; // surface-specific reply handling (e.g. context token capture)
}

/// One streaming chat turn's client-side invariant, shared by all three surfaces: append the user
/// message optimistically, accumulate reply deltas into streamingText, append the assistant reply
/// on success — and on failure mirror the server's whole-turn rollback: drop the optimistic user
/// bubble, keep the partial reply as a failed turn, and restore the message as an input draft.
/// The surface supplies its stream call as data; isSending/streamingText/failedTurn/draft drive
/// ChatTranscript and ChatInput directly.
export function useStreamingChat<TReply extends StreamedReply, TContext = void>(
  streamTurn: StreamTurn<TReply, TContext>
) {
  const [messages, setMessages]     = useState<ChatMessage[]>([]);
  const [failedTurn, setFailedTurn] = useState<FailedTurn | null>(null);
  const [draft, setDraft]           = useState<{ text: string } | null>(null);
  const { text: streamingText, append, reset, getText } = useStreamingText();

  // Latest-render mirror so a turn always runs the current closure (fresh editor/session state);
  // updated in an effect because refs must not be written during render
  const streamTurnRef = useRef(streamTurn);
  useEffect(() => {
    streamTurnRef.current = streamTurn;
  });

  const mutation = useMutation<TReply, Error, { message: string; context?: TContext }>({
    mutationFn: ({ message, context }) => {
      reset();
      return streamTurnRef.current(message, append, context);
    },
  });

  function sendMessage(message: string, options?: SendOptions<TReply, TContext>) {
    // A new turn supersedes a failed one — its remains clear and the restored draft is consumed
    setFailedTurn(null);
    setDraft(null);
    setMessages((prev) => [...prev, { role: "User", content: message, timestamp: new Date().toISOString() }]);

    mutation.mutate(
      { message, context: options?.context },
      {
        onSuccess: (reply) => {
          setMessages((prev) => [...prev, { role: "Assistant", content: reply.response, timestamp: new Date().toISOString() }]);
          options?.onSuccess?.(reply);
        },
        onError: (error) => {
          // The server rolled the turn back — mirror it: drop the optimistic user bubble, keep
          // the partial reply visible as a failed turn, and put the message back in the input.
          setMessages((prev) => prev.slice(0, -1));
          setFailedTurn({ partial: getText(), message: error.message });
          setDraft({ text: message });
        },
      }
    );
  }

  // Seeds (or clears) the transcript and wipes failed-turn remains — session start / nav reset.
  // Stable identity so callers can safely list it in effect dependencies.
  const resetChat = useCallback((seed: ChatMessage[] = []) => {
    setMessages(seed);
    setFailedTurn(null);
    setDraft(null);
  }, []);

  return {
    messages,
    sendMessage,
    resetChat,
    isSending: mutation.isPending,
    streamingText,
    failedTurn,
    draft,
  };
}
