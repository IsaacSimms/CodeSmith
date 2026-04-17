// == Shared Monaco Editor Monokai Theme Definition == //
import type { Monaco } from "@monaco-editor/react";

export function defineMonokaiTheme(monaco: Monaco): void {
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
}
