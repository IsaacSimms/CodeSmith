// == Message Bubble Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MessageBubble } from "./MessageBubble";

// Mock CodeBlock so we can assert it renders without needing react-syntax-highlighter
vi.mock("./CodeBlock", () => ({
  CodeBlock: ({ language, children }: { language: string; children: string }) => (
    <pre data-testid="code-block" data-language={language}>
      {children}
    </pre>
  ),
}));

describe("MessageBubble", () => {
  it("renders user message content as plain text", () => {
    render(<MessageBubble role="User" content="Hello world" />);

    expect(screen.getByText("Hello world")).toBeInTheDocument();
  });

  it("aligns user messages to the right", () => {
    const { container } = render(<MessageBubble role="User" content="test" />);

    const wrapper = container.firstElementChild;
    expect(wrapper?.className).toContain("justify-end");
  });

  it("aligns assistant messages to the left", () => {
    const { container } = render(<MessageBubble role="Assistant" content="test" />);

    const wrapper = container.firstElementChild;
    expect(wrapper?.className).toContain("justify-start");
  });

  it("applies user styling for User role", () => {
    render(<MessageBubble role="User" content="user msg" />);

    const bubble = screen.getByText("user msg").closest("div");
    expect(bubble?.className).toContain("bg-monokai-pink");
  });

  it("applies assistant styling for Assistant role", () => {
    render(<MessageBubble role="Assistant" content="assistant msg" />);

    const bubble = screen.getByText("assistant msg").closest("div");
    expect(bubble?.className).toContain("bg-gray-700");
  });

  it("preserves whitespace for user messages", () => {
    const { container } = render(<MessageBubble role="User" content={"line1\nline2"} />);

    const paragraph = container.querySelector("p");
    expect(paragraph?.textContent).toBe("line1\nline2");
    expect(paragraph?.className).toContain("whitespace-pre-wrap");
  });

  it("renders fenced code blocks via CodeBlock for assistant messages", () => {
    const content = "Here is some code:\n\n```typescript\nconst x = 1;\n```";
    render(<MessageBubble role="Assistant" content={content} />);

    const codeBlock = screen.getByTestId("code-block");
    expect(codeBlock).toBeInTheDocument();
    expect(codeBlock).toHaveAttribute("data-language", "typescript");
    expect(codeBlock.textContent).toContain("const x = 1;");
  });

  it("renders unlabeled fenced code blocks via CodeBlock for assistant messages", () => {
    const content = "Here is some code:\n\n```\nconst x = 1;\n```";
    render(<MessageBubble role="Assistant" content={content} />);

    const codeBlock = screen.getByTestId("code-block");
    expect(codeBlock).toBeInTheDocument();
    expect(codeBlock).toHaveAttribute("data-language", "");
    expect(codeBlock.textContent).toContain("const x = 1;");
  });

  it("does not render CodeBlock for plain user messages", () => {
    render(<MessageBubble role="User" content="```typescript\nconst x = 1;\n```" />);

    expect(screen.queryByTestId("code-block")).not.toBeInTheDocument();
  });

  it("renders assistant plain text through markdown", () => {
    render(<MessageBubble role="Assistant" content="Just a normal reply." />);

    expect(screen.getByText("Just a normal reply.")).toBeInTheDocument();
  });
});
