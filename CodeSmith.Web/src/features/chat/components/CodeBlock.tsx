// == Code Block Component == //
import { useState } from "react";
import SyntaxHighlighter from "react-syntax-highlighter";
import { monokai } from "react-syntax-highlighter/dist/esm/styles/hljs";

interface CodeBlockProps {
  language: string;
  children: string;
}

// == Code Block == //
export function CodeBlock({ language, children }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const displayLanguage = language || "plaintext";

  function handleCopy() {
    navigator.clipboard.writeText(children).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  return (
    <div className="my-2 overflow-hidden rounded-lg border border-gray-600">
      {/* Header bar */}
      <div className="flex items-center justify-between bg-gray-800 px-4 py-1.5">
        <span className="font-mono text-xs text-gray-400">{displayLanguage}</span>
        <button
          onClick={handleCopy}
          className="text-xs text-gray-400 transition-colors hover:text-gray-100"
          aria-label="Copy code"
        >
          {copied ? "copied" : "copy"}
        </button>
      </div>

      {/* Code body */}
      <SyntaxHighlighter
        language={displayLanguage}
        style={monokai}
        customStyle={{ margin: 0, borderRadius: 0, fontSize: "0.8125rem" }}
        wrapLongLines
      >
        {children}
      </SyntaxHighlighter>
    </div>
  );
}
