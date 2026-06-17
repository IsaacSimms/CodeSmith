// == Shared Monaco Editor Theme (VS Code Dark Modern / Dark+) == //
import type { Monaco } from "@monaco-editor/react";

export function defineVsCodeDarkTheme(monaco: Monaco): void {
  monaco.editor.defineTheme("dark-modern", {
    base: "vs-dark",
    inherit: true,
    rules: [
      { token: "comment",    foreground: "6A9955", fontStyle: "italic" },
      { token: "keyword",    foreground: "C586C0" },
      { token: "string",     foreground: "CE9178" },
      { token: "number",     foreground: "B5CEA8" },
      { token: "type",       foreground: "4EC9B0", fontStyle: "italic" },
      { token: "class",      foreground: "4EC9B0" },
      { token: "function",   foreground: "DCDCAA" },
      { token: "variable",   foreground: "9CDCFE" },
      { token: "operator",   foreground: "D4D4D4" },
      { token: "delimiter",  foreground: "D4D4D4" },
      { token: "identifier", foreground: "D4D4D4" },
    ],
    colors: {
      "editor.background":                "#1F1F1F",
      "editor.foreground":                "#CCCCCC",
      "editor.lineHighlightBackground":   "#2B2B2B",
      "editor.selectionBackground":       "#264F78",
      "editorCursor.foreground":          "#CCCCCC",
      "editorWhitespace.foreground":      "#404040",
      "editorLineNumber.foreground":      "#6E7681",
      "editorLineNumber.activeForeground":"#CCCCCC",
      "editorIndentGuide.background":     "#2B2B2B",
      "editorIndentGuide.activeBackground": "#3C3C3C",
    },
  });
  monaco.editor.setTheme("dark-modern");
}
