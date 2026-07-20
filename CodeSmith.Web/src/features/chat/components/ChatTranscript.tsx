// == Chat Transcript Component == //
import { useEffect, useRef } from "react";
import type { ChatMessage } from "../types";
import { MessageBubble } from "./MessageBubble";
import { StreamingChatTail, type FailedTurn } from "./StreamingChatTail";

interface ChatTranscriptProps {
  messages: ChatMessage[];
  isStreaming: boolean;           // a turn is in flight
  streamingText: string;          // reply text streamed so far this turn
  failedTurn: FailedTurn | null;  // last turn died mid-stream — partial kept visible, dimmed
  emptyStateText?: string;        // hint shown while the transcript has no messages
  className?: string;             // sizing within the parent layout (e.g. "flex-1" or "h-full")
}

/// The scrollable transcript of a streaming chat, shared by all three surfaces: one bubble per
/// message, the live tail (in-flight reply / dimmed failed turn), and the keep-scrolled-to-bottom
/// behavior tracking new messages, streamed deltas, and failed-turn remains.
export function ChatTranscript({ messages, isStreaming, streamingText, failedTurn, emptyStateText, className }: ChatTranscriptProps) {
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, streamingText, failedTurn]);

  return (
    <div className={`overflow-y-auto p-3 ${className ?? ""}`}>
      {messages.length === 0 && emptyStateText && (
        <p className="text-xs text-gray-600">{emptyStateText}</p>
      )}
      <div className="flex flex-col gap-3">
        {messages.map((msg, i) => (
          <MessageBubble key={i} role={msg.role} content={msg.content} />
        ))}
        <StreamingChatTail isStreaming={isStreaming} streamingText={streamingText} failedTurn={failedTurn} />
        <div ref={endRef} />
      </div>
    </div>
  );
}
