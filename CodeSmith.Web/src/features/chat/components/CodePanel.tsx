// == Code Panel Component == //
import { useEffect } from "react";
import Editor from "@monaco-editor/react";
import type { Language, RunCodeResponse } from "../types";
import { monacoLanguageIds } from "../types";
import { useResizableVerticalSplit } from "../hooks/useResizableVerticalSplit";
import { TerminalPanel } from "./TerminalPanel";

interface CodePanelProps {
  code: string;
  onCodeChange: (value: string) => void;
  language: Language;
  onGenerateNew: () => void;
  isGenerating: boolean;
  onRunCode: () => void;
  isRunning: boolean;
  executionResult: RunCodeResponse | null;
  onClearOutput: () => void;
}

export function CodePanel({
  code,
  onCodeChange,
  language,
  onGenerateNew,
  isGenerating,
  onRunCode,
  isRunning,
  executionResult,
  onClearOutput,
}: CodePanelProps) {
  const terminalOpen = isRunning || executionResult !== null;
  const { topPercent, setTopPercent, dividerProps, containerRef } = useResizableVerticalSplit(75);

  // Snap editor to full height when terminal closes, restore split when it opens
  useEffect(() => {
    setTopPercent(terminalOpen ? 75 : 100);
  }, [terminalOpen, setTopPercent]);

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Panel Header == */}
      <div className="flex items-center justify-between border-b border-gray-900 bg-gray-800 px-4 py-2">
        <h2 className="text-sm font-semibold text-gray-400">Code</h2>
        <div className="flex items-center gap-2">
          <button
            onClick={onGenerateNew}
            disabled={isGenerating}
            className="rounded px-2 py-1 text-xs font-medium text-gray-300 transition-colors hover:bg-gray-700 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isGenerating ? "Generating…" : "Generate New Problem"}
          </button>
          <button
            onClick={onRunCode}
            disabled={isRunning}
            className="rounded bg-green-800 px-2 py-1 text-xs font-medium text-green-200 transition-colors hover:bg-green-700 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isRunning ? "Running…" : "Test Code"}
          </button>
        </div>
      </div>

      {/* == Editor + Terminal Split == */}
      <div ref={containerRef} className="flex flex-1 flex-col overflow-hidden">
        {/* == Monaco Editor == */}
        <div style={{ height: `${topPercent}%` }} className="min-h-0">
          <Editor
            height="100%"
            language={monacoLanguageIds[language]}
            theme="vs-dark"
            value={code}
            onChange={(value) => onCodeChange(value ?? "")}
            beforeMount={(monaco) => {
              monaco.editor.defineTheme("monokai", {
                base: "vs-dark",
                inherit: true,
                rules: [
                  { token: "comment",    foreground: "75715E", fontStyle: "italic" },
                  { token: "keyword",    foreground: "F92672" },
                  { token: "string",     foreground: "E6DB74" },
                  { token: "number",     foreground: "AE81FF" },
                  { token: "type",       foreground: "66D9EF", fontStyle: "italic" },
                  { token: "class",      foreground: "A6E22E" },
                  { token: "function",   foreground: "A6E22E" },
                  { token: "variable",   foreground: "F8F8F2" },
                  { token: "operator",   foreground: "F92672" },
                  { token: "delimiter",  foreground: "F8F8F2" },
                  { token: "identifier", foreground: "F8F8F2" },
                ],
                colors: {
                  "editor.background":                "#272822",
                  "editor.foreground":                "#F8F8F2",
                  "editor.lineHighlightBackground":   "#3E3D32",
                  "editor.selectionBackground":       "#49483E",
                  "editorCursor.foreground":           "#F8F8F0",
                  "editorWhitespace.foreground":       "#464741",
                  "editorLineNumber.foreground":       "#90908A",
                  "editorLineNumber.activeForeground": "#C2C2BF",
                },
              });
              monaco.editor.setTheme("monokai");
            }}
            onMount={(_editor, monaco) => {
              monaco.editor.setTheme("monokai");
            }}
            options={{
              fontSize: 14,
              minimap: { enabled: false },
              scrollBeyondLastLine: false,
              automaticLayout: true,
              padding: { top: 12, bottom: 12 },
            }}
          />
        </div>

        {/* == Draggable Divider (visible when terminal is open) == */}
        {terminalOpen && (
          <div
            {...dividerProps}
            role="separator"
            aria-orientation="horizontal"
            className="h-1.5 shrink-0 cursor-row-resize bg-gray-700 transition-colors hover:bg-monokai-pink active:bg-monokai-pink"
          />
        )}

        {/* == Terminal Panel == */}
        {terminalOpen && (
          <div style={{ height: `${100 - topPercent}%` }} className="min-h-0">
            <TerminalPanel
              result={executionResult}
              isRunning={isRunning}
              onClear={onClearOutput}
            />
          </div>
        )}
      </div>
    </div>
  );
}
