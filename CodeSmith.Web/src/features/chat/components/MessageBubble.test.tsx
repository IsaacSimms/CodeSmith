// == Message Bubble Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MessageBubble } from "./MessageBubble";

describe("MessageBubble", () => {
  it("renders the message content", () => {
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
    expect(bubble?.className).toContain("bg-blue-600");
  });

  it("applies assistant styling for Assistant role", () => {
    render(<MessageBubble role="Assistant" content="assistant msg" />);

    const bubble = screen.getByText("assistant msg").closest("div");
    expect(bubble?.className).toContain("bg-gray-700");
  });

  it("preserves whitespace in content", () => {
    const { container } = render(<MessageBubble role="User" content={"line1\nline2"} />);

    const paragraph = container.querySelector("p");
    expect(paragraph?.textContent).toBe("line1\nline2");
    expect(paragraph?.className).toContain("whitespace-pre-wrap");
  });
});
