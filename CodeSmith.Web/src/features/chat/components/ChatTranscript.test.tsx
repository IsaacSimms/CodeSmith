// == Chat Transcript Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ChatTranscript } from "./ChatTranscript";
import type { ChatMessage } from "../types";

const messages: ChatMessage[] = [
  { role: "User",      content: "How do I start?", timestamp: "2026-07-19T00:00:00Z" },
  { role: "Assistant", content: "Try a for loop",  timestamp: "2026-07-19T00:00:01Z" },
];

describe("ChatTranscript", () => {
  it("renders a bubble per message", () => {
    render(<ChatTranscript messages={messages} isStreaming={false} streamingText="" failedTurn={null} />);

    expect(screen.getByText("How do I start?")).toBeInTheDocument();
    expect(screen.getByText("Try a for loop")).toBeInTheDocument();
  });

  it("shows the empty state only while there are no messages", () => {
    const { rerender } = render(
      <ChatTranscript messages={[]} isStreaming={false} streamingText="" failedTurn={null} emptyStateText="Ask a question." />
    );
    expect(screen.getByText("Ask a question.")).toBeInTheDocument();

    rerender(
      <ChatTranscript messages={messages} isStreaming={false} streamingText="" failedTurn={null} emptyStateText="Ask a question." />
    );
    expect(screen.queryByText("Ask a question.")).not.toBeInTheDocument();
  });

  it("renders the in-flight reply while streaming", () => {
    render(<ChatTranscript messages={messages} isStreaming={true} streamingText="Half a rep" failedTurn={null} />);

    expect(screen.getByText("Half a rep")).toBeInTheDocument();
  });

  it("renders the dimmed remains of a failed turn", () => {
    render(
      <ChatTranscript
        messages={messages}
        isStreaming={false}
        streamingText=""
        failedTurn={{ partial: "half a hint", message: "AI service error" }}
      />
    );

    expect(screen.getByTestId("failed-turn")).toBeInTheDocument();
    expect(screen.getByText("half a hint")).toBeInTheDocument();
  });
});
