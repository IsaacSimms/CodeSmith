// == Justification Editor Component == //
interface JustificationEditorProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
}

const MAX_CHARS = 10_000;

export function JustificationEditor({ value, onChange, onSubmit, isSubmitting }: JustificationEditorProps) {
  const charCount    = value.length;
  const atLimit      = charCount >= MAX_CHARS;
  const nearLimit    = charCount >= MAX_CHARS * 0.8;
  const counterClass = atLimit ? "text-red-400" : nearLimit ? "text-yellow-400" : "text-gray-600";

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && !e.shiftKey && !e.ctrlKey && !e.metaKey && !e.altKey && !isSubmitting) {
      e.preventDefault();
      onSubmit();
    }
  }

  return (
    <div className="flex h-full flex-col overflow-hidden bg-gray-900">
      {/* == Editor Header == */}
      <div className="flex items-center justify-between border-b border-gray-700 px-4 py-1.5">
        <h3 className="text-xs font-semibold text-gray-400">Your Justification</h3>
        <span className={`text-xs tabular-nums ${counterClass}`}>
          {charCount.toLocaleString()} / {MAX_CHARS.toLocaleString()}
        </span>
      </div>

      {/* == Textarea == */}
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={handleKeyDown}
        maxLength={MAX_CHARS}
        disabled={isSubmitting}
        placeholder="Write your infrastructure design justification here. Engage with each required tradeoff by explaining the causal reasoning behind your decisions…"
        className="flex-1 resize-none bg-gray-900 p-4 font-mono text-sm text-gray-200 placeholder-gray-600 outline-none disabled:opacity-60"
        spellCheck={false}
      />

      {/* == Footer Hint == */}
      <div className="border-t border-gray-700 px-4 py-1.5">
        <p className="text-xs text-gray-600">
          <kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Enter</kbd> to submit ·{" "}
          <kbd className="rounded bg-gray-700 px-1 py-0.5 font-mono text-gray-400">Shift+Enter</kbd> for new line
        </p>
      </div>
    </div>
  );
}
