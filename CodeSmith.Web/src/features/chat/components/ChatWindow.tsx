// == Chat Window Component == //
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import type { ProblemSession, Difficulty, Language, ChatMessage } from "../types";
import { isDifficulty, isLanguage, languageLabels } from "../types";
import { useCreateSession } from "../hooks/useCreateSession";
import { useSendMessage } from "../hooks/useSendMessage";
import { useResizableSplit } from "../hooks/useResizableSplit";
import { DifficultySelector } from "./DifficultySelector";
import { CodePanel } from "./CodePanel";
import { ChatPanel } from "./ChatPanel";

export function ChatWindow() {
  const [searchParams] = useSearchParams();
  const [session, setSession] = useState<ProblemSession | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [code, setCode] = useState("");

  const createSession = useCreateSession();
  const sendMessage = useSendMessage();
  const { leftPercent, dividerProps, containerRef } = useResizableSplit(75);

  // == URL Param Seeding (Option A) == //
  const urlLangRaw = searchParams.get("lang");
  const urlDifficultyRaw = searchParams.get("difficulty");
  const initialLanguage: Language | undefined = isLanguage(urlLangRaw) ? urlLangRaw : undefined;
  const initialDifficulty: Difficulty | undefined = isDifficulty(urlDifficultyRaw) ? urlDifficultyRaw : undefined;

  function handleStart(difficulty: Difficulty, language: Language) {
    createSession.mutate(
      { difficulty, language },
      {
        onSuccess: (data) => {
          setSession(data);
          setMessages(data.messages);
          setCode(data.starterCode);
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

  function handleSendMessage(message: string) {
    if (!session) return;

    const userMessage: ChatMessage = {
      role: "User",
      content: message,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    sendMessage.mutate(
      { sessionId: session.sessionId, message, editorContent: code },
      {
        onSuccess: (data) => {
          const assistantMessage: ChatMessage = {
            role: "Assistant",
            content: data.response,
            timestamp: new Date().toISOString(),
          };
          setMessages((prev) => [...prev, assistantMessage]);
        },
      }
    );
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
          <CodePanel key={session.sessionId} code={code} onCodeChange={setCode} language={session.language} />
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
          />
        </div>
      </div>
    </div>
  );
}
