// == Send Message Hook == //
import { useState } from "react";
import { streamChat } from "../../../lib/apiClient";
import { useGuidanceChat } from "../../../hooks/useGuidanceChat";
import type { ChatMessage, ChatResponse, GuidanceMode } from "../types";

/// Tutoring adapter over the shared guidance chat state machine: supplies the surface's message
/// shape and streaming call, and tracks the context-token telemetry the tutoring UI displays.
/// Turn mechanics (optimistic append, rollback, partial snapshot, draft, settle invalidation)
/// live in useGuidanceChat.
export function useSendMessage() {
  const [contextTokensUsed, setContextTokensUsed] = useState<number | null>(null);
  const [contextWindowSize, setContextWindowSize] = useState(200_000);

  const chat = useGuidanceChat<ChatMessage, ChatResponse>({
    toUserMessage: (message) => ({ role: "User", content: message, timestamp: new Date().toISOString() }),
    toAssistantMessage: (data) => ({ role: "Assistant", content: data.response, timestamp: new Date().toISOString() }),
    onTurnSuccess: (data) => {
      setContextTokensUsed(data.contextTokensUsed);
      setContextWindowSize(data.contextWindowSize);
    },
  });

  function sendTurn(sessionId: string, message: string, editorContent?: string, guidanceMode?: GuidanceMode) {
    return chat
      .send(message, (onDelta) => streamChat(sessionId, { message, editorContent, guidanceMode }, { onDelta }))
      .catch(() => {}); // failure is surfaced via failedTurn state, not an unhandled rejection
  }

  // Clears the token telemetry when the session goes away (nav reset / new problem).
  function resetContextUsage() {
    setContextTokensUsed(null);
    setContextWindowSize(200_000);
  }

  return { ...chat, sendTurn, contextTokensUsed, contextWindowSize, resetContextUsage };
}
