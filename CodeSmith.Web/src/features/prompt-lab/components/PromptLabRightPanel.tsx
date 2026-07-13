// == Prompt Lab Right Panel Component == //
// Top: ChallengePanel (challenge info, rubric, test inputs)
// Bottom: guidance chat (messages + input)
// Resizable vertical split mirrors SystemLabRightPanel in features/system-lab.
import { useRef, useEffect } from "react";
import type { ChallengeResponse, AttemptResult, TestInputSummary, PromptLabChatMessage } from "../types";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { MessageBubble } from "../../chat/components/MessageBubble";
import { ChatInput } from "../../chat/components/ChatInput";
import { ChallengePanel } from "./ChallengePanel";

interface PromptLabRightPanelProps {
  challenge: ChallengeResponse;
  testInputs: TestInputSummary[];
  isSubmitting: boolean;
  lastAttempt: AttemptResult | null;
  attemptCount: number;
  onSubmit: () => void;
  chatMessages: PromptLabChatMessage[];
  onSendMessage: (message: string) => void;
  isSendingChat: boolean;
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
}: PromptLabRightPanelProps) {
  const messagesEndRef                             = useRef<HTMLDivElement>(null);
  const { topPercent, dividerProps, containerRef } = useResizableVerticalSplit(55, 25, 80);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatMessages]);

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

          <div className="flex-1 overflow-y-auto p-3">
            {chatMessages.length === 0 && (
              <p className="text-xs text-gray-600">Ask a question to get guidance on your prompt.</p>
            )}
            <div className="flex flex-col gap-3">
              {chatMessages.map((msg, i) => (
                <MessageBubble
                  key={i}
                  role={msg.role === "user" ? "User" : "Assistant"}
                  content={msg.content}
                />
              ))}
              <div ref={messagesEndRef} />
            </div>
          </div>

          <ChatInput onSend={onSendMessage} isLoading={isSendingChat} />
        </div>

      </div>
    </div>
  );
}
