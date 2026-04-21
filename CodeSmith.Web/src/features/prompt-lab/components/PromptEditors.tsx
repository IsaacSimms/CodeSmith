// == Prompt Editors Component == //
import Editor, { type OnMount } from "@monaco-editor/react";
import type { ChallengeResponse } from "../types";
import { defineMonokaiTheme } from "../../shared/monacoTheme";

const MAX_PROMPT_CHARS = 30_000;

interface PromptEditorsProps {
  challenge: ChallengeResponse;
  systemPromptContent: string;
  userMessageContent: string;
  onSystemPromptChange: (value: string) => void;
  onUserMessageChange: (value: string) => void;
  onSubmit: () => void;
}

export function PromptEditors({
  challenge,
  systemPromptContent,
  userMessageContent,
  onSystemPromptChange,
  onUserMessageChange,
  onSubmit,
}: PromptEditorsProps) {
  // Enter submits; Shift+Enter inserts a newline (standard chat-editor pattern)
  const bindSubmitKey: OnMount = (editor, monaco) => {
    monaco.editor.setTheme("monokai");
    editor.addCommand(monaco.KeyCode.Enter, () => onSubmit());
    editor.addCommand(monaco.KeyMod.Shift | monaco.KeyCode.Enter, () => {
      editor.trigger("keyboard", "type", { text: "\n" });
    });
  };
  const systemEditable = challenge.editableFields.some((f) => f.fieldType === "SystemPrompt");
  const userEditable   = challenge.editableFields.some((f) => f.fieldType === "UserMessage");

  const systemField = challenge.editableFields.find((f) => f.fieldType === "SystemPrompt");
  const userField   = challenge.editableFields.find((f) => f.fieldType === "UserMessage");

  // Split 50/50 when both fields are editable; each solo field takes full height
  const bothEditable     = systemEditable && userEditable;
  const systemHeight     = bothEditable ? "50%" : "100%";
  const userMessageHeight = bothEditable ? "50%" : "100%";

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == System Prompt Editor (shown when editable) == */}
      {systemEditable && (
        <div style={{ height: systemHeight }} className="flex min-h-0 flex-col">
          <EditorHeader label="System Prompt" charCount={systemPromptContent.length} />
          <div className="flex-1 overflow-hidden">
            <Editor
              height="100%"
              language="plaintext"
              theme="vs-dark"
              value={systemPromptContent}
              onChange={(v) => onSystemPromptChange(v ?? "")}
              beforeMount={defineMonokaiTheme}
              onMount={bindSubmitKey}
              options={{
                fontSize:             14,
                minimap:              { enabled: false },
                scrollBeyondLastLine: false,
                automaticLayout:      true,
                padding:              { top: 12, bottom: 12 },
                readOnly:             false,
                placeholder:          systemField?.placeholder ?? "",
                wordWrap:             "on",
              }}
            />
          </div>
        </div>
      )}

      {/* == Divider between editors (only when both are shown) == */}
      {bothEditable && (
        <div className="h-1.5 shrink-0 bg-gray-700" />
      )}

      {/* == User Message Editor (shown when editable) == */}
      {userEditable && (
        <div style={{ height: userMessageHeight }} className="flex min-h-0 flex-col">
          <EditorHeader label="User Message" charCount={userMessageContent.length} />
          <div className="flex-1 overflow-hidden">
            <Editor
              height="100%"
              language="plaintext"
              theme="monokai"
              value={userMessageContent}
              onChange={(v) => onUserMessageChange(v ?? "")}
              beforeMount={defineMonokaiTheme}
              onMount={bindSubmitKey}
              options={{
                fontSize:             14,
                minimap:              { enabled: false },
                scrollBeyondLastLine: false,
                automaticLayout:      true,
                padding:              { top: 12, bottom: 12 },
                readOnly:             false,
                placeholder:          userField?.placeholder ?? "",
                wordWrap:             "on",
              }}
            />
          </div>
        </div>
      )}
    </div>
  );
}

// == Editor Header Sub-component == //

function EditorHeader({ label, charCount }: { label: string; charCount: number }) {
  const remaining   = MAX_PROMPT_CHARS - charCount;
  const usageRatio  = charCount / MAX_PROMPT_CHARS;
  const counterColor = usageRatio >= 1 ? "text-red-400" : usageRatio >= 0.8 ? "text-yellow-400" : "text-gray-600";

  return (
    <div className="flex items-center justify-between border-b border-gray-900 bg-gray-800 px-4 py-1.5">
      <h3 className="text-xs font-semibold text-gray-400">{label}</h3>
      <span className={`font-mono text-xs tabular-nums ${counterColor}`}>
        {remaining.toLocaleString()} / {MAX_PROMPT_CHARS.toLocaleString()}
      </span>
    </div>
  );
}
