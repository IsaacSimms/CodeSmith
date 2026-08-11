// == System Lab Guidance Chat Hook == //
import { streamSystemLabChat } from "../../../lib/apiClient";
import { useGuidanceChat } from "../../../hooks/useGuidanceChat";
import type { SystemLabChatMessage, SystemLabChatResponse } from "../types";

/// System Lab adapter over the shared guidance chat state machine — supplies the surface's message
/// shape and streaming call; turn mechanics live in useGuidanceChat.
export function useSystemLabChat() {
  const chat = useGuidanceChat<SystemLabChatMessage, SystemLabChatResponse>({
    toUserMessage: (message) => ({ role: "user", content: message }),
    toAssistantMessage: (data) => ({ role: "assistant", content: data.response }),
  });

  function sendTurn(sessionId: string, message: string, currentJustification?: string) {
    return chat
      .send(message, (onDelta) => streamSystemLabChat(sessionId, { message, currentJustification }, { onDelta }))
      .catch(() => {}); // failure is surfaced via failedTurn state, not an unhandled rejection
  }

  return { ...chat, sendTurn };
}
