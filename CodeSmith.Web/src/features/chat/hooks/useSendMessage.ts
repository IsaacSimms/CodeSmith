// == Send Message Hook == //
import { useMutation } from "@tanstack/react-query";
import { sendMessage } from "../../../lib/apiClient";
import type { ChatResponse, GuidanceMode } from "../types";

interface SendMessageVariables {
  sessionId: string;
  message: string;
  editorContent?: string;
  guidanceMode?: GuidanceMode;
}

export function useSendMessage() {
  return useMutation<ChatResponse, Error, SendMessageVariables>({
    mutationFn: ({ sessionId, message, editorContent, guidanceMode }) => sendMessage(sessionId, { message, editorContent, guidanceMode }),
  });
}
