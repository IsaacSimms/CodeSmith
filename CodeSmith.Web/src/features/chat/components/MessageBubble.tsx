// == Message Bubble Component == //
import Markdown from "react-markdown";
import type { Components } from "react-markdown";
import type { MessageRole } from "../types";
import { CodeBlock } from "./CodeBlock";

interface MessageBubbleProps {
  role: MessageRole;
  content: string;
}

// == Markdown component overrides for assistant messages == //
const markdownComponents: Components = {
  // Fenced code blocks
  code({ className, children, ...props }) {
    const languageMatch = /language-(\w+)/.exec(className ?? "");
    const rawContent = String(children);
    // react-markdown always appends \n to fenced block content (labeled or not).
    // Inline code never has a trailing \n — use that to distinguish the two.
    const isBlock = !!languageMatch || rawContent.endsWith("\n");

    if (isBlock) {
      return (
        <CodeBlock language={languageMatch?.[1] ?? ""}>
          {rawContent.replace(/\n$/, "")}
        </CodeBlock>
      );
    }

    // Inline code
    return (
      <code
        className="rounded bg-gray-800 px-1 py-0.5 font-mono text-sm text-monokai-yellow"
        {...props}
      >
        {children}
      </code>
    );
  },
  // Paragraph — prevent double-wrapping block elements
  p({ children }) {
    return <p className="mb-2 last:mb-0 break-words">{children}</p>;
  },
};

export function MessageBubble({ role, content }: MessageBubbleProps) {
  const isUser = role === "User";

  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[85%] rounded-lg px-4 py-2 ${
          isUser ? "bg-monokai-pink text-white" : "bg-gray-700 text-gray-100"
        }`}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap break-words">{content}</p>
        ) : (
          <Markdown components={markdownComponents}>{content}</Markdown>
        )}
      </div>
    </div>
  );
}
