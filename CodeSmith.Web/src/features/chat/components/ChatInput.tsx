// == Chat Input Component == //
import { useRef } from "react";

interface ChatInputProps {
  onSend: (message: string) => void;
  isLoading: boolean;
}

export function ChatInput({ onSend, isLoading }: ChatInputProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = textareaRef.current?.value.trim() ?? "";
    if (trimmed.length === 0 || trimmed.length > 2000) return;
    onSend(trimmed);
    if (textareaRef.current) {
      textareaRef.current.value = "";
      // Reset height after clearing
      textareaRef.current.style.height = "auto";
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    // Submit on Enter, allow newline with Shift+Enter
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e as unknown as React.FormEvent);
    }
  }

  function handleChange(e: React.ChangeEvent<HTMLTextAreaElement>) {
    const el = e.target;
    // Auto-grow: reset then set to scrollHeight so it expands with content
    el.style.height = "auto";
    el.style.height = `${el.scrollHeight}px`;
  }

  return (
    <form onSubmit={handleSubmit} className="flex items-end gap-2 border-t border-gray-700 p-4">
      <textarea
        ref={textareaRef}
        rows={1}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        placeholder="Ask for guidance..."
        maxLength={2000}
        disabled={isLoading}
        className="flex-1 resize-none overflow-hidden rounded-lg bg-gray-850 px-4 py-2 text-white placeholder-gray-500 outline-none focus:ring-2 focus:ring-monokai-cyan"
      />
      <button
        type="submit"
        disabled={isLoading}
        className="rounded-lg bg-monokai-pink px-4 py-2 font-semibold text-white transition-colors hover:bg-monokai-pink-dark disabled:opacity-50"
      >
        {isLoading ? "Building..." : "Send"}
      </button>
    </form>
  );
}
