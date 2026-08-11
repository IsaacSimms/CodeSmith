// == Guidance Chat State Machine Hook == //
import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useStreamingText } from "./useStreamingText";
import { interpretError, type ClientFailure } from "../lib/clientError";
import { invalidateAccountUsageQueries } from "../features/account/hooks/invalidateAccountUsageQueries";

// A turn that failed after the optimistic append was rolled back: the interpreted failure, plus the
// partial reply if any deltas arrived before the death (drives the "incomplete reply" framing).
export interface FailedTurn {
  failure: ClientFailure;
  partial?: string;
}

// Runs the surface's streaming API call for one turn; deltas must be forwarded to onDelta.
export type GuidanceTurnRunner<TResponse> = (
  onDelta: (delta: string) => void,
) => Promise<TResponse>;

export interface GuidanceChatConfig<TMessage, TResponse> {
  toUserMessage: (message: string) => TMessage;         // Surface's optimistic user bubble shape
  toAssistantMessage: (response: TResponse) => TMessage; // Surface's assistant bubble from the reply
  onTurnSuccess?: (response: TResponse) => void;         // Extra per-surface effects (e.g. context tokens)
}

/**
 * The one client mirror of the server's whole-turn Guidance invariant, shared by all three surfaces.
 * send() appends the user message optimistically and runs the surface's streaming call; on success it
 * appends the assistant reply and invalidates the account usage queries (quota / balance / ledger move
 * on every settled metered turn); on failure it rolls the optimistic bubble back — mirroring the
 * server-side whole-turn rollback — snapshots any partial reply into failedTurn, and restores the
 * user's message as a draft for the input. Surfaces supply only their message shapes and API call.
 */
export function useGuidanceChat<TMessage, TResponse>(config: GuidanceChatConfig<TMessage, TResponse>) {
  const queryClient = useQueryClient();
  const [messages, setMessages] = useState<TMessage[]>([]);
  const [failedTurn, setFailedTurn] = useState<FailedTurn | null>(null);
  const [draft, setDraft] = useState<{ text: string } | null>(null);
  const { text: streamingText, append, reset, getText } = useStreamingText();

  const mutation = useMutation<TResponse, Error, { message: string; run: GuidanceTurnRunner<TResponse> }>({
    mutationFn: ({ run }) => {
      reset();
      return run(append);
    },
    onSuccess: (response) => {
      setMessages((prev) => [...prev, config.toAssistantMessage(response)]);
      config.onTurnSuccess?.(response);
      invalidateAccountUsageQueries(queryClient);
    },
    onError: (error, { message }) => {
      // The server rolled the turn back — mirror it: drop the optimistic user bubble, keep any
      // partial reply for the failure UI, and put the message back in the input.
      setMessages((prev) => prev.slice(0, -1));
      const partial = getText();
      setFailedTurn({
        failure: interpretError(error),
        partial: partial.trim() ? partial : undefined,
      });
      setDraft({ text: message });
    },
  });

  // == send: one whole turn == //
  function send(message: string, run: GuidanceTurnRunner<TResponse>): Promise<TResponse> {
    setFailedTurn(null);
    setDraft(null);
    setMessages((prev) => [...prev, config.toUserMessage(message)]);
    return mutation.mutateAsync({ message, run });
  }

  return {
    messages,
    setMessages, // session start seeds history; nav reset clears it
    send,
    isPending: mutation.isPending,
    streamingText,
    failedTurn,
    draft,
  };
}
