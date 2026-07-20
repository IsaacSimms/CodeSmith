// == System Lab Window Component == //
import { useEffect, useState } from "react";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreference } from "../../../hooks/useProviderPreference";
import type { ScenarioResponse, AttemptResult, SystemLabSession, SystemLabChatResponse } from "../types";
import { useGetScenarios } from "../hooks/useGetScenarios";
import { useStartSession } from "../hooks/useStartSession";
import { useSubmitAttempt } from "../hooks/useSubmitAttempt";
import { useStreamingChat } from "../../../hooks/useStreamingChat";
import { streamSystemLabChat } from "../../../lib/apiClient";
import { useResizableSplit } from "../../chat/hooks/useResizableSplit";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { ScenarioSelector } from "./ScenarioSelector";
import { JustificationEditor } from "./JustificationEditor";
import { AttemptResultsPanel } from "./AttemptResultsPanel";
import { SystemLabRightPanel } from "./SystemLabRightPanel";

export function SystemLabWindow() {
  const [session, setSession]                   = useState<SystemLabSession | null>(null);
  const [scenario, setScenario]                 = useState<ScenarioResponse | null>(null);
  const [justification, setJustification]       = useState("");
  const [lastResult, setLastResult]             = useState<AttemptResult | null>(null);

  const { provider }  = useProviderPreference();
  const getScenarios  = useGetScenarios();
  const startSession  = useStartSession();
  const submitAttempt = useSubmitAttempt();
  // The shared streaming-chat module owns the transcript and turn invariant; this surface
  // supplies its stream call (the current justification draft) as data.
  const sendChat = useStreamingChat<SystemLabChatResponse>((message, onDelta) => {
    if (!session) return Promise.reject(new Error("No active session"));
    return streamSystemLabChat(
      session.sessionId,
      { message, currentJustification: justification || undefined },
      { onDelta }
    );
  });

  const { registerReset, unregisterReset } = useNavigationContext();

  // Horizontal split: left (editor + results) / right (info + chat)
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);

  // Vertical split for left panel: editor top / results bottom
  const resultsOpen = submitAttempt.isPending || lastResult !== null;
  const { topPercent, setTopPercent, dividerProps: vertDividerProps, containerRef: vertContainerRef } =
    useResizableVerticalSplit(60);

  // == Register nav reset handler == //
  const { resetChat } = sendChat;   // stable identity, safe as an effect dependency
  useEffect(() => {
    registerReset("system-lab", () => {
      setSession(null);
      setScenario(null);
      setLastResult(null);
      setJustification("");
      resetChat();
    });
    return () => unregisterReset("system-lab");
  }, [registerReset, unregisterReset, resetChat]);

  // Snap editor to full height when results close, restore split when they open
  useEffect(() => {
    setTopPercent(resultsOpen ? 60 : 100);
  }, [resultsOpen, setTopPercent]);

  // == Handlers == //

  function handleSelectScenario(scenarioId: string) {
    const found = getScenarios.data?.find((s) => s.scenarioId === scenarioId);
    if (!found) return;

    startSession.mutate(
      { scenarioId, provider },
      {
        onSuccess: (data) => {
          setSession(data);
          setScenario(found);
          setJustification("");
          setLastResult(null);
          sendChat.resetChat();
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
    sendChat.sendMessage(message);
  }

  // == No session: show scenario selector == //
  if (!session || !scenario) {
    return (
      <div className="flex h-full justify-center overflow-y-auto px-4 py-6">
        <div className="w-full max-w-2xl">
          <ScenarioSelector
            scenarios={getScenarios.data ?? []}
            isLoading={getScenarios.isLoading}
            isStarting={startSession.isPending}
            onSelect={handleSelectScenario}
          />
          {getScenarios.isError && (
            <p className="mt-4 text-center text-sm text-red-400">
              Failed to load scenarios: {getScenarios.error.message}
            </p>
          )}
          {startSession.isError && (
            <p className="mt-4 text-center text-sm text-red-400">{startSession.error.message}</p>
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
        {session.attempts.length > 0 && (
          <span className="text-xs text-gray-500">
            Attempt {session.attempts.length + 1}
          </span>
        )}

        {/* == Submit Button (in header for quick access) == */}
        <div className="ml-auto">
          <button
            onClick={handleSubmit}
            disabled={submitAttempt.isPending || !justification.trim()}
            className="rounded bg-accent px-4 py-1.5 text-sm font-semibold text-white transition-colors hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
          >
            {submitAttempt.isPending ? "Evaluating…" : "Submit"}
          </button>
        </div>
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
            chatMessages={sendChat.messages}
            onSendMessage={handleSendChat}
            isSending={sendChat.isSending}
            streamingText={sendChat.streamingText}
            failedTurn={sendChat.failedTurn}
            draft={sendChat.draft}
          />
        </div>
      </div>
    </div>
  );
}
