// == System Lab Right Panel Component == //
// Top: collapsible scenario info (description, constraints, rubric, tradeoffs) + submit footer
// Bottom: guidance chat (messages + input)
// Resizable vertical split mirrors ChatPanel in features/chat.
import { useRef, useEffect, useState } from "react";
import type { ClientFailure } from "../../../lib/clientError";
import { FailureNotice } from "../../shared/FailureNotice";
import type { ScenarioResponse, SystemLabChatMessage } from "../types";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { MessageBubble } from "../../chat/components/MessageBubble";
import { ChatInput } from "../../chat/components/ChatInput";
import { StreamingChatTail, type FailedTurn } from "../../chat/components/StreamingChatTail";

interface SystemLabRightPanelProps {
  scenario: ScenarioResponse;
  isSubmitting: boolean;
  canSubmit: boolean;               // false while the justification is empty
  attemptCount: number;
  onSubmit: () => void;
  submitError: ClientFailure | null;
  chatMessages: SystemLabChatMessage[];
  onSendMessage: (message: string) => void;
  isSending: boolean;
  streamingText: string;           // in-flight assistant reply, accumulating while isSending
  failedTurn: FailedTurn | null;   // last turn died mid-stream — partial kept visible, dimmed
  draft: { text: string } | null;  // failed turn's user message, restored to the input
}

export function SystemLabRightPanel({
  scenario,
  isSubmitting,
  canSubmit,
  attemptCount,
  onSubmit,
  submitError,
  chatMessages,
  onSendMessage,
  isSending,
  streamingText,
  failedTurn,
  draft,
}: SystemLabRightPanelProps) {
  const messagesEndRef                                          = useRef<HTMLDivElement>(null);
  const [infoCollapsed, setInfoCollapsed]                      = useState(false);
  const { topPercent, dividerProps, containerRef }             = useResizableVerticalSplit(40, 15, 75);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [chatMessages, streamingText, failedTurn]);

  return (
    <div className="flex h-full flex-col overflow-hidden">
      <div ref={containerRef} className="flex flex-1 flex-col overflow-hidden">

        {/* == Scenario Info (top, collapsible) == */}
        <div
          className="flex flex-col overflow-hidden border-b border-gray-700"
          style={{ height: `${topPercent}%` }}
        >
          {/* Section header with collapse toggle */}
          <button
            onClick={() => setInfoCollapsed((c) => !c)}
            className="flex w-full items-center justify-between px-4 py-2 text-left hover:bg-gray-800/40"
          >
            <div>
              <span className="text-sm font-bold text-gray-100">{scenario.title}</span>
              <span className="ml-2 text-xs text-gray-500">
                {scenario.category.replace(/([A-Z])/g, " $1").trim()} · {scenario.difficulty}
              </span>
            </div>
            <span className="text-xs text-gray-600">{infoCollapsed ? "▼" : "▲"}</span>
          </button>

          {!infoCollapsed && (
            <div className="flex-1 space-y-4 overflow-y-auto px-4 pb-3">
              {/* Description */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Scenario</h3>
                <p className="whitespace-pre-wrap text-xs leading-relaxed text-gray-100">{scenario.description.trim()}</p>
              </section>

              {/* Constraints */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Constraints</h3>
                <p className="whitespace-pre-wrap text-xs leading-relaxed text-gray-100">{scenario.constraints.trim()}</p>
              </section>

              {/* Required tradeoffs */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Required Tradeoffs</h3>
                <ol className="list-decimal space-y-1 pl-4">
                  {scenario.requiredTradeoffs.map((t, i) => (
                    <li key={i} className="text-xs leading-relaxed text-gray-100">{t}</li>
                  ))}
                </ol>
              </section>

              {/* Rubric */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Scoring Rubric</h3>
                <div className="space-y-1.5">
                  {scenario.rubric.map((criterion) => (
                    <div key={criterion.criterionId} className="rounded border border-gray-700 px-3 py-2">
                      <div className="flex items-center justify-between">
                        <span className="text-xs font-medium text-gray-200">{criterion.name}</span>
                        <span className="text-xs text-gray-500">{criterion.maxPoints} pts</span>
                      </div>
                      <p className="mt-0.5 text-xs text-gray-300">{criterion.description}</p>
                    </div>
                  ))}
                </div>
              </section>
            </div>
          )}

          {/* == Submit Button == */}
          {/* mt-auto keeps the footer pinned to the bottom when the info body is collapsed away */}
          <div className="mt-auto border-t border-gray-700 px-4 py-4">
            <button
              onClick={onSubmit}
              disabled={isSubmitting || !canSubmit}
              className="w-full rounded bg-accent px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isSubmitting ? "Evaluating…" : "Submit Justification"}
            </button>
            <p className="mt-2 text-center text-xs text-gray-600">
              {isSubmitting ? (
                <span>Evaluating {"·".repeat(3)}</span>
              ) : (
                <span><kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Enter</kbd> to submit · <kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Shift+Enter</kbd> for new line</span>
              )}
            </p>
            {attemptCount > 0 && !isSubmitting && (
              <p className="mt-1 text-center text-xs text-gray-600">Attempt {attemptCount + 1}</p>
            )}
            {submitError && !isSubmitting && (
              <div className="mt-3">
                <FailureNotice failure={submitError} />
              </div>
            )}
          </div>
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
              <p className="text-xs text-gray-600">Ask a question to get Socratic guidance on your justification.</p>
            )}
            <div className="flex flex-col gap-3">
              {chatMessages.map((msg, i) => (
                <MessageBubble
                  key={i}
                  role={msg.role === "user" ? "User" : "Assistant"}
                  content={msg.content}
                />
              ))}
              <StreamingChatTail isStreaming={isSending} streamingText={streamingText} failedTurn={failedTurn} />
              <div ref={messagesEndRef} />
            </div>
          </div>

          <ChatInput onSend={onSendMessage} isLoading={isSending} draft={draft} />
        </div>

      </div>
    </div>
  );
}
