// == Chat Window Component == //
import { useState } from "react";
import type { ProblemSession, Difficulty, ChatMessage } from "../types";
import { useCreateSession } from "../hooks/useCreateSession";
import { useSendMessage } from "../hooks/useSendMessage";
import { DifficultySelector } from "./DifficultySelector";
import { CodePanel } from "./CodePanel";
import { ChatPanel } from "./ChatPanel";

export function ChatWindow() {
  const [session, setSession] = useState<ProblemSession | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);

  const createSession = useCreateSession();
  const sendMessage = useSendMessage();

  function handleSelectDifficulty(difficulty: Difficulty) {
    createSession.mutate(
      { difficulty },
      {
        onSuccess: (data) => {
          setSession(data);
          setMessages(data.messages);
        },
      }
    );
  }

  function handleSendMessage(message: string) {
    if (!session) return;

    const userMessage: ChatMessage = {
      role: "User",
      content: message,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    sendMessage.mutate(
      { sessionId: session.sessionId, message },
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
      <div className="flex h-screen items-center justify-center bg-gray-900">
        <div>
          <DifficultySelector onSelect={handleSelectDifficulty} isLoading={createSession.isPending} />
          {createSession.isError && (
            <p className="mt-4 text-center text-red-400">{createSession.error.message}</p>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col bg-gray-900">
      {/* == Header == */}
      <header className="flex items-center justify-between border-b border-gray-700 px-6 py-4">
        <h1 className="text-lg font-bold text-white">CodeSmith</h1>
        <span className="rounded bg-gray-700 px-3 py-1 text-sm text-gray-300">{session.difficulty}</span>
      </header>

      {/* == Split Screen Body == */}
      <div className="flex flex-1 overflow-hidden">
        {/* == Left Panel: Code (75%) == */}
        <div className="w-3/4">
          <CodePanel starterCode={session.starterCode} />
        </div>

        {/* == Right Panel: Chat (25%) == */}
        <div className="w-1/4">
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
