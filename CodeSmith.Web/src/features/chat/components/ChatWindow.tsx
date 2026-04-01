// == Chat Window Component == //
import { useState, useRef, useEffect } from "react";
import type { ProblemSession, Difficulty, ChatMessage } from "../types";
import { useCreateSession } from "../hooks/useCreateSession";
import { useSendMessage } from "../hooks/useSendMessage";
import { DifficultySelector } from "./DifficultySelector";
import { MessageBubble } from "./MessageBubble";
import { ChatInput } from "./ChatInput";

export function ChatWindow() {
  const [session, setSession] = useState<ProblemSession | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const createSession = useCreateSession();
  const sendMessage = useSendMessage();

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

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

      {/* == Problem Description == */}
      <div className="border-b border-gray-700 bg-gray-800 px-6 py-4">
        <h2 className="mb-2 font-semibold text-white">Problem</h2>
        <p className="whitespace-pre-wrap text-gray-300">{session.problemDescription}</p>
        {session.starterCode && (
          <pre className="mt-3 overflow-x-auto rounded bg-gray-900 p-3 text-sm text-green-400">
            {session.starterCode}
          </pre>
        )}
      </div>

      {/* == Messages == */}
      <div className="flex-1 overflow-y-auto p-4">
        <div className="flex flex-col gap-3">
          {messages.map((msg, i) => (
            <MessageBubble key={i} role={msg.role} content={msg.content} />
          ))}
          <div ref={messagesEndRef} />
        </div>
      </div>

      {/* == Input == */}
      <ChatInput onSend={handleSendMessage} isLoading={sendMessage.isPending} />
    </div>
  );
}
