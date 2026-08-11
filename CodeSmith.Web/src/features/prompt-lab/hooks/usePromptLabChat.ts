// == Prompt Lab Guidance Chat Hook == //
import { streamPromptLabChat } from "../../../lib/apiClient";
import { useGuidanceChat } from "../../../hooks/useGuidanceChat";
import type { PromptLabChatMessage, PromptLabChatResponse } from "../types";

/// Prompt Lab adapter over the shared guidance chat state machine — supplies the surface's message
/// shape and streaming call; turn mechanics live in useGuidanceChat.
export function usePromptLabChat() {
  const chat = useGuidanceChat<PromptLabChatMessage, PromptLabChatResponse>({
    toUserMessage: (message) => ({ role: "user", content: message }),
    toAssistantMessage: (data) => ({ role: "assistant", content: data.response }),
  });

  function sendTurn(sessionId: string, message: string, editorContent?: string) {
    return chat
      .send(message, (onDelta) => streamPromptLabChat(sessionId, { message, editorContent }, { onDelta }))
      .catch(() => {}); // failure is surfaced via failedTurn state, not an unhandled rejection
  }

  return { ...chat, sendTurn };
}
