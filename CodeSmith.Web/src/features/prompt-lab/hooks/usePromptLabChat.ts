// == Prompt Lab Guidance Chat Hook == //
import { useMutation } from "@tanstack/react-query";
import { sendPromptLabChat } from "../../../lib/apiClient";
import type { PromptLabChatResponse } from "../types";

interface ChatVariables {
  sessionId: string;
  message: string;
  editorContent?: string;
}

export function usePromptLabChat() {
  return useMutation<PromptLabChatResponse, Error, ChatVariables>({
    mutationFn: ({ sessionId, message, editorContent }) =>
      sendPromptLabChat(sessionId, { message, editorContent }),
  });
}
