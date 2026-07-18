// == Prompt Lab Window Component == //
import { useEffect, useState } from "react";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreference } from "../../../hooks/useProviderPreference";
import type { ChallengeResponse, AttemptResult, PromptLabSession, PromptLabChatMessage } from "../types";
import { useGetChallenges } from "../hooks/useGetChallenges";
import { useStartChallenge } from "../hooks/useStartChallenge";
import { useSubmitAttempt } from "../hooks/useSubmitAttempt";
import { usePromptLabChat } from "../hooks/usePromptLabChat";
import { useResizableSplit } from "../../chat/hooks/useResizableSplit";
import { useResizableVerticalSplit } from "../../chat/hooks/useResizableVerticalSplit";
import { ChallengeSelector } from "./ChallengeSelector";
import { PromptLabRightPanel } from "./PromptLabRightPanel";
import type { FailedTurn } from "../../chat/components/StreamingChatTail";
import { PromptEditors } from "./PromptEditors";
import { ResultsPanel } from "./ResultsPanel";
import { TokenUsageBar } from "../../../components/TokenUsageBar";

export function PromptLabWindow() {
  const [session, setSession]                   = useState<PromptLabSession | null>(null);
  const [challenge, setChallenge]               = useState<ChallengeResponse | null>(null);
  const [systemPromptContent, setSystemContent] = useState("");
  const [userMessageContent, setUserContent]    = useState("");
  const [lastResult, setLastResult]             = useState<AttemptResult | null>(null);
  const [chatMessages, setChatMessages]         = useState<PromptLabChatMessage[]>([]);
  const [failedChatTurn, setFailedChatTurn]     = useState<FailedTurn | null>(null);
  const [chatDraft, setChatDraft]               = useState<{ text: string } | null>(null);

  const getChallenges  = useGetChallenges();
  const startChallenge = useStartChallenge();
  const submitAttempt  = useSubmitAttempt();
  const sendChat       = usePromptLabChat();
  const { provider } = useProviderPreference();
  const { registerReset, unregisterReset } = useNavigationContext();

  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);

  // Vertical split for left panel (editors top / results bottom)
  const resultsOpen = submitAttempt.isPending || lastResult !== null;
  const { topPercent, setTopPercent, dividerProps: vertDividerProps, containerRef: vertContainerRef } =
    useResizableVerticalSplit(60);

  // == Register nav reset handler == //
  useEffect(() => {
    registerReset("prompt-lab", () => {
      setSession(null);
      setChallenge(null);
      setLastResult(null);
      setChatMessages([]);
    });
    return () => unregisterReset("prompt-lab");
  }, [registerReset, unregisterReset]);

  // Snap editors to full height when results close, restore split when they open
  useEffect(() => {
    setTopPercent(resultsOpen ? 60 : 100);
  }, [resultsOpen, setTopPercent]);

  // Ctrl+Enter submits the prompt from anywhere in the window
  useEffect(() => {
    if (!session) return;

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Enter" && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey && !submitAttempt.isPending) {
        e.preventDefault();
        handleSubmit();
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session, systemPromptContent, userMessageContent, submitAttempt.isPending]);

  // Pre-fill editable field defaults when challenge loads
  useEffect(() => {
    if (!challenge) return;
    const sysField  = challenge.editableFields.find((f) => f.fieldType === "SystemPrompt");
    const userField = challenge.editableFields.find((f) => f.fieldType === "UserMessage");
    setSystemContent(sysField?.defaultValue ?? "");
    setUserContent(userField?.defaultValue ?? "");
    setLastResult(null);
  }, [challenge]);

  // == Handlers == //

  function handleSelectChallenge(challengeId: string) {
    const found = getChallenges.data?.find((c) => c.challengeId === challengeId);
    if (!found) return;

    startChallenge.mutate(
      { challengeId, provider },
      {
        onSuccess: (data) => {
          setSession(data);
          setChallenge(found);
          setChatMessages([]);
        },
      }
    );
  }

  function handleSubmit() {
    if (!session) return;

    submitAttempt.mutate(
      {
        sessionId:           session.sessionId,
        systemPromptContent,
        userMessageContent,
      },
      {
        onSuccess: (result) => {
          setLastResult(result);
          // Also update session attempts list locally so the count stays accurate
          setSession((prev) =>
            prev ? { ...prev, attempts: [...prev.attempts, result] } : prev
          );
        },
      }
    );
  }

  function handleSendChat(message: string) {
    if (!session) return;

    // Build structured editor content from both prompt editors
    const editorContent = [
      systemPromptContent ? `[SYSTEM PROMPT]\n${systemPromptContent}` : null,
      userMessageContent  ? `[USER MESSAGE]\n${userMessageContent}`   : null,
    ]
      .filter(Boolean)
      .join("\n\n") || undefined;

    // Optimistically append user message
    setFailedChatTurn(null);
    setChatDraft(null);
    setChatMessages((prev) => [...prev, { role: "user", content: message }]);

    sendChat.mutate(
      { sessionId: session.sessionId, message, editorContent },
      {
        onSuccess: (data) => {
          setChatMessages((prev) => [...prev, { role: "assistant", content: data.response }]);
        },
        onError: (error) => {
          // The server rolled the turn back — mirror it: drop the optimistic user bubble, keep
          // the partial reply visible as a failed turn, and put the message back in the input.
          setChatMessages((prev) => prev.slice(0, -1));
          setFailedChatTurn({ partial: sendChat.getStreamedText(), message: error.message });
          setChatDraft({ text: message });
        },
      }
    );
  }

  // == No session: show challenge selector == //
  if (!session || !challenge) {
    return (
      <div className="flex h-full justify-center overflow-hidden px-4 py-6">
        <div className="w-full max-w-2xl overflow-hidden">
          <ChallengeSelector
            challenges={getChallenges.data ?? []}
            isLoading={getChallenges.isLoading}
            isStarting={startChallenge.isPending}
            onSelect={handleSelectChallenge}
          />
          {getChallenges.isError && (
            <p className="mt-4 text-center text-sm text-red-400">
              Failed to load challenges: {getChallenges.error.message}
            </p>
          )}
          {startChallenge.isError && (
            <p className="mt-4 text-center text-sm text-red-400">{startChallenge.error.message}</p>
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
        <span className="rounded bg-gray-700 px-3 py-1 text-xs text-gray-300">{challenge.difficulty}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-xs text-gray-300">
          {challenge.category.replace(/([A-Z])/g, " $1").trim()}
        </span>

        {!session.dynamicInputsGenerated && (
          <span className="rounded border border-yellow-600 bg-yellow-900/30 px-3 py-1 text-xs text-yellow-400">
            Static test inputs — dynamic generation failed
          </span>
        )}

        {/* == Prompt Size Bar (appears after first submission) == */}
        {lastResult !== null && (
          <div className="ml-auto w-72">
            <TokenUsageBar
              tokensUsed={lastResult.promptTokensUsed}
              contextWindowSize={lastResult.contextWindowSize}
              label="Prompt Size"
              description="Tokens your prompt uses per simulation call. Unlike the paired programmer, this stays roughly constant between submissions."
            />
          </div>
        )}
      </div>

      {/* == Split Screen Body == */}
      <div ref={containerRef} className="flex flex-1 overflow-hidden">
        {/* == Left Panel: Editors + Results == */}
        <div style={{ width: `${leftPercent}%` }} ref={vertContainerRef} className="flex flex-col overflow-hidden">
          {/* == Prompt Editors (top) == */}
          <div style={{ height: `${topPercent}%` }} className="min-h-0">
            <PromptEditors
              challenge={challenge}
              systemPromptContent={systemPromptContent}
              userMessageContent={userMessageContent}
              onSystemPromptChange={setSystemContent}
              onUserMessageChange={setUserContent}
              onSubmit={handleSubmit}
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
              <ResultsPanel
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

        {/* == Right Panel: Challenge + Guidance Chat == */}
        <div className="min-w-0" style={{ width: `${100 - leftPercent}%` }}>
          <PromptLabRightPanel
            challenge={challenge}
            testInputs={session.testInputs}
            isSubmitting={submitAttempt.isPending}
            lastAttempt={lastResult}
            attemptCount={session.attempts.length}
            onSubmit={handleSubmit}
            chatMessages={chatMessages}
            onSendMessage={handleSendChat}
            isSendingChat={sendChat.isPending}
            streamingText={sendChat.streamingText}
            failedTurn={failedChatTurn}
            draft={chatDraft}
          />
        </div>
      </div>
    </div>
  );
}
 