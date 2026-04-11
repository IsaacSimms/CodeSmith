// == Code Panel Component == //
import Editor from "@monaco-editor/react";
import type { Language } from "../types";
import { monacoLanguageIds } from "../types";

interface CodePanelProps {
  code: string;
  onCodeChange: (value: string) => void;
  language: Language;
  onGenerateNew: () => void;
  isGenerating: boolean;
}

export function CodePanel({ code, onCodeChange, language, onGenerateNew, isGenerating }: CodePanelProps) {

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == Panel Header == */}
      <div className="flex items-center justify-between border-b border-gray-900 bg-gray-800 px-4 py-2">
        <h2 className="text-sm font-semibold text-gray-400">Code</h2>
        <button
          onClick={onGenerateNew}
          disabled={isGenerating}
          className="rounded px-2 py-1 text-xs font-medium text-gray-300 transition-colors hover:bg-gray-700 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isGenerating ? "Generating…" : "Generate New Problem"}
        </button>
      </div>

      {/* == Monaco Editor == */}
      <div className="flex-1">
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
    </div>
  );
}
