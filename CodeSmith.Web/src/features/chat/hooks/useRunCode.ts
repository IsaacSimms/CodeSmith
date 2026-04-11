// == Run Code Hook == //
import { useMutation } from "@tanstack/react-query";
import { runCode } from "../../../lib/apiClient";
import type { RunCodeResponse, Language } from "../types";

interface RunCodeVariables {
  sessionId: string;
  code: string;
  language: Language;
}

export function useRunCode() {
  return useMutation<RunCodeResponse, Error, RunCodeVariables>({
    mutationFn: ({ sessionId, code, language }) => runCode(sessionId, { code, language }),
  });
}
