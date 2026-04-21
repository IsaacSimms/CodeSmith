// == Code Block Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CodeBlock } from "./CodeBlock";

// Mock react-syntax-highlighter to avoid jsdom style-injection issues
vi.mock("react-syntax-highlighter", () => ({
  default: ({ children, language }: { children: string; language: string }) => (
    <pre data-testid="syntax-highlighter" data-language={language}>
      <code>{children}</code>
    </pre>
  ),
}));

vi.mock("react-syntax-highlighter/dist/esm/styles/hljs", () => ({
  monokai: {},
}));

const CODE = 'function hello() {\n  return "world";\n}';

describe("CodeBlock", () => {
  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: { writeText: vi.fn().mockResolvedValue(undefined) },
    });
  });

  it("renders the code content", () => {
    render(<CodeBlock language="typescript">{CODE}</CodeBlock>);

    expect(screen.getByTestId("syntax-highlighter")).toHaveTextContent('function hello()');
  });

  it("displays the language label in the header", () => {
    render(<CodeBlock language="typescript">{CODE}</CodeBlock>);

    expect(screen.getByText("typescript")).toBeInTheDocument();
  });

  it("falls back to 'plaintext' when language is empty", () => {
    render(<CodeBlock language="">{CODE}</CodeBlock>);

    expect(screen.getByText("plaintext")).toBeInTheDocument();
  });

  it("shows a copy button", () => {
    render(<CodeBlock language="python">{CODE}</CodeBlock>);

    expect(screen.getByRole("button", { name: /copy/i })).toBeInTheDocument();
  });

  it("copies code to clipboard and briefly shows 'copied'", async () => {
    render(<CodeBlock language="python">{CODE}</CodeBlock>);

    const button = screen.getByRole("button", { name: /copy/i });
    fireEvent.click(button);

    await waitFor(() => expect(screen.getByText("copied")).toBeInTheDocument());
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(CODE);
  });
});
