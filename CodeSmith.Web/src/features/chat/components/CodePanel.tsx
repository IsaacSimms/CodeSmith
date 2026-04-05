// == Code Panel Component == //
import { useState } from "react";
import Editor from "@monaco-editor/react";

interface CodePanelProps {
  starterCode: string;
}

export function CodePanel({ starterCode }: CodePanelProps) {
  const [code, setCode] = useState(starterCode);

  return (
    <div className="flex h-full flex-col overflow-hidden border-r border-gray-700">
      {/* == Panel Header == */}
      <div className="border-b border-gray-700 px-4 py-2">
        <h2 className="text-sm font-semibold text-gray-400">Code</h2>
      </div>

      {/* == Monaco Editor == */}
      <div className="flex-1">
        <Editor
          height="100%"
          defaultLanguage="csharp"
          theme="vs-dark"
          value={code}
          onChange={(value) => setCode(value ?? "")}
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
