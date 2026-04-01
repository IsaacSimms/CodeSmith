// == Chat Input Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatInput } from "./ChatInput";

describe("ChatInput", () => {
  it("renders an input field and send button", () => {
    render(<ChatInput onSend={vi.fn()} isLoading={false} />);

    expect(screen.getByPlaceholderText("Ask for guidance...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Send" })).toBeInTheDocument();
  });

  it("calls onSend with trimmed message on submit", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    render(<ChatInput onSend={onSend} isLoading={false} />);

    const input = screen.getByPlaceholderText("Ask for guidance...");
    await user.type(input, "  Hello world  ");
    await user.click(screen.getByRole("button", { name: "Send" }));

    expect(onSend).toHaveBeenCalledOnce();
    expect(onSend).toHaveBeenCalledWith("Hello world");
  });

  it("clears the input after sending", async () => {
    const user = userEvent.setup();
    render(<ChatInput onSend={vi.fn()} isLoading={false} />);

    const input = screen.getByPlaceholderText("Ask for guidance...");
    await user.type(input, "Hello");
    await user.click(screen.getByRole("button", { name: "Send" }));

    expect(input).toHaveValue("");
  });

  it("does not call onSend when message is empty", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    render(<ChatInput onSend={onSend} isLoading={false} />);

    await user.click(screen.getByRole("button", { name: "Send" }));

    expect(onSend).not.toHaveBeenCalled();
  });

  it("does not call onSend when message is only whitespace", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    render(<ChatInput onSend={onSend} isLoading={false} />);

    const input = screen.getByPlaceholderText("Ask for guidance...");
    await user.type(input, "   ");
    await user.click(screen.getByRole("button", { name: "Send" }));

    expect(onSend).not.toHaveBeenCalled();
  });

  it("disables input and button when loading", () => {
    render(<ChatInput onSend={vi.fn()} isLoading={true} />);

    expect(screen.getByPlaceholderText("Ask for guidance...")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Sending..." })).toBeDisabled();
  });

  it("shows 'Sending...' text when loading", () => {
    render(<ChatInput onSend={vi.fn()} isLoading={true} />);

    expect(screen.getByRole("button", { name: "Sending..." })).toBeInTheDocument();
  });

  it("disables send button when input is empty", () => {
    render(<ChatInput onSend={vi.fn()} isLoading={false} />);

    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
  });

  it("submits on enter key", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    render(<ChatInput onSend={onSend} isLoading={false} />);

    const input = screen.getByPlaceholderText("Ask for guidance...");
    await user.type(input, "Hello{Enter}");

    expect(onSend).toHaveBeenCalledOnce();
    expect(onSend).toHaveBeenCalledWith("Hello");
  });
});
