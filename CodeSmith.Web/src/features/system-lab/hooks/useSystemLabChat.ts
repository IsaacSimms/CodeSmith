// == System Lab Guidance Chat Hook == //
import { useMutation } from "@tanstack/react-query";
import { sendSystemLabChat } from "../../../lib/apiClient";
import type { SystemLabChatResponse } from "../types";

interface ChatVariables {
  sessionId: string;
  message: string;
  currentJustification?: string;
}

export function useSystemLabChat() {
  return useMutation<SystemLabChatResponse, Error, ChatVariables>({
    mutationFn: ({ sessionId, message, currentJustification }) =>
      sendSystemLabChat(sessionId, { message, currentJustification }),
  });
}
