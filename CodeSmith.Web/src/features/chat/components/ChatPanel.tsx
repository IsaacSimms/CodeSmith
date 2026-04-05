// == Chat Panel Component == //
import { useRef, useEffect } from "react";
import type { ChatMessage } from "../types";
import { MessageBubble } from "./MessageBubble";
import { ChatInput } from "./ChatInput";

interface ChatPanelProps {
  problemDescription: string;
  messages: ChatMessage[];
  onSendMessage: (message: string) => void;
  isSending: boolean;
}

export function ChatPanel({ problemDescription, messages, onSendMessage, isSending }: ChatPanelProps) {
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  return (
    <div className="flex h-full flex-col">
      {/* == Problem Description == */}
      <div className="border-b border-gray-700 bg-gray-800 px-4 py-3">
        <h2 className="mb-1 text-sm font-semibold text-gray-400">Problem</h2>
        <p className="whitespace-pre-wrap text-sm text-gray-300">{problemDescription}</p>
      </div>

      {/* == Messages == */}
      <div className="flex-1 overflow-y-auto p-3">
        <div className="flex flex-col gap-3">
          {messages.map((msg, i) => (
            <MessageBubble key={i} role={msg.role} content={msg.content} />
          ))}
          <div ref={messagesEndRef} />
        </div>
      </div>

      {/* == Input == */}
      <ChatInput onSend={onSendMessage} isLoading={isSending} />
    </div>
  );
}
