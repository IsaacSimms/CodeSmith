// == Prompt Lab Right Panel Component == //
// Top: ChallengePanel (challenge info, rubric, test inputs)
// Bottom: guidance chat (messages + input)
// Resizable vertical split mirrors SystemLabRightPanel in features/system-lab.
import type { ChallengeResponse, AttemptResult, TestInputSummary } from "../types";
import type { ChatMessage } from "../../chat/types";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { ChatInput } from "../../chat/components/ChatInput";
import { ChatTranscript } from "../../chat/components/ChatTranscript";
import type { FailedTurn } from "../../chat/components/StreamingChatTail";
import { ChallengePanel } from "./ChallengePanel";

interface PromptLabRightPanelProps {
  challenge: ChallengeResponse;
  testInputs: TestInputSummary[];
  isSubmitting: boolean;
  lastAttempt: AttemptResult | null;
  attemptCount: number;
  onSubmit: () => void;
  chatMessages: ChatMessage[];
  onSendMessage: (message: string) => void;
  isSendingChat: boolean;
  streamingText: string;           // in-flight assistant reply, accumulating while isSendingChat
  failedTurn: FailedTurn | null;   // last turn died mid-stream — partial kept visible, dimmed
  draft: { text: string } | null;  // failed turn's user message, restored to the input
}

export function PromptLabRightPanel({
  challenge,
  testInputs,
  isSubmitting,
  lastAttempt,
  attemptCount,
  onSubmit,
  chatMessages,
  onSendMessage,
  isSendingChat,
  streamingText,
  failedTurn,
  draft,
}: PromptLabRightPanelProps) {
  const { topPercent, dividerProps, containerRef } = useResizableVerticalSplit(55, 25, 80);

  return (
    <div className="flex h-full flex-col overflow-hidden">
      <div ref={containerRef} className="flex flex-1 flex-col overflow-hidden">

        {/* == Challenge Panel (top) == */}
        <div className="min-h-0 overflow-hidden" style={{ height: `${topPercent}%` }}>
          <ChallengePanel
            challenge={challenge}
            testInputs={testInputs}
            isSubmitting={isSubmitting}
            lastAttempt={lastAttempt}
            attemptCount={attemptCount}
            onSubmit={onSubmit}
          />
        </div>

        {/* == Drag Divider == */}
        <div
          {...dividerProps}
          className="h-1.5 shrink-0 cursor-row-resize bg-gray-700 transition-colors hover:bg-accent active:bg-accent"
        />

        {/* == Guidance Chat (bottom) == */}
        <div className="flex flex-col overflow-hidden" style={{ height: `${100 - topPercent}%` }}>
          <div className="border-b border-gray-700 px-4 py-1.5">
            <h3 className="text-xs font-semibold text-gray-400">Guidance</h3>
          </div>

          <ChatTranscript
            className="flex-1"
            messages={chatMessages}
            isStreaming={isSendingChat}
            streamingText={streamingText}
            failedTurn={failedTurn}
            emptyStateText="Ask a question to get guidance on your prompt."
          />

          <ChatInput onSend={onSendMessage} isLoading={isSendingChat} draft={draft} />
        </div>

      </div>
    </div>
  );
}
