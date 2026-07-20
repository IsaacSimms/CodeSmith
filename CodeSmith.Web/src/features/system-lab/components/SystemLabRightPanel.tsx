// == System Lab Right Panel Component == //
// Top: collapsible scenario info (description, constraints, rubric, tradeoffs)
// Bottom: guidance chat (messages + input)
// Resizable vertical split mirrors ChatPanel in features/chat.
import { useState } from "react";
import type { ScenarioResponse } from "../types";
import type { ChatMessage } from "../../chat/types";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { ChatInput } from "../../chat/components/ChatInput";
import { ChatTranscript } from "../../chat/components/ChatTranscript";
import type { FailedTurn } from "../../chat/components/StreamingChatTail";

interface SystemLabRightPanelProps {
  scenario: ScenarioResponse;
  chatMessages: ChatMessage[];
  onSendMessage: (message: string) => void;
  isSending: boolean;
  streamingText: string;           // in-flight assistant reply, accumulating while isSending
  failedTurn: FailedTurn | null;   // last turn died mid-stream — partial kept visible, dimmed
  draft: { text: string } | null;  // failed turn's user message, restored to the input
}

export function SystemLabRightPanel({
  scenario,
  chatMessages,
  onSendMessage,
  isSending,
  streamingText,
  failedTurn,
  draft,
}: SystemLabRightPanelProps) {
  const [infoCollapsed, setInfoCollapsed]          = useState(false);
  const { topPercent, dividerProps, containerRef } = useResizableVerticalSplit(40, 15, 75);

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
                <p className="whitespace-pre-wrap text-xs leading-relaxed text-gray-300">{scenario.description.trim()}</p>
              </section>

              {/* Constraints */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Constraints</h3>
                <p className="whitespace-pre-wrap text-xs leading-relaxed text-gray-300">{scenario.constraints.trim()}</p>
              </section>

              {/* Required tradeoffs */}
              <section>
                <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-500">Required Tradeoffs</h3>
                <ol className="list-decimal space-y-1 pl-4">
                  {scenario.requiredTradeoffs.map((t, i) => (
                    <li key={i} className="text-xs leading-relaxed text-gray-300">{t}</li>
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
                        <span className="text-xs font-medium text-gray-300">{criterion.name}</span>
                        <span className="text-xs text-gray-500">{criterion.maxPoints} pts</span>
                      </div>
                      <p className="mt-0.5 text-xs text-gray-500">{criterion.description}</p>
                    </div>
                  ))}
                </div>
              </section>
            </div>
          )}
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
            isStreaming={isSending}
            streamingText={streamingText}
            failedTurn={failedTurn}
            emptyStateText="Ask a question to get Socratic guidance on your justification."
          />

          <ChatInput onSend={onSendMessage} isLoading={isSending} draft={draft} />
        </div>

      </div>
    </div>
  );
}
