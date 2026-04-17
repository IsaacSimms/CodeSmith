// == Prompt Editors Component == //
import Editor from "@monaco-editor/react";
import type { ChallengeResponse } from "../types";
import { defineMonokaiTheme } from "../../shared/monacoTheme";

interface PromptEditorsProps {
  challenge: ChallengeResponse;
  systemPromptContent: string;
  userMessageContent: string;
  onSystemPromptChange: (value: string) => void;
  onUserMessageChange: (value: string) => void;
}

export function PromptEditors({
  challenge,
  systemPromptContent,
  userMessageContent,
  onSystemPromptChange,
  onUserMessageChange,
}: PromptEditorsProps) {
  const systemEditable = challenge.editableFields.some((f) => f.fieldType === "SystemPrompt");
  const userEditable   = challenge.editableFields.some((f) => f.fieldType === "UserMessage");

  const systemField = challenge.editableFields.find((f) => f.fieldType === "SystemPrompt");
  const userField   = challenge.editableFields.find((f) => f.fieldType === "UserMessage");

  // When both fields are editable, split 50/50; when only one, it takes full height
  const bothEditable = systemEditable && userEditable;
  const topHeight    = bothEditable ? "50%" : "100%";

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* == System Prompt Editor == */}
      <div style={{ height: topHeight }} className="flex min-h-0 flex-col">
        <EditorHeader
          label="System Prompt"
          locked={!systemEditable}
          lockedTitle="This prompt is locked by the challenge"
        />
        <div className="flex-1 overflow-hidden">
          <Editor
            height="100%"
            language="plaintext"
            theme="vs-dark"
            value={systemEditable ? systemPromptContent : challenge.lockedSystemPrompt}
            onChange={(v) => systemEditable && onSystemPromptChange(v ?? "")}
            beforeMount={defineMonokaiTheme}
            onMount={(_editor, monaco) => monaco.editor.setTheme("monokai")}
            options={{
              fontSize:             14,
              minimap:              { enabled: false },
              scrollBeyondLastLine: false,
              automaticLayout:      true,
              padding:              { top: 12, bottom: 12 },
              readOnly:             !systemEditable,
              placeholder:          systemField?.placeholder ?? "",
              wordWrap:             "on",
            }}
          />
        </div>
      </div>

      {/* == Divider between editors (only when both are shown) == */}
      {bothEditable && (
        <div className="h-1.5 shrink-0 bg-gray-700" />
      )}

      {/* == User Message Editor == */}
      {bothEditable && (
        <div style={{ height: "50%" }} className="flex min-h-0 flex-col">
          <EditorHeader
            label="User Message"
            locked={!userEditable}
            lockedTitle="This message is provided by each test input"
          />
          <div className="flex-1 overflow-hidden">
            <Editor
              height="100%"
              language="plaintext"
              theme="monokai"
              value={userMessageContent}
              onChange={(v) => onUserMessageChange(v ?? "")}
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

function EditorHeader({
  label,
  locked,
  lockedTitle,
}: {
  label: string;
  locked: boolean;
  lockedTitle: string;
}) {
  return (
    <div className="flex items-center gap-2 border-b border-gray-900 bg-gray-800 px-4 py-1.5">
      <h3 className="text-xs font-semibold text-gray-400">{label}</h3>
      {locked && (
        <span title={lockedTitle} className="rounded bg-gray-700 px-1.5 py-0.5 text-xs text-gray-500">
          locked
        </span>
      )}
    </div>
  );
}
