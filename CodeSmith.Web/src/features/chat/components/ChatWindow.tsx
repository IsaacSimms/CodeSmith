// == Chat Window Component == //
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useNavigationContext } from "../../../contexts/NavigationContext";
import { useProviderPreference } from "../../../hooks/useProviderPreference";
import type { ProblemSession, Difficulty, Language, ChatMessage, RunCodeResponse } from "../types";
import { isDifficulty, isLanguage, languageLabels } from "../types";
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
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [code, setCode] = useState("");
  const [executionResult, setExecutionResult] = useState<RunCodeResponse | null>(null);
  const [contextTokensUsed, setContextTokensUsed] = useState<number | null>(null);
  const [contextWindowSize, setContextWindowSize] = useState(200_000);

  const createSession = useCreateSession();
  const sendMessage = useSendMessage();
  const runCode = useRunCode();
  const { provider } = useProviderPreference();
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);
  const { registerReset, unregisterReset } = useNavigationContext();

  // == Register nav reset handler == //
  useEffect(() => {
    registerReset("pairedprogrammer", () => {
      setSession(null);
      setMessages([]);
      setCode("");
      setExecutionResult(null);
      setContextTokensUsed(null);
    });
    return () => unregisterReset("pairedprogrammer");
  }, [registerReset, unregisterReset]);

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
          setMessages(data.messages);
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
  function handleSendMessage(message: string, isCodeAnalysis = false) {
    if (!session) return;

    const userMessage: ChatMessage = {
      role: "User",
      content: message,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    sendMessage.mutate(
      { sessionId: session.sessionId, message, editorContent: code, isCodeAnalysis },
      {
        onSuccess: (data) => {
          const assistantMessage: ChatMessage = {
            role: "Assistant",
            content: data.response,
            timestamp: new Date().toISOString(),
          };
          setMessages((prev) => [...prev, assistantMessage]);
          setContextTokensUsed(data.contextTokensUsed);
          setContextWindowSize(data.contextWindowSize);
        },
      }
    );
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
          handleSendMessage(analysisMessage, true);
        },
      }
    );
  }

  function handleClearOutput() {
    setExecutionResult(null);
  }

  if (!session) {
    return (
      <div className="flex h-full items-center justify-center">
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
          className="w-1.5 shrink-0 cursor-col-resize bg-gray-700 transition-colors hover:bg-monokai-pink active:bg-monokai-pink"
        />

        {/* == Right Panel: Chat == */}
        <div className="min-w-0" style={{ width: `${100 - leftPercent}%` }}>
          <ChatPanel
            problemDescription={session.problemDescription}
            messages={messages}
            onSendMessage={handleSendMessage}
            isSending={sendMessage.isPending}
            contextTokensUsed={contextTokensUsed}
            contextWindowSize={contextWindowSize}
          />
        </div>
      </div>
    </div>
  );
}
