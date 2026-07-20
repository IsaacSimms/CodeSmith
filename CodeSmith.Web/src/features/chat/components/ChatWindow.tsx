// == Chat Window Component == //
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreference } from "../../../hooks/useProviderPreference";
import type { ProblemSession, Difficulty, Language, RunCodeResponse, GuidanceMode, ChatResponse } from "../types";
import { isDifficulty, isLanguage, languageLabels } from "../types";
import { useCreateSession } from "../hooks/useCreateSession";
import { useStreamingChat } from "../../../hooks/useStreamingChat";
import { streamChat } from "../../../lib/apiClient";
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
  const [contextTokensUsed, setContextTokensUsed] = useState<number | null>(null);
  const [contextWindowSize, setContextWindowSize] = useState(200_000);

  const createSession = useCreateSession();
  // The shared streaming-chat module owns the transcript and turn invariant; this surface
  // supplies its stream call (current editor contents + per-send guidance mode) as data.
  const chat = useStreamingChat<ChatResponse, GuidanceMode>((message, onDelta, guidanceMode) => {
    if (!session) return Promise.reject(new Error("No active session"));
    return streamChat(
      session.sessionId,
      { message, editorContent: code, guidanceMode: guidanceMode ?? "Guidance" },
      { onDelta }
    );
  });
  const runCode = useRunCode();
  const { provider } = useProviderPreference();
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);
  const { registerReset, unregisterReset } = useNavigationContext();

  // == Register nav reset handler == //
  const { resetChat } = chat;   // stable identity, safe as an effect dependency
  useEffect(() => {
    registerReset("pairedprogrammer", () => {
      setSession(null);
      resetChat();
      setCode("");
      setExecutionResult(null);
      setContextTokensUsed(null);
    });
    return () => unregisterReset("pairedprogrammer");
  }, [registerReset, unregisterReset, resetChat]);

  // == URL Param Seeding (Option A) == //
  const urlLangRaw = searchParams.get("lang");
  const urlDifficultyRaw = searchParams.get("difficulty");
  const initialLanguage: Language | undefined = isLanguage(urlLangRaw) ? urlLangRaw : undefined;
  const initialDifficulty: Difficulty | undefined = isDifficulty(urlDifficultyRaw) ? urlDifficultyRaw : undefined;

  function handleStart(difficulty: Difficulty, language: Language) {
    createSession.mutate(
      { difficulty, language, provider },
      {
        onSuccess: (data) => {
          setSession(data);
          chat.resetChat(data.messages);
          setCode(data.starterCode);
          setExecutionResult(null);
        },
      }
    );
  }

  // == Auto-start when both URL params are present == //
  // One-shot ref guard prevents StrictMode double-fire and retry loops on error.
  const autoStartedRef = useRef(false);
  useEffect(() => {
    if (autoStartedRef.current) return;
    if (session) return;
    if (!initialDifficulty || !initialLanguage) return;

    autoStartedRef.current = true;
    handleStart(initialDifficulty, initialLanguage);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // == Send Chat Message == //
  function handleSendMessage(message: string, guidanceMode: GuidanceMode = "Guidance") {
    if (!session) return;

    chat.sendMessage(message, {
      context: guidanceMode,
      onSuccess: (reply) => {
        setContextTokensUsed(reply.contextTokensUsed);
        setContextWindowSize(reply.contextWindowSize);
      },
    });
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
            <p className="whitespace-pre-wrap text-sm text-gray-300">{createSession.streamingDescription}</p>
          </div>
        ) : (
          <div>
            <DifficultySelector
              onSelect={handleStart}
              isLoading={createSession.isPending}
              initialLanguage={initialLanguage}
            />
            {createSession.isError && (
              <p className="mt-4 text-center text-red-400">{createSession.error.message}</p>
            )}
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      {/* == Session Badge Row == */}
      <div className="flex items-center justify-end gap-2 border-b border-gray-700 px-6 py-2">
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{session.difficulty}</span>
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{languageLabels[session.language]}</span>
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
            onGenerateNew={() => handleStart(session.difficulty, session.language)}  
            isGenerating={createSession.isPending}
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
            messages={chat.messages}
            onSendMessage={handleSendMessage}
            isSending={chat.isSending}
            streamingText={chat.streamingText}
            failedTurn={chat.failedTurn}
            draft={chat.draft}
            contextTokensUsed={contextTokensUsed}
            contextWindowSize={contextWindowSize}
          />
        </div>
      </div>
    </div>
  );
}
