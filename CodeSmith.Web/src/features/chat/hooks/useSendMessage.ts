// == Send Message Hook == //
import { useMutation } from "@tanstack/react-query";
import { sendMessage } from "../../../lib/apiClient";
import type { ChatResponse } from "../types";

interface SendMessageVariables {
  sessionId: string;
  message: string;
  editorContent?: string;
  isCodeAnalysis?: boolean;
}

export function useSendMessage() {
  return useMutation<ChatResponse, Error, SendMessageVariables>({
    mutationFn: ({ sessionId, message, editorContent, isCodeAnalysis }) => sendMessage(sessionId, { message, editorContent, isCodeAnalysis }),
  });
}
