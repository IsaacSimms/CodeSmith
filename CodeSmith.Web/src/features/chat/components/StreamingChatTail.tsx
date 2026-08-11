// == Streaming Chat Tail Component == //
import { FailureNotice } from "../../shared/FailureNotice";
import type { FailedTurn } from "../../../hooks/useGuidanceChat";
import { MessageBubble } from "./MessageBubble";

// The FailedTurn shape is owned by the guidance chat state machine; re-exported here so
// existing component-level imports keep working.
export type { FailedTurn };

interface StreamingChatTailProps {
  isStreaming: boolean;   // a turn is in flight
  streamingText: string;  // reply text streamed so far this turn
  failedTurn: FailedTurn | null;
}

/// Renders the live tail of a streaming chat: the in-flight assistant reply as it accumulates,
/// and the remains of a turn that failed. Shared by all three surfaces' chats.
export function StreamingChatTail({ isStreaming, streamingText, failedTurn }: StreamingChatTailProps) {
  const partial = failedTurn?.partial?.trim() ? failedTurn.partial : undefined;

  return (
    <>
      {failedTurn && !isStreaming && (
        <div data-testid="failed-turn">
          {partial && (
            <div className="opacity-60">
              <MessageBubble role="Assistant" content={partial} />
            </div>
          )}
          <FailureNotice failure={failedTurn.failure} className="mt-1" />
          {partial && (
            <p className="mt-1 text-xs text-red-400/80">
              This reply is incomplete and was not saved. Your message is back in the box below.
            </p>
          )}
        </div>
      )}
      {isStreaming &&
        (streamingText ? (
          <MessageBubble role="Assistant" content={streamingText} />
        ) : (
          <p className="animate-pulse text-xs text-gray-500">Thinking…</p>
        ))}
    </>
  );
}
