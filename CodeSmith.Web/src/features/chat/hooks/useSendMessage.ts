// == Send Message Hook == //
import { useMutation } from "@tanstack/react-query";
import { sendMessage } from "../../../lib/apiClient";
import type { ChatResponse } from "../types";

interface SendMessageVariables {
  sessionId: string;
  message: string;
}

export function useSendMessage() {
  return useMutation<ChatResponse, Error, SendMessageVariables>({
    mutationFn: ({ sessionId, message }) => sendMessage(sessionId, { message }),
  });
}
