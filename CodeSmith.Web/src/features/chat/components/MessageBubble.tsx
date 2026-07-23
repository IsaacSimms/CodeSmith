// == Message Bubble Component == //
import Markdown from "react-markdown";
import type { Components } from "react-markdown";
import remarkGfm from "remark-gfm";
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
        className="rounded bg-gray-800 px-1 py-0.5 font-mono text-sm text-[#CE9178]"
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
  // GFM tables — horizontal scroll so wide grids stay inside the bubble
  table({ children }) {
    return (
      <div className="my-2 overflow-x-auto rounded border border-gray-600">
        <table className="w-full border-collapse text-sm">{children}</table>
      </div>
    );
  },
  thead({ children }) {
    return <thead className="bg-gray-800">{children}</thead>;
  },
  th({ children }) {
    return (
      <th className="border border-gray-600 px-2 py-1.5 text-left font-semibold text-gray-100">
        {children}
      </th>
    );
  },
  td({ children }) {
    return (
      <td className="border border-gray-600 px-2 py-1.5 text-gray-100">{children}</td>
    );
  },
  // Autolinks / markdown links — dark-theme safe, open externally
  a({ href, children }) {
    return (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className="text-accent underline underline-offset-2 hover:text-accent-hover"
      >
        {children}
      </a>
    );
  },
};

export function MessageBubble({ role, content }: MessageBubbleProps) {
  const isUser = role === "User";

  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      <div
        className={`max-w-[85%] rounded-lg px-4 py-2 ${
          isUser ? "bg-accent text-white" : "bg-gray-700 text-gray-100"
        }`}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap break-words">{content}</p>
        ) : (
          <Markdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
            {content}
          </Markdown>
        )}
      </div>
    </div>
  );
}
