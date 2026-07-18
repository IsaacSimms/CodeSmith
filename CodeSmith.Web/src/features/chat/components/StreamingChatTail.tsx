// == Streaming Chat Tail Component == //
import { MessageBubble } from "./MessageBubble";

// A turn that died mid-stream: the partial reply stays visible (dimmed) with the error under it,
// while the user's message goes back to the input for one-tap resend. Cleared on the next send.
export interface FailedTurn {
  partial: string;
  message: string;
}

interface StreamingChatTailProps {
  isStreaming: boolean;   // a turn is in flight
  streamingText: string;  // reply text streamed so far this turn
  failedTurn: FailedTurn | null;
}

/// Renders the live tail of a streaming chat: the in-flight assistant reply as it accumulates,
/// and the dimmed remains of a turn that failed mid-stream. Shared by all three surfaces' chats.
export function StreamingChatTail({ isStreaming, streamingText, failedTurn }: StreamingChatTailProps) {
  return (
    <>
      {failedTurn && !isStreaming && (
        <div data-testid="failed-turn">
          {failedTurn.partial && (
            <div className="opacity-60">
              <MessageBubble role="Assistant" content={failedTurn.partial} />
            </div>
          )}
          <p className="mt-1 text-xs text-red-400">
            {failedTurn.message} — this reply is incomplete and was not saved. Your message is back in the box below.
          </p>
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
