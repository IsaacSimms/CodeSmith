// == System Lab Window Component == //
import { useEffect, useState } from "react";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreferenceContext } from "../../../contexts/ProviderPreferenceContext";
import { interpretError } from "../../../lib/clientError";
import { FailureNotice } from "../../shared/FailureNotice";
import type { ScenarioResponse, AttemptResult, SystemLabSession, SystemLabChatMessage } from "../types";
import { useGetScenarios } from "../hooks/useGetScenarios";
import { useStartSession } from "../hooks/useStartSession";
import { useSubmitAttempt } from "../hooks/useSubmitAttempt";
import { useSystemLabChat } from "../hooks/useSystemLabChat";
import { useResizableSplit } from "../../chat/hooks/useResizableSplit";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { ScenarioSelector } from "./ScenarioSelector";
import { JustificationEditor } from "./JustificationEditor";
import { AttemptResultsPanel } from "./AttemptResultsPanel";
import { SystemLabRightPanel } from "./SystemLabRightPanel";
import type { FailedTurn } from "../../chat/components/StreamingChatTail";

export function SystemLabWindow() {
  const [session, setSession]                   = useState<SystemLabSession | null>(null);
  const [scenario, setScenario]                 = useState<ScenarioResponse | null>(null);
  const [justification, setJustification]       = useState("");
  const [lastResult, setLastResult]             = useState<AttemptResult | null>(null);
  const [chatMessages, setChatMessages]         = useState<SystemLabChatMessage[]>([]);
  const [failedChatTurn, setFailedChatTurn]     = useState<FailedTurn | null>(null);
  const [chatDraft, setChatDraft]               = useState<{ text: string } | null>(null);

  const { provider, isReady } = useProviderPreferenceContext();
  const getScenarios  = useGetScenarios();
  const startSession  = useStartSession();
  const submitAttempt = useSubmitAttempt();
  const sendChat      = useSystemLabChat();

  const { registerReset, unregisterReset } = useNavigationContext();

  // Horizontal split: left (editor + results) / right (info + chat)
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);

  // Vertical split for left panel: editor top / results bottom — open on submit error too
  const resultsOpen =
    submitAttempt.isPending || lastResult !== null || submitAttempt.isError;
  const { topPercent, setTopPercent, dividerProps: vertDividerProps, containerRef: vertContainerRef } =
    useResizableVerticalSplit(60);

  // == Register nav reset handler == //
  useEffect(() => {
    registerReset("system-lab", () => {
      setSession(null);
      setScenario(null);
      setLastResult(null);
      setJustification("");
      setChatMessages([]);
    });
    return () => unregisterReset("system-lab");
  }, [registerReset, unregisterReset]);

  // Snap editor to full height when results close, restore split when they open
  useEffect(() => {
    setTopPercent(resultsOpen ? 60 : 100);
  }, [resultsOpen, setTopPercent]);

  // == Handlers == //

  function handleSelectScenario(scenarioId: string) {
    const found = getScenarios.data?.find((s) => s.scenarioId === scenarioId);
    if (!found) return;

    startSession.mutate(
      {
        scenarioId,
        ...(provider !== undefined ? { provider } : {}),
      },
      {
        onSuccess: (data) => {
          setSession(data);
          setScenario(found);
          setJustification("");
          setLastResult(null);
          setChatMessages([]);
        },
      }
    );
  }

  function handleSubmit() {
    if (!session || !justification.trim()) return;

    submitAttempt.mutate(
      { sessionId: session.sessionId, justificationContent: justification },
      {
        onSuccess: (result) => {
          setLastResult(result);
          setSession((prev) =>
            prev ? { ...prev, attempts: [...prev.attempts, result] } : prev
          );
        },
      }
    );
  }

  function handleSendChat(message: string) {
    if (!session) return;

    // Optimistically append user message
    setFailedChatTurn(null);
    setChatDraft(null);
    setChatMessages((prev) => [...prev, { role: "user", content: message }]);

    sendChat.mutate(
      { sessionId: session.sessionId, message, currentJustification: justification || undefined },
      {
        onSuccess: (data) => {
          setChatMessages((prev) => [...prev, { role: "assistant", content: data.response }]);
        },
        onError: (error) => {
          setChatMessages((prev) => prev.slice(0, -1));
          const partial = sendChat.getStreamedText();
          setFailedChatTurn({
            failure: interpretError(error),
            partial: partial.trim() ? partial : undefined,
          });
          setChatDraft({ text: message });
        },
      }
    );
  }

  const submitError =
    submitAttempt.isError && submitAttempt.error && !submitAttempt.isPending
      ? interpretError(submitAttempt.error)
      : null;

  // == No session: show scenario selector == //
  if (!session || !scenario) {
    return (
      <div className="flex h-full justify-center overflow-y-auto px-4 py-6">
        <div className="w-full max-w-2xl">
          <ScenarioSelector
            scenarios={getScenarios.data ?? []}
            isLoading={getScenarios.isLoading}
            isStarting={startSession.isPending}
            isReady={isReady}
            onSelect={handleSelectScenario}
          />
          {getScenarios.isError && getScenarios.error && (
            <div className="mt-4 flex justify-center">
              <FailureNotice failure={interpretError(getScenarios.error)} className="text-center" />
            </div>
          )}
          {startSession.isError && startSession.error && (
            <div className="mt-4 flex justify-center">
              <FailureNotice failure={interpretError(startSession.error)} className="text-center" />
            </div>
          )}
        </div>
      </div>
    );
  }

  // == Active session: split-panel view == //
  return (
    <div className="flex h-full flex-col">
      {/* == Session Badge Row == */}
      <div className="flex items-center gap-2 border-b border-gray-700 px-6 py-2">
        <span className="rounded bg-gray-700 px-3 py-1 text-xs text-gray-300">{scenario.difficulty}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-xs text-gray-300">
          {scenario.category.replace(/([A-Z])/g, " $1").trim()}
        </span>
        <span className="rounded bg-gray-700 px-3 py-1 text-xs text-gray-300">
          {scenario.evaluationMode.replace(/([A-Z])/g, " $1").trim()}
        </span>
      </div>

      {/* == Split Screen Body == */}
      <div ref={containerRef} className="flex flex-1 overflow-hidden">
        {/* == Left Panel: Editor + Results == */}
        <div style={{ width: `${leftPercent}%` }} ref={vertContainerRef} className="flex flex-col overflow-hidden">
          {/* == Justification Editor (top) == */}
          <div style={{ height: `${topPercent}%` }} className="min-h-0">
            <JustificationEditor
              value={justification}
              onChange={setJustification}
              onSubmit={handleSubmit}
              isSubmitting={submitAttempt.isPending}
            />
          </div>

          {/* == Draggable Vertical Divider == */}
          {resultsOpen && (
            <div
              {...vertDividerProps}
              role="separator"
              aria-orientation="horizontal"
              className="h-1.5 shrink-0 cursor-row-resize bg-gray-700 transition-colors hover:bg-accent active:bg-accent"
            />
          )}

          {/* == Results Panel (bottom) == */}
          {resultsOpen && (
            <div style={{ height: `${100 - topPercent}%` }} className="min-h-0">
              <AttemptResultsPanel
                result={lastResult}
                isEvaluating={submitAttempt.isPending}
                onClear={() => setLastResult(null)}
                error={submitError}
              />
            </div>
          )}
        </div>

        {/* == Horizontal Draggable Divider == */}
        <div
          {...dividerProps}
          role="separator"
          aria-orientation="vertical"
          className="w-1.5 shrink-0 cursor-col-resize bg-gray-700 transition-colors hover:bg-accent active:bg-accent"
        />

        {/* == Right Panel: Scenario Info + Guidance Chat == */}
        <div className="min-w-0" style={{ width: `${100 - leftPercent}%` }}>
          <SystemLabRightPanel
            scenario={scenario}
            isSubmitting={submitAttempt.isPending}
            canSubmit={justification.trim().length > 0}
            attemptCount={session.attempts.length}
            onSubmit={handleSubmit}
            submitError={submitError}
            chatMessages={chatMessages}
            onSendMessage={handleSendChat}
            isSending={sendChat.isPending}
            streamingText={sendChat.streamingText}
            failedTurn={failedChatTurn}
            draft={chatDraft}
          />
        </div>
      </div>
    </div>
  );
}
