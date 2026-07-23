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
    expect(bubble?.className).toContain("bg-accent");
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

  it("renders GFM pipe tables as HTML tables for assistant messages", () => {
    const content = [
      "| Number | Binary |",
      "| --- | --- |",
      "| 1 | 0001 |",
      "| 2 | 0010 |",
    ].join("\n");

    const { container } = render(<MessageBubble role="Assistant" content={content} />);

    const table = container.querySelector("table");
    expect(table).toBeInTheDocument();
    expect(screen.getByText("Number")).toBeInTheDocument();
    expect(screen.getByText("Binary")).toBeInTheDocument();
    expect(screen.getByText("0001")).toBeInTheDocument();
    expect(screen.getByText("0010")).toBeInTheDocument();
  });

  it("wraps assistant tables in a horizontal scroll container", () => {
    const content = [
      "| A | B |",
      "| --- | --- |",
      "| 1 | 2 |",
    ].join("\n");

    const { container } = render(<MessageBubble role="Assistant" content={content} />);

    const table = container.querySelector("table");
    const scrollWrapper = table?.parentElement;
    expect(scrollWrapper).toBeTruthy();
    expect(scrollWrapper?.className).toContain("overflow-x-auto");
  });

  it("renders assistant markdown links with external target and safe rel", () => {
    render(
      <MessageBubble
        role="Assistant"
        content="See [docs](https://example.com/docs) for details."
      />,
    );

    const link = screen.getByRole("link", { name: "docs" });
    expect(link).toHaveAttribute("href", "https://example.com/docs");
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("does not parse GFM tables in user messages", () => {
    const content = [
      "| Number | Binary |",
      "| --- | --- |",
      "| 1 | 0001 |",
    ].join("\n");

    const { container } = render(<MessageBubble role="User" content={content} />);

    expect(container.querySelector("table")).not.toBeInTheDocument();
    expect(container.querySelector("p")?.textContent).toContain("| Number | Binary |");
  });
});
