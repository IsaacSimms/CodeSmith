// == Chat Window Component == //
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreferenceContext } from "../../../contexts/ProviderPreferenceContext";
import type {
  ProblemSession, Difficulty, Language, RunCodeResponse, GuidanceMode,
  ProblemFocus, ProblemTopic,
} from "../types";
import {
  isDifficulty, isLanguage, isProblemFocus, isProblemTopic,
  languageLabels, problemFocusLabels, problemTopicLabels,
} from "../types";
import { interpretError } from "../../../lib/clientError";
import { FailureNotice } from "../../shared/FailureNotice";
import { useCreateSession } from "../hooks/useCreateSession";
import { useSendMessage } from "../hooks/useSendMessage";
import { useRunCode } from "../hooks/useRunCode";
import { useResizableSplit } from "../hooks/useResizableSplit";
import { DifficultySelector } from "./DifficultySelector";
import { CodePanel } from "./CodePanel";
import { ChatPanel } from "./ChatPanel";

export function ChatWindow() {
  const [searchParams] = useSearchParams();
  const [session, setSession] = useState<ProblemSession | null>(null);
  const [code, setCode] = useState("");
  const [executionResult, setExecutionResult] = useState<RunCodeResponse | null>(null);

  const createSession = useCreateSession();
  // The guidance turn state machine (messages, optimistic append/rollback, failed turn, draft,
  // context tokens) lives in the hook — this window only renders it and wires the session.
  const sendMessage = useSendMessage();
  const { messages, setMessages, failedTurn, draft, contextTokensUsed, contextWindowSize } = sendMessage;
  const runCode = useRunCode();
  const { provider, isReady } = useProviderPreferenceContext();
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);
  const { registerReset, unregisterReset } = useNavigationContext();

  // == Register nav reset handler == //
  useEffect(() => {
    registerReset("pairedprogrammer", () => {
      setSession(null);
      setMessages([]);
      setCode("");
      setExecutionResult(null);
      sendMessage.resetContextUsage();
      // focus/topic deliberately survive the reset so a drill session costs one selection, not one per problem
    });
    return () => unregisterReset("pairedprogrammer");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [registerReset, unregisterReset]);

  // == URL Param Seeding (Option A) == //
  // focus and topic are optional additions: auto-start still keys only on lang + difficulty, so
  // every pre-existing bookmark behaves exactly as before.
  const urlLangRaw = searchParams.get("lang");
  const urlDifficultyRaw = searchParams.get("difficulty");
  const urlFocusRaw = searchParams.get("focus");
  const urlTopicRaw = searchParams.get("topic");
  const initialLanguage: Language | undefined = isLanguage(urlLangRaw) ? urlLangRaw : undefined;
  const initialDifficulty: Difficulty | undefined = isDifficulty(urlDifficultyRaw) ? urlDifficultyRaw : undefined;

  // The user's *selection*, distinct from the session's *resolved* values below. Held here rather
  // than in DifficultySelector so a pick survives the nav reset; only a reload returns it to Random.
  const [focus, setFocus] = useState<ProblemFocus>(isProblemFocus(urlFocusRaw) ? urlFocusRaw : "Random");
  const [topic, setTopic] = useState<ProblemTopic>(isProblemTopic(urlTopicRaw) ? urlTopicRaw : "Random");

  function handleStart(difficulty: Difficulty, language: Language) {
    createSession.mutate(
      {
        difficulty,
        language,
        focus,
        topic,
        ...(provider !== undefined ? { provider } : {}),
      },
      {
        onSuccess: (data) => {
          setSession(data);
          setMessages(data.messages);
          setCode(data.starterCode);
          setExecutionResult(null);
        },
      }
    );
  }

  // == Auto-start when both URL params are present == //
  // One-shot ref guard prevents StrictMode double-fire and retry loops on error.
  // Wait for isReady so we never fire with a provisional Anthropic guess.
  const autoStartedRef = useRef(false);
  useEffect(() => {
    if (autoStartedRef.current) return;
    if (session) return;
    if (!initialDifficulty || !initialLanguage) return;
    if (!isReady) return;

    autoStartedRef.current = true;
    handleStart(initialDifficulty, initialLanguage);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isReady]);

  // == Send Chat Message == //
  function handleSendMessage(message: string, guidanceMode: GuidanceMode = "Guidance") {
    if (!session) return;
    sendMessage.sendTurn(session.sessionId, message, code, guidanceMode);
  }

  // == Run Code and Auto-Analyze == //
  function handleRunCode() {
    if (!session) return;

    runCode.mutate(
      { sessionId: session.sessionId, code, language: session.language },
      {
        onSuccess: (data) => {
          setExecutionResult(data);

          // Build analysis message with execution results
          const parts = [`I just tested my code. Here are the results:\n`];
          parts.push(`Exit code: ${data.exitCode}`);
          if (data.timedOut) parts.push(`The program timed out after 10 seconds.`);
          parts.push(`\nStdout:\n${data.stdout || "(no output)"}`);
          parts.push(`\nStderr:\n${data.stderr || "(no output)"}`);

          const analysisMessage = parts.join("\n");
          handleSendMessage(analysisMessage, "CodeAnalysis");
        },
      }
    );
  }

  function handleClearOutput() {
    setExecutionResult(null);
  }

  if (!session) {
    // Once description text starts streaming (or a retry reset arrives), swap the selector for a
    // live view of the problem being written; the editor fills only when generation completes.
    const showStreamingDescription =
      createSession.isPending && (createSession.streamingDescription.length > 0 || createSession.isRetrying);

    return (
      <div className="flex h-full items-center justify-center">
        {showStreamingDescription ? (
          <div className="w-full max-w-2xl overflow-y-auto px-6 py-8" data-testid="streaming-description">
            <h2 className="mb-2 text-sm font-semibold text-gray-400">
              {createSession.isRetrying ? "Taking another pass at your problem…" : "Writing your problem…"}
            </h2>
            <p className="whitespace-pre-wrap text-sm text-gray-100">{createSession.streamingDescription}</p>
          </div>
        ) : (
          <div>
            <DifficultySelector
              onSelect={handleStart}
              isLoading={createSession.isPending}
              isReady={isReady}
              initialLanguage={initialLanguage}
              focus={focus}
              topic={topic}
              onFocusChange={setFocus}
              onTopicChange={setTopic}
            />
            {createSession.isError && createSession.error && (
              <div className="mt-4 flex justify-center">
                <FailureNotice failure={interpretError(createSession.error)} className="text-center" />
              </div>
            )}
          </div>
        )}
      </div>
    );
  }

  const generateError =
    session && createSession.isError && createSession.error && !createSession.isPending
      ? interpretError(createSession.error)
      : null;

  return (
    <div className="flex h-full flex-col">
      {/* == Session Badge Row == */}
      {/* Focus and Topic report what was requested of the provider, not what it delivered */}
      <div className="flex items-center justify-end gap-2 border-b border-gray-700 px-6 py-2">
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{session.difficulty}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{languageLabels[session.language]}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{problemFocusLabels[session.focus]}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{problemTopicLabels[session.topic]}</span>
      </div>

      {/* == Split Screen Body == */}
      <div ref={containerRef} className="flex flex-1 overflow-hidden">
        {/* == Left Panel: Code == */}
        <div style={{ width: `${leftPercent}%` }}>
          <CodePanel
            key={session.sessionId}
            code={code}
            onCodeChange={setCode}
            language={session.language}
            // Difficulty and language come from the session, but focus/topic come from the user's
            // selection inside handleStart — passing session.focus here would silently pin a Random
            // pick to whatever the first roll produced and it could never re-roll.
            onGenerateNew={() => handleStart(session.difficulty, session.language)}
            isGenerating={createSession.isPending}
            generateError={generateError}
            onRunCode={handleRunCode}
            isRunning={runCode.isPending}
            executionResult={executionResult}
            onClearOutput={handleClearOutput}
          />
        </div>

        {/* == Draggable Divider == */}
        <div
          {...dividerProps}
          role="separator"
          aria-orientation="vertical"
          className="w-1.5 shrink-0 cursor-col-resize bg-gray-700 transition-colors hover:bg-accent active:bg-accent"
        />

        {/* == Right Panel: Chat == */}
        <div className="min-w-0" style={{ width: `${100 - leftPercent}%` }}>
          <ChatPanel
            problemDescription={session.problemDescription}
            messages={messages}
            onSendMessage={handleSendMessage}
            isSending={sendMessage.isPending}
            streamingText={sendMessage.streamingText}
            failedTurn={failedTurn}
            draft={draft}
            contextTokensUsed={contextTokensUsed}
            contextWindowSize={contextWindowSize}
          />
        </div>
      </div>
    </div>
  );
}
