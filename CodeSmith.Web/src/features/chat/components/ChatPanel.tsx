// == Chat Panel Component == //
import type { ChatMessage } from "../types";
import { ChatInput } from "./ChatInput";
import { ChatTranscript } from "./ChatTranscript";
import type { FailedTurn } from "./StreamingChatTail";
import { TokenUsageBar } from "../../../components/TokenUsageBar";
import { useResizableVerticalSplit } from "../hooks/useResizableVerticalSplit";

interface ChatPanelProps {
  problemDescription: string;
  messages: ChatMessage[];
  onSendMessage: (message: string) => void;
  isSending: boolean;
  streamingText: string;             // in-flight assistant reply, accumulating while isSending
  failedTurn: FailedTurn | null;     // last turn died mid-stream — partial kept visible, dimmed
  draft: { text: string } | null;    // failed turn's user message, restored to the input
  contextTokensUsed: number | null;  // null before first message is sent
  contextWindowSize: number;
}

export function ChatPanel({ problemDescription, messages, onSendMessage, isSending, streamingText, failedTurn, draft, contextTokensUsed, contextWindowSize }: ChatPanelProps) {
  const { topPercent, dividerProps, containerRef } = useResizableVerticalSplit(30, 15, 70);

  return (
    <div className="flex h-full flex-col">
      {/* == Resizable Problem / Messages Split == */}
      <div ref={containerRef} className="flex flex-1 flex-col overflow-hidden">

        {/* == Problem Description == */}
        <div
          className="overflow-y-auto border-b border-gray-900 bg-gray-900 px-4 py-3"
          style={{ height: `${topPercent}%` }}
        >
          <h2 className="mb-1 text-sm font-semibold text-gray-400">Problem</h2>
          <p className="whitespace-pre-wrap text-sm text-gray-300">{problemDescription}</p>
        </div>

        {/* == Drag Divider == */}
        <div
          {...dividerProps}
          className="h-1.5 cursor-row-resize bg-gray-700 transition-colors hover:bg-gray-600"
        />

        {/* == Messages == */}
        <div className="min-h-0" style={{ height: `${100 - topPercent}%` }}>
          <ChatTranscript
            className="h-full"
            messages={messages}
            isStreaming={isSending}
            streamingText={streamingText}
            failedTurn={failedTurn}
          />
        </div>

      </div>

      {/* == Context Usage Bar (appears after first reply, pinned above input) == */}
      {contextTokensUsed !== null && (
        <TokenUsageBar
          tokensUsed={contextTokensUsed}
          contextWindowSize={contextWindowSize}
          label="Context"
          description="Conversation history sent to the AI this turn. Grows with each message."
        />
      )}

      {/* == Input == */}
      <ChatInput onSend={onSendMessage} isLoading={isSending} draft={draft} />
    </div>
  );
}
